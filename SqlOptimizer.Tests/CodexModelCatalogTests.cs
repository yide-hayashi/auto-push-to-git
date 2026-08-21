using System.Text.Json;
using Project_auto_push_to_gitlab.SqlOptimizer.Application;
using Project_auto_push_to_gitlab.SqlOptimizer.Codex;
using Project_auto_push_to_gitlab.SqlOptimizer.Persistence;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

/// <summary>
/// 驗證Codex動態模型目錄解析、未知能力保留、選擇校正與LocalAppData替代儲存行為。
/// </summary>
/// <remarks>
/// UI：間接驗證frm_SqlOptimizer的cmbCodexModel、cmbCodexReasoning、cmbCodexSpeed資料來源；不啟動Codex或Oracle。
/// </remarks>
public sealed class CodexModelCatalogTests
{
    /// <summary>
    /// 驗證model/list單頁回應能保留新模型、未知推論量、Fast服務層、移行資訊及下一頁Cursor。
    /// </summary>
    /// <remarks>
    /// UI：間接驗證frm_SqlOptimizer三個Codex ComboBox與lblCodexCatalogStatus所需資料。
    /// </remarks>
    [Fact]
    public void Parser_PreservesUnknownCapabilitiesAndPagingCursor()
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：以未固定的新值驗證目錄解析不依賴Enum或內建模型清單。
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "data": [
                {
                  "id": "future-model-id",
                  "model": "gpt-future-codex",
                  "displayName": "GPT Future Codex",
                  "description": "Future model",
                  "hidden": false,
                  "isDefault": true,
                  "defaultReasoningEffort": "ultra-future",
                  "supportedReasoningEfforts": [
                    { "reasoningEffort": "ultra-future", "description": "Future effort" }
                  ],
                  "serviceTiers": [
                    { "id": "priority", "name": "Fast", "description": "Fast responses" }
                  ],
                  "defaultServiceTier": "default",
                  "upgrade": "gpt-next-codex",
                  "upgradeInfo": "Move when available"
                }
              ],
              "nextCursor": "page-2"
            }
            """);

        CodexModelCatalogPage page =
            CodexModelCatalogService.ParseModelListResult(
                document.RootElement);

        CodexModelOption model = Assert.Single(page.Models);
        Assert.Equal("page-2", page.NextCursor);
        Assert.Equal("gpt-future-codex", model.Model);
        Assert.True(model.IsDefault);
        Assert.Equal("ultra-future", Assert.Single(model.SupportedReasoningEfforts).Value);
        CodexServiceTierOption tier = Assert.Single(model.ServiceTiers);
        Assert.Equal("priority", tier.Id);
        Assert.Equal("Fast", tier.Name);
        Assert.Equal("gpt-next-codex", model.UpgradeModel);
        Assert.Equal("Move when available", model.MigrationMessage);
    }

    /// <summary>
    /// 驗證保存值不受新模型支援時會切換至該模型的預設推論量及標準速度。
    /// </summary>
    /// <remarks>
    /// UI：間接驗證cmbCodexModel變更後cmbCodexReasoning與cmbCodexSpeed的重建規則。
    /// </remarks>
    [Fact]
    public void Resolver_ReplacesUnsupportedReasoningAndSpeedWithDefaults()
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：確保跨模型切換不保留無效能力組合。
        CodexModelOption model = CreateModel(
            "gpt-new",
            isDefault: true,
            defaultReasoning: "medium",
            reasoningValues: ["low", "medium"],
            serviceTierIds: ["priority"]);
        CodexModelCatalog catalog = new(
            [model],
            DateTimeOffset.UtcNow);

        CodexExecutionSelection selection =
            CodexExecutionSelectionResolver.Resolve(
                catalog,
                new CodexExecutionSelection(
                    "gpt-new",
                    "unknown-effort",
                    "unknown-tier"),
                out string? warning);

        Assert.Equal("gpt-new", selection.Model);
        Assert.Equal("medium", selection.ReasoningEffort);
        Assert.Equal("default", selection.ServiceTier);
        Assert.Null(warning);
    }

    /// <summary>
    /// 驗證找不到前次模型時會選擇CLI標示的預設模型，而非任何程式內固定名稱。
    /// </summary>
    /// <remarks>
    /// UI：間接驗證廢止模型無可用移行資訊時cmbCodexModel的fallback行為。
    /// </remarks>
    [Fact]
    public void Resolver_UsesCatalogDefaultWhenSavedModelIsUnavailable()
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：只信任當次目錄的isDefault，不固定目前產品模型。
        CodexModelCatalog catalog = new(
            [
                CreateModel("gpt-first", false, "low", ["low"], []),
                CreateModel("gpt-cli-default", true, "high", ["high"], [])
            ],
            DateTimeOffset.UtcNow);

        CodexExecutionSelection selection =
            CodexExecutionSelectionResolver.Resolve(
                catalog,
                new CodexExecutionSelection("retired-model", "high", "default"),
                out string? warning);

        Assert.Equal("gpt-cli-default", selection.Model);
        Assert.Contains("retired-model", warning, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證保存目錄已通知的廢止模型移行目標，在最新目錄仍可用時優先於CLI預設fallback。
    /// </summary>
    /// <remarks>
    /// UI：間接驗證frm_SqlOptimizer啟動更新後cmbCodexModel自動切換與警告顯示。
    /// </remarks>
    [Fact]
    public void ControllerResolver_UsesAdvertisedMigrationForRemovedModel()
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：確認已移除模型依保存的官方移行通知切換，不任意猜測目標。
        CodexModelOption retired = CreateModel(
            "gpt-retired",
            false,
            "medium",
            ["medium"],
            []) with
        {
            UpgradeModel = "gpt-migration-target",
            MigrationMessage = "Move to the new model"
        };
        CodexModelCatalog previousCatalog = new(
            [retired],
            DateTimeOffset.UtcNow.AddDays(-1));
        CodexModelCatalog liveCatalog = new(
            [
                CreateModel("gpt-cli-default", true, "medium", ["medium"], []),
                CreateModel("gpt-migration-target", false, "high", ["high"], [])
            ],
            DateTimeOffset.UtcNow);

        CodexExecutionSelection migrated =
            SqlOptimizerUiController.ResolveAdvertisedUpgrade(
                liveCatalog,
                previousCatalog,
                new CodexExecutionSelection(
                    "gpt-retired",
                    "medium",
                    "default"),
                out string? warning);

        Assert.Equal("gpt-migration-target", migrated.Model);
        Assert.Contains("gpt-migration-target", warning, StringComparison.Ordinal);
    }

    /// <summary>
    /// 驗證設定檔不存在時不產生錯誤，成功保存後可完整還原目錄與未知字串選擇。
    /// </summary>
    /// <returns>非同步儲存與讀取驗證工作。</returns>
    /// <remarks>
    /// UI：間接驗證frm_SqlOptimizer啟動時「快取優先、無快取則繼承」流程；只操作測試暫存目錄。
    /// </remarks>
    [Fact]
    public async Task SettingsStore_HandlesMissingFileAndRoundTripsSnapshot()
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：驗證保存內容不需固定Schema值即可跨啟動復元。
        using TestDirectory directory = new("codex-settings-roundtrip");
        CodexExecutionSettingsStore store = new(directory.Path);
        CodexExecutionSettingsLoadResult missing = await store.LoadAsync();
        Assert.Null(missing.Snapshot);
        Assert.Null(missing.Warning);

        CodexModelCatalog catalog = new(
            [CreateModel("gpt-future", true, "future", ["future"], ["priority"])],
            DateTimeOffset.UtcNow);
        CodexExecutionSettingsSnapshot expected = new(
            new CodexExecutionSelection("gpt-future", "future", "priority"),
            catalog);
        await store.SaveAsync(expected);

        CodexExecutionSettingsLoadResult loaded = await store.LoadAsync();
        Assert.Null(loaded.Warning);
        Assert.Equal(expected.Selection, loaded.Snapshot?.Selection);
        Assert.Equal("gpt-future", Assert.Single(loaded.Snapshot!.Catalog!.Models).Model);
        Assert.Empty(Directory.EnumerateFiles(directory.Path, "*.tmp"));
    }

    /// <summary>
    /// 驗證設定快取JSON損壞時回傳警告與null快照，使Controller可安全繼承Codex設定。
    /// </summary>
    /// <returns>非同步損壞檔案讀取驗證工作。</returns>
    /// <remarks>
    /// UI：間接驗證frm_SqlOptimizer的lblCodexCatalogStatus fallback顯示；只操作測試暫存目錄。
    /// </remarks>
    [Fact]
    public async Task SettingsStore_ReturnsWarningForCorruptedCache()
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：將快取損壞視為可恢復狀況，不阻止Codex啟動。
        using TestDirectory directory = new("codex-settings-corrupt");
        string path = Path.Combine(
            directory.Path,
            "codex-execution-settings.json");
        await File.WriteAllTextAsync(path, "{ invalid json");
        CodexExecutionSettingsStore store = new(directory.Path);

        CodexExecutionSettingsLoadResult loaded = await store.LoadAsync();

        Assert.Null(loaded.Snapshot);
        Assert.False(string.IsNullOrWhiteSpace(loaded.Warning));
    }

    /// <summary>
    /// 建立測試使用的動態Codex模型項目，不使用產品固定模型或固定能力列舉。
    /// </summary>
    /// <param name="model">模型識別字，不可為空白。</param>
    /// <param name="isDefault">CLI將模型標示為預設時為true。</param>
    /// <param name="defaultReasoning">模型預設推論量，不可為空白。</param>
    /// <param name="reasoningValues">模型支援推論量字串；不可為null。</param>
    /// <param name="serviceTierIds">CLI額外回傳的服務層字串；不可為null。</param>
    /// <returns>供Resolver與Store測試使用的完整模型項目。</returns>
    /// <remarks>UI：無；僅由本測試類別建立輸入資料。</remarks>
    private static CodexModelOption CreateModel(
        string model,
        bool isDefault,
        string defaultReasoning,
        IReadOnlyList<string> reasoningValues,
        IReadOnlyList<string> serviceTierIds)
    {
        return new CodexModelOption(
            model,
            model,
            model,
            string.Empty,
            isDefault,
            defaultReasoning,
            reasoningValues
                .Select(value => new CodexReasoningEffortOption(value, string.Empty))
                .ToArray(),
            serviceTierIds
                .Select(value => new CodexServiceTierOption(value, value, string.Empty))
                .ToArray(),
            "default",
            null,
            null);
    }
}
