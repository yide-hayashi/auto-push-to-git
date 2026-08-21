using System.Text.Json;
using Project_auto_push_to_gitlab.SqlOptimizer.Codex;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Persistence;

/// <summary>
/// 彙整要保存的Codex執行選擇與最後一次成功取得的模型目錄。
/// </summary>
/// <param name="Selection">使用者最後選擇的執行設定；尚未選擇時為null。</param>
/// <param name="Catalog">最後一次由Codex CLI成功取得的目錄；尚未取得時為null。</param>
public sealed record CodexExecutionSettingsSnapshot(
    CodexExecutionSelection? Selection,
    CodexModelCatalog? Catalog);

/// <summary>
/// 保存Codex執行設定讀取結果與無法還原時的使用者警告。
/// </summary>
/// <param name="Snapshot">正常還原的設定；檔案不存在或損壞時為null。</param>
/// <param name="Warning">說明讀取失敗的安全警告；正常時為null。</param>
public sealed record CodexExecutionSettingsLoadResult(
    CodexExecutionSettingsSnapshot? Snapshot,
    string? Warning);

/// <summary>
/// 將Codex執行選擇與動態模型目錄原子化保存至LocalAppData。
/// </summary>
/// <remarks>
/// UI：由frm_SqlOptimizer的Codex設定欄經SqlOptimizerUiController間接使用。
/// 保存內容不包含SQL、Bind值、Token或認證資訊。
/// </remarks>
public sealed class CodexExecutionSettingsStore
{
    private static readonly string SettingsFileName =
        "codex-execution-settings.json";
    private readonly string _storageDirectory;
    private readonly string _settingsPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// 建立使用指定目錄或SQL最佳化預設LocalAppData目錄的設定Store。
    /// </summary>
    /// <param name="storageDirectory">保存目錄；null時使用AutoCallGit\SqlOptimizer，不修改傳入字串。</param>
    /// <remarks>
    /// UI：由SqlOptimizerApplicationRuntime建立，間接提供給frm_SqlOptimizer的Codex設定欄。
    /// 建構時不建立目錄或檔案。
    /// </remarks>
    public CodexExecutionSettingsStore(string? storageDirectory = null)
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：統一使用SqlOptimizer LocalAppData且不在建構階段建立檔案。
        _storageDirectory = Path.GetFullPath(
            storageDirectory ??
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "AutoCallGit",
                "SqlOptimizer"));
        _settingsPath = Path.Combine(_storageDirectory, SettingsFileName);
    }

    /// <summary>
    /// 讀取前次Codex執行選擇與模型目錄。
    /// </summary>
    /// <param name="cancellationToken">取消檔案讀取的權杖。</param>
    /// <returns>還原設定與警告；檔案不存在時兩者皆為null，損壞時Snapshot為null並回傳Warning。</returns>
    /// <remarks>
    /// UI：由frm_SqlOptimizer的Shown經SqlOptimizerUiController間接使用。
    /// 讀取失敗不阻止Codex啟動，呼叫端可改為繼承Codex預設值。
    /// </remarks>
    public async Task<CodexExecutionSettingsLoadResult> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return new CodexExecutionSettingsLoadResult(null, null);
        }

        // 1150820 codex しずくちゃん_gpt-5.6-sol：將設定損壞視為可恢復狀況，回傳安全的fallback原因。
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using FileStream stream = new(
                _settingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            CodexExecutionSettingsSnapshot? snapshot =
                await JsonSerializer.DeserializeAsync<CodexExecutionSettingsSnapshot>(
                        stream,
                        _jsonOptions,
                        cancellationToken)
                    .ConfigureAwait(false);

            return snapshot is null
                ? new CodexExecutionSettingsLoadResult(
                    null,
                    "保存済みCodex設定が空のため、Codex既定値を使用します。")
                : new CodexExecutionSettingsLoadResult(snapshot, null);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException or
            NotSupportedException)
        {
            return new CodexExecutionSettingsLoadResult(
                null,
                $"保存済みCodex設定を読めないため、Codex既定値を使用します: {exception.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 原子化保存目前Codex執行選擇與最後一次成功取得的模型目錄。
    /// </summary>
    /// <param name="snapshot">要保存的選擇與目錄，不可為null且不得包含秘密資訊。</param>
    /// <param name="cancellationToken">取消檔案寫入的權杖。</param>
    /// <returns>完成暫存檔寫入與原子置換的非同步工作。</returns>
    /// <exception cref="SqlOptimizerPersistenceException">目錄建立、JSON序列化、寫入或置換失敗時拋出。</exception>
    /// <remarks>
    /// UI：cmbCodexModel、cmbCodexReasoning、cmbCodexSpeed變更時由Controller間接使用。
    /// 先完整寫入同目錄暫存檔，再置換既有檔案。
    /// </remarks>
    public async Task SaveAsync(
        CodexExecutionSettingsSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporaryPath = null;
        try
        {
            // 1150820 codex しずくちゃん_gpt-5.6-sol：將選擇值與模型目錄視為同一Snapshot原子更新。
            Directory.CreateDirectory(_storageDirectory);
            temporaryPath = Path.Combine(
                _storageDirectory,
                $".{SettingsFileName}.{Guid.NewGuid():N}.tmp");
            await using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                        stream,
                        snapshot,
                        _jsonOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(_settingsPath))
            {
                File.Replace(temporaryPath, _settingsPath, null, true);
            }
            else
            {
                File.Move(temporaryPath, _settingsPath);
            }

            temporaryPath = null;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            JsonException or
            NotSupportedException)
        {
            throw new SqlOptimizerPersistenceException(
                "Codex実行設定を保存できませんでした。",
                exception);
        }
        finally
        {
            if (temporaryPath is not null && File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            _gate.Release();
        }
    }
}
