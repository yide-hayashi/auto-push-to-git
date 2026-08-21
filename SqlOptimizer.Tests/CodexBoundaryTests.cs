using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Project_auto_push_to_gitlab.SqlOptimizer.Codex;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

/// <summary>
/// 驗證Codex JSONL、MCP啟動邊界、執行引數分離與秘密資訊排除規則。
/// </summary>
/// <remarks>
/// UI：間接驗證frm_SqlOptimizer的btnStartCodex執行要求；不啟動正式Codex或Oracle。
/// </remarks>
public sealed class CodexBoundaryTests
{
    [Fact]
    public void JsonlParser_ReadsSafeProgressAndUsage()
    {
        const string json = """
            {
              "type": "item.completed",
              "thread_id": "thread-123",
              "item": {
                "id": "item-456",
                "type": "agent_message",
                "status": "completed",
                "text": "Optimization complete"
              },
              "usage": {
                "input_tokens": 100,
                "cached_input_tokens": 25,
                "output_tokens": 40,
                "reasoning_output_tokens": 10
              }
            }
            """;

        bool parsed = CodexJsonlEventParser.TryParse(
            json,
            out CodexJsonlEvent? parsedEvent,
            out string? error);

        Assert.True(parsed, error);
        Assert.NotNull(parsedEvent);
        Assert.Equal("item.completed", parsedEvent.Type);
        Assert.Equal("thread-123", parsedEvent.ThreadId);
        Assert.Equal("item-456", parsedEvent.ItemId);
        Assert.Equal("agent_message", parsedEvent.ItemType);
        Assert.Equal("completed", parsedEvent.Status);
        Assert.Equal("Optimization complete", parsedEvent.Message);
        Assert.Equal(100, parsedEvent.Usage?.InputTokens);
        Assert.Equal(10, parsedEvent.Usage?.ReasoningOutputTokens);
    }

