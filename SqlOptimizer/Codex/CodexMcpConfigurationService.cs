using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Codex;

public sealed record CodexMcpRegistrationState(
    bool CodexAvailable,
    bool IsRegistered,
    bool MatchesExpectedConfiguration,
    string? Endpoint,
    string? BearerTokenEnvironmentVariable,
    string CommandPreview,
    string? Error);

public enum CodexMcpRegistrationOutcome
{
    Registered,
    AlreadyRegistered,
    Conflict,
    Failed
}

public sealed record CodexMcpRegistrationResult(
    CodexMcpRegistrationOutcome Outcome,
    CodexMcpRegistrationState State,
    string? Error);

/// <summary>
/// Inspects and, only after an explicit caller confirmation, registers the
/// local Streamable HTTP MCP server in Codex configuration.
/// </summary>
public sealed class CodexMcpConfigurationService
{
    private const int MaxProcessOutputLength = 256 * 1024;
    private readonly CodexCliLocator _locator;

    public CodexMcpConfigurationService(CodexCliLocator locator)
    {
        _locator = locator;
    }

    public async Task<CodexMcpRegistrationState> InspectAsync(
        Uri endpoint,
        string? codexExecutablePath = null,
        CancellationToken cancellationToken = default)
    {
        ValidateEndpoint(endpoint);
        CodexCliDiscovery discovery = _locator.Locate(codexExecutablePath);
        string preview = BuildCommandPreview(endpoint);

        if (!discovery.IsAvailable ||
            string.IsNullOrWhiteSpace(discovery.ExecutablePath))
        {
            return new CodexMcpRegistrationState(
                false,
                false,
                false,
                null,
                null,
                preview,
                discovery.Error);
        }

        ProcessResult result = await InvokeAsync(
                discovery.ExecutablePath,
                ["mcp", "get", CodexCliRunner.McpServerName, "--json"],
                cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            if (result.StandardError.Contains(
                    "No MCP server named",
                    StringComparison.OrdinalIgnoreCase))
            {
                return new CodexMcpRegistrationState(
                    true,
                    false,
                    false,
                    null,
                    null,
                    preview,
                    null);
            }

            return new CodexMcpRegistrationState(
                true,
                false,
                false,
                null,
                null,
                preview,
                SanitizeError(result.StandardError));
        }

        if (!TryReadSafeConfiguration(
                result.StandardOutput,
                out string? configuredEndpoint,
                out string? configuredTokenEnvironmentVariable))
        {
            return new CodexMcpRegistrationState(
                true,
                true,
                false,
                null,
                null,
                preview,
                "Codex MCP設定のJSON形式を認識できませんでした。");
        }

        bool matches =
            Uri.TryCreate(
                configuredEndpoint,
                UriKind.Absolute,
                out Uri? configuredUri) &&
            Uri.Compare(
                configuredUri,
                endpoint,
                UriComponents.HttpRequestUrl,
                UriFormat.SafeUnescaped,
                StringComparison.OrdinalIgnoreCase) == 0 &&
            string.Equals(
                configuredTokenEnvironmentVariable,
                SqlOptimizerTokenStore.BearerTokenEnvironmentVariable,
                StringComparison.Ordinal);

        return new CodexMcpRegistrationState(
            true,
            true,
            matches,
            configuredEndpoint,
            configuredTokenEnvironmentVariable,
            preview,
            null);
    }

    /// <param name="replaceExisting">
    /// Must be true only after the user has reviewed the existing and expected
    /// configuration in the UI and explicitly approved replacement.
    /// </param>
    /// <summary>
    /// 判斷目前Codex MCP狀態是否需要在啟動時自動套用此應用程式的共用設定。
    /// </summary>
    /// <param name="state">已檢查到的MCP設定狀態。</param>
    /// <returns>已安裝/匹配的設定不需要自動套用時為false；其他狀況則為true。</returns>
    public static bool ShouldAutoApplyConfiguration(
        CodexMcpRegistrationState state)
    {
        if (!state.CodexAvailable)
        {
            return false;
        }

        return !state.MatchesExpectedConfiguration;
    }

