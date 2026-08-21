namespace Project_auto_push_to_gitlab.SqlOptimizer.Codex;

/// <summary>
/// 保存Codex可選推論量識別字與CLI提供的用途說明。
/// </summary>
/// <param name="Value">傳給Codex CLI的推論量識別字，不可為空白；未來新增的未知值也原樣保存。</param>
/// <param name="Description">Codex CLI回傳的使用者說明；允許空字串。</param>
public sealed record CodexReasoningEffortOption(
    string Value,
    string Description)
{
    /// <summary>
    /// 回傳ComboBox顯示用的推論量識別字。
    /// </summary>
    /// <returns>與傳給Codex CLI相同的推論量識別字。</returns>
    /// <remarks>
    /// UI：直接顯示於frm_SqlOptimizer的cmbCodexReasoning；本方法不操作UI控制項。
    /// </remarks>
    public override string ToString()
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：以CLI識別字直接呈現未知推論量。
        return Value;
    }
}

/// <summary>
/// 將Codex模型支援的服務層分為CLI識別字與畫面顯示資訊保存。
/// </summary>
/// <param name="Id">傳給Codex CLI service_tier的識別字，不可為空白。</param>
/// <param name="Name">顯示於畫面的服務層名稱，不可為空白。</param>
/// <param name="Description">Codex CLI提供的速度或用量說明；允許空字串。</param>
public sealed record CodexServiceTierOption(
    string Id,
    string Name,
    string Description)
{
    /// <summary>
    /// 回傳ComboBox顯示用的服務層名稱。
    /// </summary>
    /// <returns>Codex CLI提供的名稱，或畫面補入的標準層名稱。</returns>
    /// <remarks>
    /// UI：直接顯示於frm_SqlOptimizer的cmbCodexSpeed；本方法不操作UI控制項。
    /// </remarks>
    public override string ToString()
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：以CLI名稱呈現服務層並保留未來新增值。
        return Name;
    }
}

/// <summary>
/// 保存Codex CLI公開的單一模型及其可選推論量與服務層。
/// </summary>
/// <param name="Id">目錄內識別模型的ID，不可為空白。</param>
/// <param name="Model">傳給Codex CLI --model的模型識別字，不可為空白。</param>
/// <param name="DisplayName">顯示給使用者的模型名稱，不可為空白。</param>
/// <param name="Description">模型用途說明；允許空字串。</param>
/// <param name="IsDefault">目錄將此模型標示為預設時為true。</param>
/// <param name="DefaultReasoningEffort">模型預設推論量識別字，不可為空白。</param>
/// <param name="SupportedReasoningEfforts">模型支援的推論量清單，不可為null；允許空集合。</param>
/// <param name="ServiceTiers">模型額外支援的服務層清單，不可為null；允許空集合。</param>
/// <param name="DefaultServiceTier">目錄預設服務層識別字；未提供時為null。</param>
/// <param name="UpgradeModel">模型廢止時建議的移行目標；未通知時為null。</param>
/// <param name="MigrationMessage">廢止或移行說明；未通知時為null。</param>
public sealed record CodexModelOption(
    string Id,
    string Model,
    string DisplayName,
    string Description,
    bool IsDefault,
    string DefaultReasoningEffort,
    IReadOnlyList<CodexReasoningEffortOption> SupportedReasoningEfforts,
    IReadOnlyList<CodexServiceTierOption> ServiceTiers,
    string? DefaultServiceTier,
    string? UpgradeModel,
    string? MigrationMessage)
{
    /// <summary>
    /// 回傳ComboBox顯示用的模型名稱。
    /// </summary>
    /// <returns>Codex CLI提供的顯示名稱。</returns>
    /// <remarks>
    /// UI：直接顯示於frm_SqlOptimizer的cmbCodexModel；本方法不操作UI控制項。
    /// </remarks>
    public override string ToString()
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：顯示CLI提供的模型名稱而非固定程式文字。
        return DisplayName;
    }
}