    [Fact]
    public void JsonlParser_DropsToolArgumentsAndSecrets()
    {
        const string secret = "MCP-TOOL-SECRET-95b31917";
        string json = $$"""
            {
              "type": "item.completed",
              "item": {
                "id": "tool-1",
                "type": "mcp_tool_call",
                "status": "completed",
                "arguments": {
                  "bind_value": "{{secret}}",
                  "bearer_token": "{{secret}}"
                }
              }
            }
            """;

        Assert.True(
            CodexJsonlEventParser.TryParse(
                json,
                out CodexJsonlEvent? parsedEvent,
                out string? error),
            error);
        Assert.NotNull(parsedEvent);
        Assert.Null(parsedEvent.Message);

        string sanitizedJson = JsonSerializer.Serialize(parsedEvent);
        Assert.False(sanitizedJson.Contains(secret, StringComparison.Ordinal));
        Assert.False(sanitizedJson.Contains("arguments", StringComparison.OrdinalIgnoreCase));
        Assert.False(sanitizedJson.Contains("bearer_token", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("{\"status\":\"completed\"}")]
    public void JsonlParser_RejectsMalformedOrTypelessLines(string line)
    {
        Assert.False(
            CodexJsonlEventParser.TryParse(
                line,
                out CodexJsonlEvent? parsedEvent,
                out string? error));
        Assert.Null(parsedEvent);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    /// <summary>
    /// 驗證不安全工作階段、非Loopback端點與含控制字元的模型設定在啟動程序前即被拒絕。
    /// </summary>
    /// <returns>完成所有非同步ArgumentException驗證的工作。</returns>
    /// <remarks>UI：間接驗證frm_SqlOptimizer的btnStartCodex安全邊界。</remarks>
    [Fact]
    public async Task Runner_RejectsCommandInjectionSessionIdsAndRemoteEndpoints()
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：防止模型設定利用換行注入額外CLI選項。
        using TestDirectory directory = new("codex-validation");
        await using CodexCliRunner runner = new(
            new CodexCliLocator(),
            new SqlOptimizerTokenStore(directory.Path));

        await Assert.ThrowsAsync<ArgumentException>(
            () => runner.RunAsync(
                new CodexRunRequest(
                    "session\"; calc.exe",
                    new Uri("http://127.0.0.1:57321/mcp"))));
        await Assert.ThrowsAsync<ArgumentException>(
            () => runner.RunAsync(
                new CodexRunRequest(
                    "safe-session",
                    new Uri("http://example.com:57321/mcp"))));
        await Assert.ThrowsAsync<ArgumentException>(
            () => runner.RunAsync(
                new CodexRunRequest(
                    "safe-session",
                    new Uri("https://127.0.0.1:57321/mcp"))));
        await Assert.ThrowsAsync<ArgumentException>(
            () => runner.RunAsync(
                new CodexRunRequest(
                    "safe-session",
                    new Uri("http://127.0.0.1:57321/mcp"),
                    ExecutionSelection: new CodexExecutionSelection(
                        "gpt-safe\r\n--dangerous-option",
                        "high",
                        "default"))));
    }

    /// <summary>
    /// 驗證模型、推論量與速度皆以分離ArgumentList或跳脫後TOML值傳遞，Token不進入命令列。
    /// </summary>
    /// <returns>完成Runner建立與非同步釋放的工作。</returns>
    /// <remarks>UI：間接驗證frm_SqlOptimizer的btnStartCodex啟動參數。</remarks>
    [Fact]
    public async Task Runner_BuildsSeparatedArguments_AndKeepsTokenOutOfCommandLine()
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：以含Shell符號與引號的值驗證引數分離及TOML跳脫。
        using TestDirectory directory = new("codex-arguments");
        string workingDirectory = System.IO.Path.Combine(
            directory.Path,
            "working directory with spaces");
        Directory.CreateDirectory(workingDirectory);
        SqlOptimizerTokenStore tokenStore =
            new(System.IO.Path.Combine(directory.Path, "tokens"));
        await using CodexCliRunner runner =
            new(new CodexCliLocator(), tokenStore);
        CodexRunRequest request = new(
            "session_123-abc",
            new Uri("http://127.0.0.1:57321/mcp"),
            workingDirectory,
            @"C:\unused\codex.exe",
            new CodexExecutionSelection(
                "gpt-future;calc.exe",
                "ultra\"future",
                "priority"));

        MethodInfo method = typeof(CodexCliRunner).GetMethod(
            "CreateStartInfo",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CreateStartInfo was not found.");
        ProcessStartInfo startInfo = (ProcessStartInfo)(
            method.Invoke(
                runner,
                [@"C:\unused\codex.exe", workingDirectory, request])
            ?? throw new InvalidOperationException("CreateStartInfo returned null."));
        string[] arguments = startInfo.ArgumentList.ToArray();
        string token = startInfo.Environment[
            SqlOptimizerTokenStore.BearerTokenEnvironmentVariable]
            ?? throw new InvalidOperationException("Token environment variable is missing.");

        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
        Assert.Contains("-a", arguments);
        Assert.Contains("never", arguments);
        int modelArgumentIndex = Array.IndexOf(arguments, "--model");
        Assert.True(modelArgumentIndex >= 0);
        Assert.Equal("gpt-future;calc.exe", arguments[modelArgumentIndex + 1]);
        Assert.Contains(
            "model_reasoning_effort=\"ultra\\\"future\"",
            arguments);
        Assert.Contains("service_tier=\"priority\"", arguments);
        Assert.Contains("--json", arguments);
        Assert.Contains("--ephemeral", arguments);
        Assert.Contains("read-only", arguments);
        Assert.Contains("--skip-git-repo-check", arguments);
        Assert.Contains(workingDirectory, arguments);
        Assert.Contains(
            arguments,
            argument => argument.Contains("session_123-abc", StringComparison.Ordinal));
        Assert.DoesNotContain(
            arguments,
            argument => argument.Contains(token, StringComparison.Ordinal));
        Assert.True(token.Length >= 40);
    }

    /// <summary>
    /// 驗證Codex app-server的JSONL標準輸入使用無BOM UTF-8，避免第一筆initialize要求遭到拒絕。
    /// </summary>
    /// <remarks>
    /// UI：間接驗證frm_SqlOptimizer的btnRefreshCodexModels與畫面顯示時自動更新模型清單。
    /// 本測試只檢查程序啟動設定，不啟動Codex或連線Oracle。
    /// </remarks>
    [Fact]
    public void CatalogStartInfo_UsesBomlessUtf8ForJsonLineRequests()
    {
        // 1150821 codex しずくちゃん_gpt-5.6-sol：防止StandardInputEncoding重新使用會寫入BOM的Encoding.UTF8。
        ProcessStartInfo startInfo =
            CodexModelCatalogService.CreateStartInfo(
                @"C:\unused\codex.exe");
        Encoding inputEncoding = startInfo.StandardInputEncoding
            ?? throw new InvalidOperationException(
                "Codex app-server input encoding was not configured.");

        Assert.Equal("utf-8", inputEncoding.WebName);
        Assert.Empty(inputEncoding.GetPreamble());
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardInput);
    }

    [Fact]
    public void McpCommandPreview_UsesOnlyLoopbackAndTokenEnvironmentVariable()
    {
        Uri endpoint = new("http://localhost:57321/mcp");

        string preview =
            CodexMcpConfigurationService.BuildCommandPreview(endpoint);

        Assert.Contains(endpoint.AbsoluteUri, preview);
        Assert.Contains(
            SqlOptimizerTokenStore.BearerTokenEnvironmentVariable,
            preview);
        Assert.Throws<ArgumentException>(
            () => CodexMcpConfigurationService.BuildCommandPreview(
                new Uri("http://database.example.test:57321/mcp")));
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, true, false)]
    public void SharedSettingsAutoApplyDecision_IsBasedOnCurrentState(
        bool codexAvailable,
        bool isRegistered,
        bool matchesExpectedConfiguration,
        bool expected)
    {
        CodexMcpRegistrationState state = new(
            codexAvailable,
            isRegistered,
            matchesExpectedConfiguration,
            "http://localhost:57321/mcp",
            SqlOptimizerTokenStore.BearerTokenEnvironmentVariable,
            "codex mcp add auto_call_git_sql_optimizer",
            null);

        Assert.Equal(
            expected,
            CodexMcpConfigurationService.ShouldAutoApplyConfiguration(state));
    }
}
