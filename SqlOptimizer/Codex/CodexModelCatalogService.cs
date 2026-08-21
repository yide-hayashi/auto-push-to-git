using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Codex;

/// <summary>
/// 透過Codex app-server的model/list讀取目前帳號可使用的模型、推論量與速度層級。
/// </summary>
/// <remarks>
/// UI：由frm_SqlOptimizer的畫面顯示與btnRefreshCodexModels經SqlOptimizerUiController間接使用。
/// 本類別只管理Codex子程序與JSON協定，不直接操作任何UI控制項。
/// </remarks>
public sealed class CodexModelCatalogService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    private static readonly int MaximumResponseLineLength = 1024 * 1024;
    private static readonly int MaximumPages = 100;
    private readonly CodexCliLocator _locator;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// 建立使用既有Codex CLI定位器的模型目錄服務。
    /// </summary>
    /// <param name="locator">尋找Codex CLI執行檔的定位器，不可為null；本類別不會修改此物件。</param>
    /// <param name="timeout">一次完整目錄取得的上限時間；null時使用十秒，不可為零或負值。</param>
    /// <remarks>
    /// UI：由SqlOptimizerApplicationRuntime建立，供frm_SqlOptimizer的模型選擇區間接使用。
    /// 建構時不啟動程序、不連線網路，也不讀取正式環境資料庫。
    /// </remarks>
    public CodexModelCatalogService(
        CodexCliLocator locator,
        TimeSpan? timeout = null)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _timeout = timeout ?? DefaultTimeout;
        if (_timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                "Codex模型目錄逾時必須大於零。");
        }
    }

    /// <summary>
    /// 啟動隱藏的Codex app-server，完成初始化後分頁讀取可見模型目錄。
    /// </summary>
    /// <param name="codexExecutablePath">已診斷的Codex CLI完整路徑；null時由定位器尋找。</param>
    /// <param name="cancellationToken">取消模型目錄讀取的權杖；取消或逾時時會終止子程序。</param>
    /// <returns>包含可見模型與UTC取得時間的目錄；服務回傳空目錄時仍回傳有效物件。</returns>
    /// <exception cref="FileNotFoundException">找不到可使用的Codex CLI時拋出。</exception>
    /// <exception cref="InvalidOperationException">app-server無法啟動、協定錯誤或回傳失敗時拋出。</exception>
    /// <exception cref="OperationCanceledException">呼叫端取消或超過設定時間時拋出。</exception>
    /// <exception cref="TimeoutException">app-server未在設定時間內完成目錄取得時拋出。</exception>
    /// <remarks>
    /// UI：由frm_SqlOptimizer的Shown與btnRefreshCodexModels經SqlOptimizerUiController間接使用。
    /// 只讀取Codex帳號可見能力，不修改Codex全域設定與SQL最佳化工作階段。
    /// </remarks>
    public async Task<CodexModelCatalog> FetchAsync(
        string? codexExecutablePath = null,
        CancellationToken cancellationToken = default)
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：以Codex公開協定動態取得模型能力，避免固定清單隨版本過期。
        CodexCliDiscovery discovery = _locator.Locate(codexExecutablePath);
        if (!discovery.IsAvailable ||
            string.IsNullOrWhiteSpace(discovery.ExecutablePath))
        {
            throw new FileNotFoundException(
                discovery.Error ?? "Codex CLIが見つかりません。");
        }

        using CancellationTokenSource timeoutCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(_timeout);
        CancellationToken effectiveCancellation = timeoutCancellation.Token;

        using Process process = new()
        {
            StartInfo = CreateStartInfo(discovery.ExecutablePath),
            EnableRaisingEvents = true
        };

        bool started = false;
        Task<string>? standardErrorTask = null;
        try
        {
            try
            {
                started = process.Start();
            }
            catch (Exception exception) when (
                exception is InvalidOperationException or
                System.ComponentModel.Win32Exception)
            {
                throw new InvalidOperationException(
                    "Codex app-serverを開始できませんでした。",
                    exception);
            }

            if (!started)
            {
                throw new InvalidOperationException(
                    "Codex app-serverを開始できませんでした。");
            }

            standardErrorTask = ReadLimitedTextAsync(
                process.StandardError,
                effectiveCancellation);

            await WriteMessageAsync(
                    process.StandardInput,
                    new
                    {
                        id = 1,
                        method = "initialize",
                        @params = new
                        {
                            clientInfo = new
                            {
                                name = "auto-call-git-sql-optimizer",
                                version = GetClientVersion()
                            },
                            capabilities = (object?)null
                        }
                    },
                    effectiveCancellation)
                .ConfigureAwait(false);
            _ = await ReadMatchingResultAsync(
                    process.StandardOutput,
                    1,
                    effectiveCancellation)
                .ConfigureAwait(false);

            await WriteMessageAsync(
                    process.StandardInput,
                    new { method = "initialized" },
                    effectiveCancellation)
                .ConfigureAwait(false);

            List<CodexModelOption> models = [];
            string? cursor = null;
            int requestId = 2;
            for (int pageNumber = 0;
                 pageNumber < MaximumPages;
                 pageNumber++)
            {
                await WriteMessageAsync(
                        process.StandardInput,
                        new
                        {
                            id = requestId,
                            method = "model/list",
                            @params = new
                            {
                                cursor,
                                limit = 100,
                                includeHidden = false
                            }
                        },
                        effectiveCancellation)
                    .ConfigureAwait(false);

                JsonElement result = await ReadMatchingResultAsync(
                        process.StandardOutput,
                        requestId,
                        effectiveCancellation)
                    .ConfigureAwait(false);
                CodexModelCatalogPage page = ParseModelListResult(result);
                models.AddRange(page.Models);
                cursor = page.NextCursor;
                if (string.IsNullOrWhiteSpace(cursor))
                {
                    break;
                }

                requestId++;
            }

            if (!string.IsNullOrWhiteSpace(cursor))
            {
                throw new InvalidOperationException(
                    $"Codex model/listが{MaximumPages}頁を超えたため、安全のため取得を中止しました。");
            }

            IReadOnlyList<CodexModelOption> uniqueModels = models
                .GroupBy(item => item.Model, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
            return new CodexModelCatalog(
                uniqueModels,
                DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Codexモデル一覧の取得が{_timeout.TotalSeconds:0}秒でタイムアウトしました。",
                exception);
        }
        finally
        {
            if (started)
            {
                await TerminateProcessAsync(process).ConfigureAwait(false);
            }

            if (standardErrorTask is not null)
            {
                try
                {
                    _ = await standardErrorTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                    when (effectiveCancellation.IsCancellationRequested)
                {
                    // 逾時または呼叫端取消後のstderr読取終了は、元の取消結果を優先します。
                }
            }
        }
    }

    /// <summary>
    /// 從model/list的result元素解析單頁可見模型與下一頁Cursor。
    /// </summary>
    /// <param name="result">app-server回應的result元素，必須為JSON object。</param>
    /// <returns>保留未知推論量與服務層原始字串的單頁結果。</returns>
    /// <exception cref="InvalidOperationException">缺少data陣列或回應格式不正確時拋出。</exception>
    /// <remarks>
    /// UI：由FetchAsync間接建立frm_SqlOptimizer的模型選項。
    /// hidden模型不顯示於畫面，未知附加欄位安全忽略。
    /// </remarks>
    internal static CodexModelCatalogPage ParseModelListResult(JsonElement result)
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：不受新增欄位影響並嚴格擷取畫面需要的模型能力。
        if (result.ValueKind != JsonValueKind.Object ||
            !result.TryGetProperty("data", out JsonElement data) ||
            data.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "Codex model/list応答にdata配列がありません。");
        }

        List<CodexModelOption> models = [];
        foreach (JsonElement item in data.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                GetBoolean(item, "hidden"))
            {
                continue;
            }

            string? id = GetString(item, "id");
            string? model = GetString(item, "model");
            string? displayName = GetString(item, "displayName");
            string? defaultReasoningEffort =
                GetString(item, "defaultReasoningEffort");
            if (string.IsNullOrWhiteSpace(id) ||
                string.IsNullOrWhiteSpace(model) ||
                string.IsNullOrWhiteSpace(displayName) ||
                string.IsNullOrWhiteSpace(defaultReasoningEffort))
            {
                continue;
            }

            IReadOnlyList<CodexReasoningEffortOption> reasoningEfforts =
                ParseReasoningEfforts(item);
            IReadOnlyList<CodexServiceTierOption> serviceTiers =
                ParseServiceTiers(item);
            string? upgradeModel = GetString(item, "upgrade");
            string? migrationMessage = null;
            if (item.TryGetProperty("upgradeInfo", out JsonElement upgradeInfo) &&
                upgradeInfo.ValueKind == JsonValueKind.Object)
            {
                upgradeModel ??= GetString(upgradeInfo, "model");
                migrationMessage = GetString(upgradeInfo, "migrationMarkdown");
            }
            else if (upgradeInfo.ValueKind == JsonValueKind.String)
            {
                migrationMessage = upgradeInfo.GetString();
            }

            models.Add(new CodexModelOption(
                id,
                model,
                displayName,
                GetString(item, "description") ?? string.Empty,
                GetBoolean(item, "isDefault"),
                defaultReasoningEffort,
                reasoningEfforts,
                serviceTiers,
                GetString(item, "defaultServiceTier"),
                upgradeModel,
                migrationMessage));
        }

        return new CodexModelCatalogPage(
            models,
            GetString(result, "nextCursor"));
    }

    /// <summary>
    /// 建立不使用Shell、隱藏視窗並重導標準輸入輸出的Codex app-server啟動資訊。
    /// </summary>
    /// <param name="executablePath">已驗證的Codex CLI完整路徑，不可為空白。</param>
    /// <returns>僅供stdio app-server使用的ProcessStartInfo。</returns>
    /// <remarks>
    /// UI：由frm_SqlOptimizer模型清單更新經FetchAsync間接使用。
    /// </remarks>
    internal static ProcessStartInfo CreateStartInfo(string executablePath)
    {
        // 1150821 codex しずくちゃん_gpt-5.6-sol：以無BOM的UTF-8傳送JSONL，避免app-server無法解析第一筆initialize要求。
        ProcessStartInfo startInfo = new()
        {
            FileName = executablePath,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.ArgumentList.Add("app-server");
        startInfo.ArgumentList.Add("--listen");
        startInfo.ArgumentList.Add("stdio://");
        return startInfo;
    }

    /// <summary>
    /// 向app-server寫入一筆JSON訊息並立即Flush。
    /// </summary>
    /// <param name="writer">子程序標準輸入，不可為null。</param>
    /// <param name="message">要序列化為JSON的要求或通知，不可為null。</param>
    /// <param name="cancellationToken">在寫入前後偵測取消的權杖。</param>
    /// <returns>完成寫入與Flush的非同步工作。</returns>
    /// <remarks>
    /// UI：由frm_SqlOptimizer清單更新經FetchAsync間接使用。
    /// </remarks>
    private static async Task WriteMessageAsync(
        StreamWriter writer,
        object message,
        CancellationToken cancellationToken)
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：將每筆JSON要求獨立換行並立即送出。
        cancellationToken.ThrowIfCancellationRequested();
        string json = JsonSerializer.Serialize(message);
        await writer.WriteLineAsync(json.AsMemory(), cancellationToken)
            .ConfigureAwait(false);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 略過非同步通知，讀取指定ID的app-server回應。
    /// </summary>
    /// <param name="reader">子程序標準輸出，不可為null。</param>
    /// <param name="requestId">等待中的正整數要求ID。</param>
    /// <param name="cancellationToken">通知讀取取消或逾時的權杖。</param>
    /// <returns>複製後可獨立使用的回應result JsonElement。</returns>
    /// <exception cref="InvalidOperationException">回應不正確、回傳錯誤或程序提早結束時拋出。</exception>
    /// <remarks>
    /// UI：由frm_SqlOptimizer清單更新經FetchAsync間接使用。
    /// app-server非同步notification不顯示於畫面並安全略過。
    /// </remarks>
    private static async Task<JsonElement> ReadMatchingResultAsync(
        StreamReader reader,
        int requestId,
        CancellationToken cancellationToken)
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：按要求ID配對回應並忽略非同步通知。
        while (await reader.ReadLineAsync(cancellationToken)
                   .ConfigureAwait(false) is { } line)
        {
            if (line.Length > MaximumResponseLineLength)
            {
                throw new InvalidOperationException(
                    "Codex app-server応答が許可サイズを超えました。");
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    "Codex app-serverが不正なJSONを返しました。",
                    exception);
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                if (!root.TryGetProperty("id", out JsonElement id) ||
                    id.ValueKind != JsonValueKind.Number ||
                    !id.TryGetInt32(out int responseId) ||
                    responseId != requestId)
                {
                    continue;
                }

                if (root.TryGetProperty("error", out JsonElement error))
                {
                    string message = error.ValueKind == JsonValueKind.Object
                        ? GetString(error, "message") ?? "不明なapp-serverエラー"
                        : "不明なapp-serverエラー";
                    throw new InvalidOperationException(
                        $"Codex app-serverがエラーを返しました: {message}");
                }

                if (!root.TryGetProperty("result", out JsonElement result))
                {
                    throw new InvalidOperationException(
                        "Codex app-server応答にresultがありません。");
                }

                return result.Clone();
            }
        }

        throw new InvalidOperationException(
            "Codex app-serverが応答前に終了しました。");
    }

    /// <summary>
    /// 從單一模型解析其支援推論量清單。
    /// </summary>
    /// <param name="model">model/list內的單一模型元素，必須為JSON object。</param>
    /// <returns>去除重複後的推論量清單；沒有對應陣列時回傳空集合。</returns>
    /// <remarks>
    /// UI：由ParseModelListResult間接建立frm_SqlOptimizer的cmbCodexReasoning選項。
    /// </remarks>
    private static IReadOnlyList<CodexReasoningEffortOption> ParseReasoningEfforts(
        JsonElement model)
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：以字串保留CLI新增的未知推論量並去除重複。
        if (!model.TryGetProperty(
                "supportedReasoningEfforts",
                out JsonElement values) ||
            values.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<CodexReasoningEffortOption>();
        }

        List<CodexReasoningEffortOption> options = [];
        foreach (JsonElement value in values.EnumerateArray())
        {
            string? effort = GetString(value, "reasoningEffort");
            if (string.IsNullOrWhiteSpace(effort) ||
                options.Any(item => string.Equals(
                    item.Value,
                    effort,
                    StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            options.Add(new CodexReasoningEffortOption(
                effort,
                GetString(value, "description") ?? string.Empty));
        }

        return options;
    }

    /// <summary>
    /// 從單一模型解析標準層以外的服務層清單。
    /// </summary>
    /// <param name="model">model/list內的單一模型元素，必須為JSON object。</param>
    /// <returns>去除重複後的附加服務層清單；沒有對應陣列時回傳空集合。</returns>
    /// <remarks>
    /// UI：由ParseModelListResult間接建立frm_SqlOptimizer的cmbCodexSpeed選項。
    /// 畫面一定補入標準default，因此此處只回傳CLI實際通知的項目。
    /// </remarks>
    private static IReadOnlyList<CodexServiceTierOption> ParseServiceTiers(
        JsonElement model)
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：只解析CLI實際回傳的附加服務層並去除重複。
        if (!model.TryGetProperty("serviceTiers", out JsonElement values) ||
            values.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<CodexServiceTierOption>();
        }

        List<CodexServiceTierOption> options = [];
        foreach (JsonElement value in values.EnumerateArray())
        {
            string? id = GetString(value, "id");
            string? name = GetString(value, "name");
            if (string.IsNullOrWhiteSpace(id) ||
                string.IsNullOrWhiteSpace(name) ||
                options.Any(item => string.Equals(
                    item.Id,
                    id,
                    StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            options.Add(new CodexServiceTierOption(
                id,
                name,
                GetString(value, "description") ?? string.Empty));
        }

        return options;
    }

    /// <summary>
    /// 從JSON object安全取得字串屬性。
    /// </summary>
    /// <param name="element">搜尋目標JSON元素；允許非object。</param>
    /// <param name="propertyName">要搜尋的屬性名稱，不可為空白。</param>
    /// <returns>字串值；不存在、為null或不是字串時回傳null。</returns>
    /// <remarks>UI：無；僅供model/list最小解析處理使用。</remarks>
    private static string? GetString(
        JsonElement element,
        string propertyName)
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：以型別檢查避免不正確JSON值被當成識別字。
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out JsonElement value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    /// <summary>
    /// 從JSON object安全取得Boolean屬性。
    /// </summary>
    /// <param name="element">搜尋目標JSON元素；允許非object。</param>
    /// <param name="propertyName">要搜尋的屬性名稱，不可為空白。</param>
    /// <returns>值為JSON true時回傳true，其他情況回傳false。</returns>
    /// <remarks>UI：無；僅供model/list最小解析處理使用。</remarks>
    private static bool GetBoolean(
        JsonElement element,
        string propertyName)
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：僅將JSON true視為啟用旗標。
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out JsonElement value) &&
               value.ValueKind == JsonValueKind.True;
    }

    /// <summary>
    /// 取得通知app-server的Client版本。
    /// </summary>
    /// <returns>目前Assembly版本；無法取得時回傳0.0.0。</returns>
    /// <remarks>UI：無；僅供Codex app-server初始化要求使用。</remarks>
    private static string GetClientVersion()
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：向app-server提供目前Assembly版本並保留安全fallback。
        return typeof(CodexModelCatalogService).Assembly
                   .GetName()
                   .Version
                   ?.ToString() ?? "0.0.0";
    }

    /// <summary>
    /// 以長度上限讀取stderr，避免子程序輸出阻塞。
    /// </summary>
    /// <param name="reader">子程序標準錯誤輸出，不可為null。</param>
    /// <param name="cancellationToken">通知讀取取消或逾時的權杖。</param>
    /// <returns>最多64KiB的標準錯誤文字。</returns>
    /// <remarks>UI：無；僅供FetchAsync管理子程序使用。</remarks>
    private static async Task<string> ReadLimitedTextAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：限制stderr大小並持續排空以避免子程序阻塞。
        char[] buffer = new char[4096];
        StringBuilder result = new();
        while (result.Length < 64 * 1024)
        {
            int read = await reader.ReadAsync(
                    buffer.AsMemory(
                        0,
                        Math.Min(buffer.Length, 64 * 1024 - result.Length)),
                    cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            result.Append(buffer, 0, read);
        }

        return result.ToString();
    }

    /// <summary>
    /// 終止model/list專用app-server並等待程序結束，避免子程序殘留。
    /// </summary>
    /// <param name="process">要終止且已啟動的Process，不可為null。</param>
    /// <returns>等待程序結束的非同步工作。</returns>
    /// <remarks>UI：無；供FetchAsync成功、失敗與取消的所有路徑使用。</remarks>
    private static async Task TerminateProcessAsync(Process process)
    {
        // 1150820 codex しずくちゃん_gpt-5.6-sol：所有取得路徑都終止專用app-server並等待程序退出。
        if (!process.HasExited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
                // HasExited確認直後に自然終了した競合は、終了済み状態として扱います。
            }
        }

        await process.WaitForExitAsync(CancellationToken.None)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// 保存app-server model/list單頁內容及下一頁Cursor。
/// </summary>
/// <param name="Models">單頁可見模型清單，不可為null；允許空集合。</param>
/// <param name="NextCursor">傳給下一頁要求的不透明Cursor；最後一頁為null。</param>
internal sealed record CodexModelCatalogPage(
    IReadOnlyList<CodexModelOption> Models,
    string? NextCursor);