/// <summary>
/// 保存單次model/list取得的可用模型清單與取得時間。
/// </summary>
/// <param name="Models">排除hidden模型後的可用清單，不可為null；允許空集合。</param>
/// <param name="RetrievedAtUtc">從Codex CLI取得清單的UTC時間。</param>
public sealed record CodexModelCatalog(
    IReadOnlyList<CodexModelOption> Models,
    DateTimeOffset RetrievedAtUtc);

/// <summary>
/// 保存單次Codex啟動所固定的模型、推論量與服務層。
/// </summary>
/// <param name="Model">傳給--model的值；null時繼承Codex全域設定。</param>
/// <param name="ReasoningEffort">傳給model_reasoning_effort的值；null時繼承Codex全域設定。</param>
/// <param name="ServiceTier">傳給service_tier的值；null時繼承Codex全域設定。</param>
public sealed record CodexExecutionSelection(
    string? Model,
    string? ReasoningEffort,
    string? ServiceTier)
{
    /// <summary>
    /// 提供三個項目全部繼承Codex全域設定的選擇值。
    /// </summary>
    /// <remarks>
    /// UI：frm_SqlOptimizer無法取得模型清單時使用。
    /// </remarks>
    public static CodexExecutionSelection InheritDefaults { get; } =
        new(null, null, null);

    /// <summary>
    /// 判斷三個項目是否全部繼承Codex全域設定。
    /// </summary>
    /// <returns>三個項目全部為null或空白時回傳true，否則回傳false。</returns>
    /// <remarks>
    /// UI：由frm_SqlOptimizer選擇顯示與SqlOptimizerUiController執行紀錄間接使用。
    /// </remarks>
    public bool InheritsDefaults
    {
        get
        {
            // 1150820 codex しずくちゃん_gpt-5.6-sol：以三個空值明確表示繼承Codex全域設定。
            return string.IsNullOrWhiteSpace(Model) &&
                   string.IsNullOrWhiteSpace(ReasoningEffort) &&
                   string.IsNullOrWhiteSpace(ServiceTier);
        }
    }

    /// <summary>
    /// 將選擇內容轉為不含秘密資訊的簡短執行紀錄文字。
    /// </summary>
    /// <returns>繼承時回傳Codex預設值說明，否則回傳模型、推論量與速度組合。</returns>
    /// <remarks>
    /// UI：由SqlOptimizerUiController顯示於frm_SqlOptimizer的txtActivity。
    /// </remarks>
    public string ToDisplayText()
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：將執行時固定的Codex設定安全顯示於畫面紀錄。
        return InheritsDefaults
            ? "Codex既定値"
            : $"model={Model ?? "既定"}, reasoning={ReasoningEffort ?? "既定"}, speed={ServiceTier ?? "既定"}";
    }
}

/// <summary>
/// 依目前可用模型能力，將保存的Codex選擇校正為安全且支援的組合。
/// </summary>
public static class CodexExecutionSelectionResolver
{
    /// <summary>
    /// 將保存選擇套用至目前目錄，並以模型預設值替換不支援的能力。
    /// </summary>
    /// <param name="catalog">目前可用模型目錄，不可為null。</param>
    /// <param name="preferred">前次選擇；null或繼承設定時使用目錄預設模型。</param>
    /// <param name="warning">發生模型移行或能力校正時的畫面訊息；不需警告時為null。</param>
    /// <returns>目錄內可執行的模型、推論量與服務層組合；沒有模型時回傳繼承設定。</returns>
    /// <remarks>
    /// UI：由SqlOptimizerUiController與frm_SqlOptimizer間接使用，同步cmbCodexModel、cmbCodexReasoning、cmbCodexSpeed。
    /// 本方法不直接操作UI控制項。
    /// </remarks>
    public static CodexExecutionSelection Resolve(
        CodexModelCatalog catalog,
        CodexExecutionSelection? preferred,
        out string? warning)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        warning = null;