    /// <summary>
    /// 由應用程式啟動流程自動確保本機MCP設定符合本程式預期，不要求使用者再手動確認。
    /// </summary>
    /// <param name="endpoint">本應用程式的循環回路MCP端點。</param>
    /// <param name="codexExecutablePath">可選的Codex CLI路徑；未指定時自動定位。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>自動套用結果；若已符合設定則回傳AlreadyRegistered。</returns>
    public async Task<CodexMcpRegistrationResult> EnsureConfiguredAsync(
        Uri endpoint,
        string? codexExecutablePath = null,
        CancellationToken cancellationToken = default)
    {
        CodexMcpRegistrationState state = await InspectAsync(
                endpoint,
                codexExecutablePath,
                cancellationToken)
            .ConfigureAwait(false);

        if (!ShouldAutoApplyConfiguration(state))
        {
            return new CodexMcpRegistrationResult(
                state.MatchesExpectedConfiguration
                    ? CodexMcpRegistrationOutcome.AlreadyRegistered
                    : CodexMcpRegistrationOutcome.Failed,
                state,
                state.Error);
        }

        return await ApplyAsync(
                endpoint,
                replaceExisting: true,
                codexExecutablePath,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<CodexMcpRegistrationResult> ApplyAsync(
        Uri endpoint,
        bool replaceExisting,
        string? codexExecutablePath = null,
        CancellationToken cancellationToken = default)
    {
        CodexMcpRegistrationState state = await InspectAsync(
                endpoint,
                codexExecutablePath,
                cancellationToken)
            .ConfigureAwait(false);

        if (!state.CodexAvailable)
        {
            return new CodexMcpRegistrationResult(
                CodexMcpRegistrationOutcome.Failed,
                state,
                state.Error);
        }

        if (state.MatchesExpectedConfiguration)
        {
            return new CodexMcpRegistrationResult(
                CodexMcpRegistrationOutcome.AlreadyRegistered,
                state,
                null);
        }

        if (state.IsRegistered && !replaceExisting)
        {
            return new CodexMcpRegistrationResult(
                CodexMcpRegistrationOutcome.Conflict,
                state,
                "同名のMCP設定があります。内容を確認してから置換を承認してください。");
        }

        CodexCliDiscovery discovery = _locator.Locate(codexExecutablePath);
        string executablePath = discovery.ExecutablePath!;

        if (state.IsRegistered)
        {
            ProcessResult removeResult = await InvokeAsync(
                    executablePath,
                    ["mcp", "remove", CodexCliRunner.McpServerName],
                    cancellationToken)
                .ConfigureAwait(false);

            if (removeResult.ExitCode != 0)
            {
                return new CodexMcpRegistrationResult(
                    CodexMcpRegistrationOutcome.Failed,
                    state,
                    SanitizeError(removeResult.StandardError));
            }
        }

        ProcessResult addResult = await InvokeAsync(
                executablePath,
                [
                    "mcp",
                    "add",
                    CodexCliRunner.McpServerName,
                    "--url",
                    endpoint.AbsoluteUri,
                    "--bearer-token-env-var",
                    SqlOptimizerTokenStore.BearerTokenEnvironmentVariable
                ],
                cancellationToken)
            .ConfigureAwait(false);

        if (addResult.ExitCode != 0)
        {
            return new CodexMcpRegistrationResult(
                CodexMcpRegistrationOutcome.Failed,
                state,
                SanitizeError(addResult.StandardError));
        }

        CodexMcpRegistrationState updatedState = await InspectAsync(
                endpoint,
                codexExecutablePath,
                cancellationToken)
            .ConfigureAwait(false);

        return new CodexMcpRegistrationResult(
            updatedState.MatchesExpectedConfiguration
                ? CodexMcpRegistrationOutcome.Registered
                : CodexMcpRegistrationOutcome.Failed,
            updatedState,
            updatedState.MatchesExpectedConfiguration
                ? null
                : updatedState.Error ?? "MCP設定を確認できませんでした。");
    }

    public static string BuildCommandPreview(Uri endpoint)
    {
        ValidateEndpoint(endpoint);
        return
            $"codex mcp add {CodexCliRunner.McpServerName} " +
            $"--url \"{endpoint.AbsoluteUri}\" " +
            "--bearer-token-env-var " +
            SqlOptimizerTokenStore.BearerTokenEnvironmentVariable;
    }

    private static async Task<ProcessResult> InvokeAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            return new ProcessResult(
                -1,
                string.Empty,
                "Codex CLIプロセスを開始できませんでした。");
        }

        using CancellationTokenRegistration registration =
            cancellationToken.Register(
                static state =>
                {
                    Process target = (Process)state!;
                    try
                    {
                        if (!target.HasExited)
                        {
                            target.Kill(entireProcessTree: true);
                        }
                    }
                    catch (Exception exception)
                        when (exception is InvalidOperationException or
                              NotSupportedException or
                              System.ComponentModel.Win32Exception)
                    {
                    }
                },
                process);

        Task<string> outputTask = ReadBoundedAsync(process.StandardOutput);
        Task<string> errorTask = ReadBoundedAsync(process.StandardError);
        await process.WaitForExitAsync(CancellationToken.None)
            .ConfigureAwait(false);
        await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return new ProcessResult(
            process.ExitCode,
            await outputTask.ConfigureAwait(false),
            await errorTask.ConfigureAwait(false));
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader)
    {
        char[] buffer = new char[4096];
        StringBuilder builder = new();
        while (true)
        {
            int read = await reader.ReadAsync(buffer).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            int remaining = MaxProcessOutputLength - builder.Length;
            if (remaining > 0)
            {
                builder.Append(buffer, 0, Math.Min(remaining, read));
            }
        }

        return builder.ToString();
    }

