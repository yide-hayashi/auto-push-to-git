using System.Diagnostics;
using System.Text;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Codex;

public sealed record CodexCliDiagnostics(
    bool IsAvailable,
    string? ExecutablePath,
    string? Version,
    bool IsAuthenticated,
    string? Error);

public sealed class CodexCliDiagnosticsService
{
    private readonly CodexCliLocator _locator;

    public CodexCliDiagnosticsService(CodexCliLocator locator)
    {
        _locator = locator;
    }

    public async Task<CodexCliDiagnostics> CheckAsync(
        string? explicitPath = null,
        CancellationToken cancellationToken = default)
    {
        CodexCliDiscovery discovery = _locator.Locate(explicitPath);
        if (!discovery.IsAvailable ||
            string.IsNullOrWhiteSpace(discovery.ExecutablePath))
        {
            return new CodexCliDiagnostics(
                false,
                null,
                null,
                false,
                discovery.Error);
        }

        DiagnosticProcessResult versionResult = await RunAsync(
                discovery.ExecutablePath,
                ["--version"],
                cancellationToken)
            .ConfigureAwait(false);

        if (versionResult.ExitCode != 0)
        {
            return new CodexCliDiagnostics(
                true,
                discovery.ExecutablePath,
                null,
                false,
                ToSafeError(versionResult.StandardError));
        }

        DiagnosticProcessResult loginResult = await RunAsync(
                discovery.ExecutablePath,
                ["login", "status"],
                cancellationToken)
            .ConfigureAwait(false);

        return new CodexCliDiagnostics(
            true,
            discovery.ExecutablePath,
            FirstNonEmptyLine(versionResult.StandardOutput),
            loginResult.ExitCode == 0,
            loginResult.ExitCode == 0
                ? null
                : "Codex CLIにログインしていません。`codex login` を実行してください。");
    }

    private static async Task<DiagnosticProcessResult> RunAsync(
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
            return new DiagnosticProcessResult(
                -1,
                string.Empty,
                "Codex CLIを開始できませんでした。");
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

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(CancellationToken.None)
            .ConfigureAwait(false);
        await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return new DiagnosticProcessResult(
            process.ExitCode,
            Truncate(await outputTask.ConfigureAwait(false)),
            Truncate(await errorTask.ConfigureAwait(false)));
    }

    private static string? FirstNonEmptyLine(string value)
    {
        return value.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .FirstOrDefault();
    }

    private static string ToSafeError(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Codex CLIがエラーを返しました。"
            : Truncate(value.Trim());
    }

    private static string Truncate(string value)
    {
        const int maximum = 4096;
        return value.Length <= maximum ? value : value[..maximum] + "…";
    }

    private sealed record DiagnosticProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