        // 1150820 codex しずくちゃん_gpt-5.6-sol：模型廢止或能力變更後仍確定校正為目前可用組合。
        if (catalog.Models.Count == 0)
        {
            return CodexExecutionSelection.InheritDefaults;
        }

        CodexModelOption? preferredModel = string.IsNullOrWhiteSpace(preferred?.Model)
            ? null
            : catalog.Models.FirstOrDefault(item => string.Equals(
                item.Model,
                preferred.Model,
                StringComparison.OrdinalIgnoreCase));

        CodexModelOption selectedModel = preferredModel
            ?? ResolveUpgradeTarget(catalog, preferred?.Model)
            ?? catalog.Models.FirstOrDefault(item => item.IsDefault)
            ?? catalog.Models[0];

        if (preferredModel is null && !string.IsNullOrWhiteSpace(preferred?.Model))
        {
            warning = $"前回のモデル {preferred.Model} は利用できないため、{selectedModel.DisplayName} を選択しました。";
        }
        else if (!string.IsNullOrWhiteSpace(selectedModel.UpgradeModel))
        {
            warning = $"{selectedModel.DisplayName} には移行先 {selectedModel.UpgradeModel} が案内されています。";
        }

        string reasoning = selectedModel.SupportedReasoningEfforts
            .FirstOrDefault(item => string.Equals(
                item.Value,
                preferred?.ReasoningEffort,
                StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?? selectedModel.SupportedReasoningEfforts
                .FirstOrDefault(item => string.Equals(
                    item.Value,
                    selectedModel.DefaultReasoningEffort,
                    StringComparison.OrdinalIgnoreCase))
                ?.Value
            ?? selectedModel.SupportedReasoningEfforts.FirstOrDefault()?.Value
            ?? selectedModel.DefaultReasoningEffort;

        string serviceTier = ResolveServiceTier(selectedModel, preferred?.ServiceTier);
        return new CodexExecutionSelection(
            selectedModel.Model,
            reasoning,
            serviceTier);
    }

    /// <summary>
    /// 對已不可用的保存模型尋找目錄通知的移行目標。
    /// </summary>
    /// <param name="catalog">目前可用模型目錄，不可為null。</param>
    /// <param name="preferredModel">前次保存的模型識別字；允許null或空白。</param>
    /// <returns>移行目標目前可用時回傳該模型，否則回傳null。</returns>
    /// <remarks>
    /// UI：由Resolve在frm_SqlOptimizer還原Codex選擇時間接使用。
    /// </remarks>
    private static CodexModelOption? ResolveUpgradeTarget(
        CodexModelCatalog catalog,
        string? preferredModel)
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：解析目錄內仍可查得來源模型時的直接移行資訊。
        if (string.IsNullOrWhiteSpace(preferredModel))
        {
            return null;
        }

        CodexModelOption? source = catalog.Models.FirstOrDefault(item =>
            string.Equals(item.Id, preferredModel, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(source?.UpgradeModel))
        {
            return null;
        }

        return catalog.Models.FirstOrDefault(item => string.Equals(
            item.Model,
            source.UpgradeModel,
            StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 從模型支援的服務層選取保存值，不可用時回到標準層。
    /// </summary>
    /// <param name="model">已選取的Codex模型，不可為null。</param>
    /// <param name="preferredServiceTier">前次保存的服務層；允許null或空白。</param>
    /// <returns>可用服務層識別字；附加層不可用時回傳default。</returns>
    /// <remarks>
    /// UI：由Resolve在frm_SqlOptimizer還原cmbCodexSpeed選擇時間接使用。
    /// </remarks>
    private static string ResolveServiceTier(
        CodexModelOption model,
        string? preferredServiceTier)
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：額外速度不可用時安全回到標準default。
        if (string.Equals(
                preferredServiceTier,
                "default",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(preferredServiceTier))
        {
            return "default";
        }

        return model.ServiceTiers.Any(item => string.Equals(
            item.Id,
            preferredServiceTier,
            StringComparison.OrdinalIgnoreCase))
            ? preferredServiceTier
            : "default";
    }
}