    private static bool TryReadSafeConfiguration(
        string json,
        out string? endpoint,
        out string? bearerTokenEnvironmentVariable)
    {
        endpoint = null;
        bearerTokenEnvironmentVariable = null;

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            FindSafeFields(
                document.RootElement,
                ref endpoint,
                ref bearerTokenEnvironmentVariable);
            return !string.IsNullOrWhiteSpace(endpoint);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void FindSafeFields(
        JsonElement element,
        ref string? endpoint,
        ref string? bearerTokenEnvironmentVariable)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (endpoint is null &&
                    string.Equals(
                        property.Name,
                        "url",
                        StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    endpoint = property.Value.GetString();
                }
                else if (bearerTokenEnvironmentVariable is null &&
                         string.Equals(
                             property.Name,
                             "bearer_token_env_var",
                             StringComparison.OrdinalIgnoreCase) &&
                         property.Value.ValueKind == JsonValueKind.String)
                {
                    bearerTokenEnvironmentVariable =
                        property.Value.GetString();
                }
                else
                {
                    FindSafeFields(
                        property.Value,
                        ref endpoint,
                        ref bearerTokenEnvironmentVariable);
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                FindSafeFields(
                    child,
                    ref endpoint,
                    ref bearerTokenEnvironmentVariable);
            }
        }
    }

    private static string SanitizeError(string standardError)
    {
        if (string.IsNullOrWhiteSpace(standardError))
        {
            return "Codex CLIがエラーを返しました。";
        }

        string sanitized = standardError.Trim();
        return sanitized.Length <= 4000
            ? sanitized
            : sanitized[..4000] + "…";
    }

    private static void ValidateEndpoint(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri ||
            !string.Equals(
                endpoint.Scheme,
                Uri.UriSchemeHttp,
                StringComparison.OrdinalIgnoreCase) ||
            !(string.Equals(
                  endpoint.Host,
                  "localhost",
                  StringComparison.OrdinalIgnoreCase) ||
              System.Net.IPAddress.TryParse(
                  endpoint.Host,
                  out System.Net.IPAddress? address) &&
              System.Net.IPAddress.IsLoopback(address)))
        {
            throw new ArgumentException(
                "MCPエンドポイントはループバックHTTPである必要があります。",
                nameof(endpoint));
        }
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
