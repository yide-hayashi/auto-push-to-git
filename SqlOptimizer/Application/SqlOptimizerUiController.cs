using System.Data;
using System.Text;
using Project_auto_push_to_gitlab.SqlOptimizer.Codex;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;
using Project_auto_push_to_gitlab.SqlOptimizer.Mcp;
using Project_auto_push_to_gitlab.SqlOptimizer.Oracle;
using Project_auto_push_to_gitlab.SqlOptimizer.Persistence;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Application
{
    /// <summary>
    /// 協調SQL最佳化WinForms畫面、工作階段、Oracle檢查、MCP、Codex程序與動態模型設定。
    /// </summary>
    public sealed class SqlOptimizerUiController : IDisposable
    {
        private readonly frm_SqlOptimizer _view;
        private readonly IOptimizationSessionService _sessionService;
        private readonly IOptimizationSessionStore _store;
        private readonly SqlOptimizerMcpHost _mcpHost;
        private readonly CodexCliRunner _cliRunner;
        private readonly CodexCliDiagnosticsService _diagnostics;
        private readonly CodexModelCatalogService _modelCatalogService;
        private readonly CodexExecutionSettingsStore _codexExecutionSettingsStore;
        private readonly CodexMcpConfigurationService _configuration;
        private readonly IOracleOptimizerReadinessProbe _oracleReadiness;
        private readonly string _codexWorkingDirectory;
        private readonly string? _mcpStartupError;
        private readonly System.Windows.Forms.Timer _refreshTimer;
        private readonly SemaphoreSlim _refreshGate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _lifetimeCancellation = new();

        private CancellationTokenSource? _runCancellation;
        private CodexModelCatalog? _codexModelCatalog;
        private CodexModelCatalog? _savedCodexModelCatalog;
        private CodexExecutionSelection _preferredCodexSelection =
            CodexExecutionSelection.InheritDefaults;
        private bool _catalogRefreshRunning;
        private Guid? _sessionId;
        private SqlOptimizationSessionStatus? _lastObservedStatus;
        private bool _disposed;

        /// <summary>
        /// 建立SQL最佳化畫面Controller並連結畫面事件、輪詢Timer與Codex模型設定服務。
        /// </summary>
        /// <param name="view">接收操作與顯示狀態的frm_SqlOptimizer，不可為null。</param>
        /// <param name="sessionService">管理核准與最佳化生命週期的服務，不可為null。</param>
        /// <param name="store">讀取已保存工作階段的Store，不可為null。</param>
        /// <param name="mcpHost">提供本機受保護工具端點的MCP主機，不可為null。</param>
        /// <param name="cliRunner">啟動單次Codex子程序的Runner，不可為null。</param>
        /// <param name="diagnostics">檢查Codex CLI版本與登入狀態的服務，不可為null。</param>
        /// <param name="modelCatalogService">動態讀取model/list的服務，不可為null。</param>
        /// <param name="codexExecutionSettingsStore">保存模型目錄與前次選擇的Store，不可為null。</param>
        /// <param name="configuration">檢查及套用Codex MCP設定的服務，不可為null。</param>
        /// <param name="oracleReadiness">以唯讀方式檢查Oracle執行計畫能力的Probe，不可為null。</param>
        /// <param name="codexWorkingDirectory">Codex隔離工作目錄完整路徑，不可為空白。</param>
        /// <param name="mcpStartupError">MCP啟動失敗的安全訊息；啟動成功時為null。</param>
        /// <remarks>
        /// UI：由SqlOptimizerApplicationRuntime建立並直接控制frm_SqlOptimizer所有SQL最佳化控制項。
        /// 建構時不連線Oracle、不啟動Codex，只啟動畫面狀態輪詢Timer。
        /// </remarks>
        public SqlOptimizerUiController(
            frm_SqlOptimizer view,
            IOptimizationSessionService sessionService,
            IOptimizationSessionStore store,
            SqlOptimizerMcpHost mcpHost,
            CodexCliRunner cliRunner,
            CodexCliDiagnosticsService diagnostics,
            CodexModelCatalogService modelCatalogService,
            CodexExecutionSettingsStore codexExecutionSettingsStore,
            CodexMcpConfigurationService configuration,
            IOracleOptimizerReadinessProbe oracleReadiness,
            string codexWorkingDirectory,
            string? mcpStartupError)
        {
            // 1150820 codex しずくちゃん_gpt-5.6-sol：將目錄取得與選擇保存接入既有Controller，避免Form承擔程序或檔案責任。
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _sessionService = sessionService
                ?? throw new ArgumentNullException(nameof(sessionService));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _mcpHost = mcpHost ?? throw new ArgumentNullException(nameof(mcpHost));
            _cliRunner = cliRunner ?? throw new ArgumentNullException(nameof(cliRunner));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            _modelCatalogService = modelCatalogService
                ?? throw new ArgumentNullException(nameof(modelCatalogService));
            _codexExecutionSettingsStore = codexExecutionSettingsStore
                ?? throw new ArgumentNullException(nameof(codexExecutionSettingsStore));
            _configuration = configuration
                ?? throw new ArgumentNullException(nameof(configuration));
            _oracleReadiness = oracleReadiness
                ?? throw new ArgumentNullException(nameof(oracleReadiness));
            _codexWorkingDirectory = codexWorkingDirectory;
            _mcpStartupError = mcpStartupError;

            _view.ApproveRequested += OnApproveRequested;
            _view.StartCodexRequested += OnStartCodexRequested;
            _view.RefreshCodexModelsRequested += OnRefreshCodexModelsRequested;
            _view.CodexExecutionSelectionChanged +=
                OnCodexExecutionSelectionChanged;
            _view.ConfigureMcpRequested += OnConfigureMcpRequested;
            _view.StopRequested += OnStopRequested;
            _view.AcceptRequested += OnAcceptRequested;
            _view.ContinueRequested += OnContinueRequested;
            _view.ApprovalInvalidated += OnApprovalInvalidated;
            _view.SavedSessionSelected += OnSavedSessionSelected;
            _view.Shown += OnViewShown;
            _view.FormClosed += OnViewClosed;

            _refreshTimer = new System.Windows.Forms.Timer
            {
                Interval = 1500
            };
            _refreshTimer.Tick += OnRefreshTimerTick;
            _refreshTimer.Start();
        }

        /// <summary>
        /// 停止輪詢與Codex程序，取消模型目錄取得並釋放Controller擁有的資源。
        /// </summary>
        /// <remarks>
        /// UI：由frm_SqlOptimizer的FormClosed直接使用；不處置Runtime擁有的共用服務。
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // 1150820 codex しずくちゃん_gpt-5.6-sol：關閉Form時一併取消背景模型目錄取得，避免更新已釋放控制項。
            _disposed = true;
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _lifetimeCancellation.Cancel();
            CancelRun();
            _runCancellation?.Dispose();
            _runCancellation = null;
            _lifetimeCancellation.Dispose();
        }

        /// <summary>
        /// 畫面首次顯示時先套用保存目錄，再並行取得最新Codex目錄、工作階段履歷與CLI診斷。
        /// </summary>
        /// <param name="sender">Shown事件來源frm_SqlOptimizer。</param>
        /// <param name="e">不含額外資料的事件引數。</param>
        /// <remarks>
        /// UI：frm_SqlOptimizer.Shown直接使用；更新Codex設定列、履歷選單與MCP狀態。
        /// </remarks>
        private async void OnViewShown(object? sender, EventArgs e)
        {
            // 1150820 codex しずくちゃん_gpt-5.6-sol：先顯示保存目錄，再與其他起動診斷並行更新最新模型能力。
            try
            {
                UpdateMcpStatus();
                await LoadCodexExecutionSettingsAsync(
                        _lifetimeCancellation.Token)
                    .ConfigureAwait(true);
                Task catalogRefresh = RefreshCodexModelCatalogAsync(
                    isManualRefresh: false,
                    _lifetimeCancellation.Token);
                await LoadAvailableHistoryAsync().ConfigureAwait(true);
                await ShowCodexDiagnosticsAsync().ConfigureAwait(true);
                await catalogRefresh.ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (
                _lifetimeCancellation.IsCancellationRequested)
            {
                // Form關閉時不再更新已Dispose的控制項，並保留正常關閉語意。
            }
        }

        /// <summary>
        /// 從應用程式保存檔立即還原前次Codex模型目錄與選擇，讓畫面不必等待CLI回應。
        /// </summary>
        /// <param name="cancellationToken">Form關閉時取消讀取的權杖。</param>
        /// <returns>保存內容已套用至frm_SqlOptimizer後完成的非同步工作。</returns>
        /// <remarks>
        /// UI：由frm_SqlOptimizer的Shown間接使用；更新cmbCodexModel、cmbCodexReasoning、cmbCodexSpeed與lblCodexCatalogStatus。
        /// 保存檔不存在或損壞時只顯示替代狀態，不會阻止btnStartCodex。
        /// </remarks>
        private async Task LoadCodexExecutionSettingsAsync(
            CancellationToken cancellationToken)
        {
            // 1150820 codex しずくちゃん_gpt-5.6-sol：先呈現本機快取，再由背景CLI取得結果取代，避免畫面啟動被實驗性協定阻塞。
            CodexExecutionSettingsLoadResult loadResult =
                await _codexExecutionSettingsStore.LoadAsync(cancellationToken)
                    .ConfigureAwait(true);
            CodexExecutionSettingsSnapshot? snapshot = loadResult.Snapshot;
            _preferredCodexSelection = snapshot?.Selection
                ?? CodexExecutionSelection.InheritDefaults;
            _savedCodexModelCatalog = snapshot?.Catalog;
            _codexModelCatalog = snapshot?.Catalog;

            if (_codexModelCatalog is { Models.Count: > 0 } cachedCatalog)
            {
                CodexExecutionSelection selection =
                    CodexExecutionSelectionResolver.Resolve(
                        cachedCatalog,
                        _preferredCodexSelection,
                        out string? warning);
                _preferredCodexSelection = selection;
                _view.SetCodexModelCatalog(
                    cachedCatalog,
                    selection,
                    CombineStatus(
                        "保存済み一覧を表示しています。最新版を確認中です。",
                        warning,
                        loadResult.Warning));
                return;
            }

            _view.SetCodexModelCatalog(
                null,
                CodexExecutionSelection.InheritDefaults,
                CombineStatus(
                    "一覧取得まではCodex全体設定を継承します。",
                    loadResult.Warning));
        }

        /// <summary>
        /// 從Codex app-server更新模型目錄，並將畫面同步至目前可用的選擇組合。
        /// </summary>
        /// <param name="isManualRefresh">由btnRefreshCodexModels明確更新時為true，Shown自動更新時為false。</param>
        /// <param name="cancellationToken">Form關閉時停止CLI子程序的取消權杖。</param>
        /// <returns>完成最新目錄套用或安全fallback顯示的非同步工作。</returns>
        /// <remarks>
        /// UI：由frm_SqlOptimizer的Shown或btnRefreshCodexModels直接使用；更新三個ComboBox與狀態Label。
        /// 取得失敗時維持保存目錄，沒有保存目錄時繼承Codex全域設定。
        /// </remarks>
        private async Task RefreshCodexModelCatalogAsync(
            bool isManualRefresh,
            CancellationToken cancellationToken)
        {
            if (_catalogRefreshRunning || _disposed)
            {
                return;
            }

            // 1150820 codex しずくちゃん_gpt-5.6-sol：序列化目錄更新並在任何失敗下維持可啟動的fallback狀態。
            _catalogRefreshRunning = true;
            _view.SetCodexCatalogLoading(
                true,
                isManualRefresh
                    ? "Codexモデル一覧を更新しています…"
                    : "Codexモデル一覧を確認しています…");
            try
            {
                CodexModelCatalog liveCatalog =
                    await _modelCatalogService.FetchAsync(
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(true);
                CodexExecutionSelection requestedSelection =
                    ResolveAdvertisedUpgrade(
                        liveCatalog,
                        _codexModelCatalog ?? _savedCodexModelCatalog,
                        _preferredCodexSelection,
                        out string? migrationWarning);
                CodexExecutionSelection resolvedSelection =
                    CodexExecutionSelectionResolver.Resolve(
                        liveCatalog,
                        requestedSelection,
                        out string? resolverWarning);

                _codexModelCatalog = liveCatalog;
                _savedCodexModelCatalog = liveCatalog;
                _preferredCodexSelection = resolvedSelection;
                string status = CombineStatus(
                    $"Codex CLIから{liveCatalog.Models.Count}件を取得しました。",
                    migrationWarning,
                    resolverWarning);
                _view.SetCodexModelCatalog(
                    liveCatalog,
                    resolvedSelection,
                    status);
                try
                {
                    await SaveCodexExecutionSettingsAsync(
                            resolvedSelection,
                            cancellationToken)
                        .ConfigureAwait(true);
                }
                catch (SqlOptimizerPersistenceException exception)
                {
                    _view.SetCodexCatalogStatus(
                        CombineStatus(
                            status,
                            $"一覧は使用できますが保存できませんでした: {ToSafeMessage(exception)}"));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                string fallbackStatus = _codexModelCatalog is { Models.Count: > 0 }
                    ? "一覧更新に失敗したため、保存済み一覧を使用します。"
                    : "一覧更新に失敗したため、Codex全体設定を継承します。";
                _view.SetCodexCatalogStatus(
                    CombineStatus(fallbackStatus, ToSafeMessage(exception)));
            }
            finally
            {
                _catalogRefreshRunning = false;
                if (!_disposed)
                {
                    _view.SetCodexCatalogLoading(false);
                }
            }
        }

        /// <summary>
        /// 使用者按下清單更新Button時開始重新取得Codex目錄。
        /// </summary>
        /// <param name="sender">事件發生元；通常はfrm_SqlOptimizer。</param>
        /// <param name="e">不包含附加資料的事件引數。</param>
        /// <remarks>
        /// UI：由frm_SqlOptimizer的btnRefreshCodexModels直接使用，不變更SQL承認狀態。
        /// </remarks>
        private async void OnRefreshCodexModelsRequested(
            object? sender,
            EventArgs e)
        {
            // 1150820 codex しずくちゃん_gpt-5.6-sol：將使用者明確更新操作委派給共用目錄取得流程。
            await RefreshCodexModelCatalogAsync(
                    isManualRefresh: true,
                    _lifetimeCancellation.Token)
                .ConfigureAwait(true);
        }

        /// <summary>
        /// 記憶使用者的Codex模型、推論量或速度選擇，並寫入下次啟動用保存檔。
        /// </summary>
        /// <param name="sender">事件發生元；通常はfrm_SqlOptimizer。</param>
        /// <param name="e">不包含附加資料的事件引數。</param>
        /// <remarks>
        /// UI：由cmbCodexModel、cmbCodexReasoning、cmbCodexSpeed的選擇確定事件直接使用。
        /// 不變更SQL內容或既有承認，保存失敗時也不阻止Codex開始。
        /// </remarks>
        private async void OnCodexExecutionSelectionChanged(
            object? sender,
            EventArgs e)
        {
            // 1150820 codex しずくちゃん_gpt-5.6-sol：保存設定變更但不改動既有SQL承認與工作階段。
            _preferredCodexSelection =
                _view.SelectedCodexExecutionSelection;
            try
            {
                await SaveCodexExecutionSettingsAsync(
                        _preferredCodexSelection,
                        _lifetimeCancellation.Token)
                    .ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (
                _lifetimeCancellation.IsCancellationRequested)
            {
                // Form關閉時的預期取消不顯示錯誤，避免已Dispose的畫面被再次更新。
            }
            catch (Exception exception)
            {
                _view.SetCodexCatalogStatus(
                    $"選択は使用できますが保存できませんでした: {ToSafeMessage(exception)}");
            }
        }

        /// <summary>
        /// 不含秘密資訊地保存目前Codex選擇與最後一次成功取得的目錄。
        /// </summary>
        /// <param name="selection">要保存的模型、推論量與服務層，不可為null。</param>
        /// <param name="cancellationToken">Form關閉時取消保存的權杖。</param>
        /// <returns>完成原子保存的非同步工作。</returns>
        /// <remarks>
        /// UI：由frm_SqlOptimizer的Codex設定變更與清單更新間接使用。
        /// SQL、Bind値、MCP Token、認証情報は保存しない。
        /// </remarks>
        private Task SaveCodexExecutionSettingsAsync(
            CodexExecutionSelection selection,
            CancellationToken cancellationToken)
        {
            // 1150820 codex しずくちゃん_gpt-5.6-sol：以同一Snapshot保存選擇與最後成功目錄。
            return _codexExecutionSettingsStore.SaveAsync(
                new CodexExecutionSettingsSnapshot(
                    selection,
                    _codexModelCatalog),
                cancellationToken);
        }

        /// <summary>
        /// 若保存目錄通知的廢止模型移行目標仍在最新目錄可用，則將選擇切換至該目標。
        /// </summary>
        /// <param name="liveCatalog">本次取得的可用模型目錄，不可為null。</param>
        /// <param name="previousCatalog">保存或前一次顯示的目錄；允許null。</param>
        /// <param name="preferred">使用者前次選擇，不可為null。</param>
        /// <param name="warning">完成移行時的使用者通知；不需移行時為null。</param>
        /// <returns>需要時只將模型替換為移行目標的選擇。</returns>
        /// <remarks>
        /// UI：由frm_SqlOptimizer的Shown與btnRefreshCodexModels間接使用，決定cmbCodexModel還原目標。
        /// </remarks>
        internal static CodexExecutionSelection ResolveAdvertisedUpgrade(
            CodexModelCatalog liveCatalog,
            CodexModelCatalog? previousCatalog,
            CodexExecutionSelection preferred,
            out string? warning)
        {
            // 1150820 codex しずくちゃん_gpt-5.6-sol：僅採用保存目錄已通知且最新目錄仍可用的移行目標。
            warning = null;
            if (string.IsNullOrWhiteSpace(preferred.Model) ||
                liveCatalog.Models.Any(item => string.Equals(
                    item.Model,
                    preferred.Model,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return preferred;
            }

            CodexModelOption? previousModel = previousCatalog?.Models
                .FirstOrDefault(item => string.Equals(
                    item.Model,
                    preferred.Model,
                    StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(previousModel?.UpgradeModel) ||
                !liveCatalog.Models.Any(item => string.Equals(
                    item.Model,
                    previousModel.UpgradeModel,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return preferred;
            }

            warning = $"{preferred.Model} は利用できないため、通知された移行先 {previousModel.UpgradeModel} へ切り替えました。";
            return preferred with { Model = previousModel.UpgradeModel };
        }

        /// <summary>
        /// 排除空白狀態文字並連結為畫面用單行訊息。
        /// </summary>
        /// <param name="messages">依序連結的狀態文字；允許null元素與空白。</param>
        /// <returns>以半形空白連結的有效文字；全部空白時回傳空字串。</returns>
        /// <remarks>UI：只供組合frm_SqlOptimizer的lblCodexCatalogStatus顯示使用。</remarks>
        private static string CombineStatus(params string?[] messages)
        {
            // 1150820 codex しずくちゃん_gpt-5.6-sol：將多個取得與移行狀態整理為單行安全訊息。
            return string.Join(
                " ",
                messages.Where(message => !string.IsNullOrWhiteSpace(message)));
        }

        private void OnViewClosed(object? sender, FormClosedEventArgs e)
        {
            Dispose();
        }

        private async void OnApproveRequested(object? sender, EventArgs e)
        {
            try
            {
                if (!_view.TryCaptureInput(
                        out SqlOptimizerFormInput? input,
                        out string validationMessage) ||
                    input is null)
                {
                    throw new SqlValidationException(validationMessage);
                }

                IReadOnlyList<SqlBindParameter> binds =
                    ConvertBindParameters(input.Bindings);
                SqlOptimizationSession session =
                    await _sessionService.CreateDraftAsync(
                            new CreateOptimizationSessionRequest(
                                input.HospitalArea,
                                input.Sql,
                                ParseObjective(input.OptimizationGoal),
                                new SqlOptimizationLimits(
                                    input.CommandTimeoutSeconds,
                                    input.PreviewRowLimit,
                                    input.MaxCandidatesPerRound)))
                        .ConfigureAwait(true);

                session = await _sessionService.ApproveAsync(
                        session.SessionId,
                        binds)
                    .ConfigureAwait(true);
                _sessionId = session.SessionId;
                _lastObservedStatus = null;
                _view.SetSessionId(session.SessionId.ToString("D"));
                _view.SetIterationHistory(Array.Empty<SqlOptimizerIterationDisplay>());
                _view.SetUiState(
                    SqlOptimizerFormState.Approved,
                    "承認範囲を固定しました。Codexまたは外部Agentを開始できます。");
                _view.SetActivity(
                    $"承認済み: {session.ConnectionAlias} / 候補上限 {session.Limits.MaxCandidatesPerRound}");
                await RefreshHistoryChoicesAsync(session.SessionId)
                    .ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                _view.ShowOperationError(ToSafeMessage(exception));
            }
        }

        /// <summary>
        /// 驗證核准、MCP、Codex登入及Oracle能力後，以畫面選定的模型設定啟動單次隔離Agent。
        /// </summary>
        /// <param name="sender">btnStartCodex事件來源frm_SqlOptimizer。</param>
        /// <param name="e">不含額外資料的事件引數。</param>
        /// <remarks>
        /// UI：frm_SqlOptimizer的btnStartCodex直接使用；執行期間停用Codex模型設定但保留原SQL核准工作階段。
        /// </remarks>
        private async void OnStartCodexRequested(object? sender, EventArgs e)
        {
            if (!_sessionId.HasValue)
            {
                _view.ShowOperationError("先にSQLを承認してください。");
                return;
            }

            if (!_mcpHost.IsRunning)
            {
                _view.ShowOperationError(
                    _mcpStartupError ??
                    "MCPサーバーが起動していません。ポート57321を確認してください。");
                return;
            }

            CancelRun();
            _runCancellation?.Dispose();
            CancellationTokenSource runCancellation = new();
            _runCancellation = runCancellation;
            bool codexStarted = false;

            try
            {
                if (!await _mcpHost.CheckHealthAsync(runCancellation.Token)
                        .ConfigureAwait(true))
                {
                    throw new InvalidOperationException(
                        "MCPサーバーへ認証付き接続できません。ポート57321とToken状態を確認してください。");
                }

                CodexCliDiagnostics diagnostics =
                    await _diagnostics.CheckAsync(
                            cancellationToken: runCancellation.Token)
                        .ConfigureAwait(true);
                if (!diagnostics.IsAvailable)
                {
                    throw new InvalidOperationException(
                        diagnostics.Error ?? "Codex CLIが見つかりません。");
                }

                if (!diagnostics.IsAuthenticated)
                {
                    throw new InvalidOperationException(
                        diagnostics.Error ??
                        "Codex CLIにログインしていません。`codex login` を実行してください。");
                }

                Guid sessionId = _sessionId.Value;
                SqlOptimizationSession? session =
                    await _sessionService.GetAsync(
                            sessionId,
                            runCancellation.Token)
                        .ConfigureAwait(true);
                if (session?.Approval is null)
                {
                    throw new InvalidOperationException(
                        "承認が有効ではありません。バインド値を再入力して承認してください。");
                }

                _view.SetActivity("Oracle接続と実行計画権限を確認しています。");
                OracleOptimizerReadinessResult readiness =
                    await _oracleReadiness.CheckAsync(
                            session.ConnectionAlias,
                            session.Limits.CommandTimeoutSeconds,
                            runCancellation.Token)
                        .ConfigureAwait(true);
                if (!readiness.IsReady)
                {
                    throw new InvalidOperationException(readiness.Message);
                }

                _view.SetUiState(
                    SqlOptimizerFormState.Running,
                    "Codex Agentを開始しています。");
                // 1150820 codex しずくちゃん_gpt-5.6-sol：將開始時畫面選擇固定於單次子程序，避免執行中能力目錄變更影響本次工作。
                CodexExecutionSelection executionSelection =
                    _view.SelectedCodexExecutionSelection;
                _preferredCodexSelection = executionSelection;
                _view.SetActivity(
                    $"{readiness.Message} Codex開始: {diagnostics.Version ?? "version unknown"} / {executionSelection.ToDisplayText()}");

                Progress<CodexJsonlEvent> progress =
                    new Progress<CodexJsonlEvent>(ReportCodexEvent);
                codexStarted = true;
                CodexRunResult result = await _cliRunner.RunAsync(
                        new CodexRunRequest(
                            sessionId.ToString("D"),
                            _mcpHost.Endpoint,
                            _codexWorkingDirectory,
                            diagnostics.ExecutablePath,
                            executionSelection),
                        progress,
                        runCancellation.Token)
                    .ConfigureAwait(true);

                await RefreshSessionAsync(forceActivity: true)
                    .ConfigureAwait(true);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(result.StandardError)
                            ? $"Codex CLIは終了コード {result.ExitCode} を返しました。"
                            : result.StandardError);
                }

                _view.SetActivity(
                    result.FinalMessage is { Length: > 0 }
                        ? $"Codex完了: {Truncate(result.FinalMessage, 1000)}"
                        : "Codex処理が完了しました。");
            }
            catch (OperationCanceledException)
            {
                _view.SetActivity("Codex処理を停止しました。");
            }
            catch (Exception exception)
            {
                if (codexStarted && _sessionId.HasValue)
                {
                    try
                    {
                        await _sessionService.StopAsync(_sessionId.Value)
                            .ConfigureAwait(true);
                    }
                    catch
                    {
                    }
                }

                _view.ShowOperationError(ToSafeMessage(exception));
            }
            finally
            {
                if (ReferenceEquals(_runCancellation, runCancellation))
                {
                    _runCancellation = null;
                }

                runCancellation.Dispose();
                await RefreshSessionAsync().ConfigureAwait(true);
            }
        }

        private async void OnConfigureMcpRequested(object? sender, EventArgs e)
        {
            try
            {
                CodexMcpRegistrationState state =
                    await _configuration.InspectAsync(_mcpHost.Endpoint)
                        .ConfigureAwait(true);
                string current = !state.CodexAvailable
                    ? state.Error ?? "Codex CLIが見つかりません。"
                    : state.MatchesExpectedConfiguration
                        ? "現在のCodex MCP設定はこのアプリと一致しています。"
                        : state.IsRegistered
                            ? $"同名設定があります: {state.Endpoint ?? "(URL不明)"}"
                            : "CodexにこのMCPは未登録です。";

                if (state.MatchesExpectedConfiguration)
                {
                    MessageBox.Show(
                        _view,
                        current + Environment.NewLine +
                        "外部VS Code/CLIでは、起動前にBearer Token環境変数を安全に注入する必要があります。",
                        "Codex MCP設定",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                DialogResult confirmation = MessageBox.Show(
                    _view,
                    current + Environment.NewLine + Environment.NewLine +
                    "適用予定:" + Environment.NewLine +
                    state.CommandPreview + Environment.NewLine + Environment.NewLine +
                    "設定を適用しますか？" + Environment.NewLine +
                    "Token値は設定ファイルへ保存しません。",
                    "Codex MCP設定の確認",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (confirmation != DialogResult.Yes)
                {
                    _view.SetActivity("MCP設定の変更をキャンセルしました。");
                    return;
                }

                CodexMcpRegistrationResult result =
                    await _configuration.ApplyAsync(
                            _mcpHost.Endpoint,
                            replaceExisting: state.IsRegistered)
                        .ConfigureAwait(true);
                if (result.Outcome is CodexMcpRegistrationOutcome.Failed
                    or CodexMcpRegistrationOutcome.Conflict)
                {
                    throw new InvalidOperationException(
                        result.Error ?? "Codex MCP設定を適用できませんでした。");
                }

                _view.SetActivity("Codex MCP設定を適用しました。");
                MessageBox.Show(
                    _view,
                    "MCP設定を適用しました。" + Environment.NewLine +
                    "VS Codeを再起動してください。" + Environment.NewLine +
                    "外部起動ではBearer Token環境変数の安全な受け渡しが別途必要です。",
                    "Codex MCP設定",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception exception)
            {
                _view.ShowOperationError(ToSafeMessage(exception));
            }
        }

        private async void OnStopRequested(object? sender, EventArgs e)
        {
            CancelRun();
            if (!_sessionId.HasValue)
            {
                _view.SetUiState(SqlOptimizerFormState.Stopped, "停止しました。");
                return;
            }

            try
            {
                _view.SetActivity("停止要求を送信しました。Oracle処理をキャンセルします。");
                SqlOptimizationSession session =
                    await _sessionService.StopAsync(_sessionId.Value)
                        .ConfigureAwait(true);
                RenderSession(session, forceActivity: true);
                await RefreshHistoryChoicesAsync(session.SessionId)
                    .ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                _view.ShowOperationError(ToSafeMessage(exception));
            }
        }

        private async void OnAcceptRequested(object? sender, EventArgs e)
        {
            if (!_sessionId.HasValue ||
                !Guid.TryParse(_view.SelectedIterationId, out Guid iterationId))
            {
                _view.ShowOperationError("採用する最終候補を選択してください。");
                return;
            }

            try
            {
                SqlOptimizationSession session =
                    await _sessionService.AcceptAsync(
                            _sessionId.Value,
                            iterationId)
                        .ConfigureAwait(true);
                RenderSession(session, forceActivity: true);
                await RefreshHistoryChoicesAsync(session.SessionId)
                    .ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                _view.ShowOperationError(ToSafeMessage(exception));
            }
        }

        private async void OnContinueRequested(object? sender, EventArgs e)
        {
            if (!_sessionId.HasValue ||
                !Guid.TryParse(_view.SelectedIterationId, out Guid iterationId))
            {
                _view.ShowOperationError("次ラウンドの基準にする候補を選択してください。");
                return;
            }

            try
            {
                SqlOptimizationSession session =
                    await _sessionService.ContinueAsync(
                            _sessionId.Value,
                            iterationId,
                            _view.FeedbackText)
                        .ConfigureAwait(true);
                RenderSession(session, forceActivity: true);
                _view.SetActivity(
                    "同じ承認範囲で次ラウンドを開始できます。再承認は不要です。");
            }
            catch (Exception exception)
            {
                _view.ShowOperationError(ToSafeMessage(exception));
            }
        }

        private async void OnApprovalInvalidated(object? sender, EventArgs e)
        {
            Guid? invalidatedSession = _sessionId;
            _sessionId = null;
            _lastObservedStatus = null;
            CancelRun();

            if (!invalidatedSession.HasValue)
            {
                return;
            }

            try
            {
                await _sessionService.StopAsync(invalidatedSession.Value)
                    .ConfigureAwait(true);
                _view.SetActivity(
                    "入力変更により以前の承認を失効し、そのセッションを停止しました。");
            }
            catch (Exception exception)
            {
                _view.SetActivity(
                    $"以前のセッション停止を確認できませんでした: {ToSafeMessage(exception)}");
            }
        }

        private async void OnRefreshTimerTick(object? sender, EventArgs e)
        {
            await RefreshSessionAsync().ConfigureAwait(true);
        }

        private async void OnSavedSessionSelected(object? sender, EventArgs e)
        {
            if (!Guid.TryParse(
                    _view.SelectedSavedSessionId,
                    out Guid selectedSessionId) ||
                selectedSessionId == _sessionId)
            {
                return;
            }

            try
            {
                SqlOptimizationSession? session =
                    await _store.LoadAsync(selectedSessionId)
                        .ConfigureAwait(true);
                if (session is null)
                {
                    throw new OptimizationSessionNotFoundException(
                        selectedSessionId);
                }

                _sessionId = session.SessionId;
                _lastObservedStatus = null;
                LoadSessionInput(session);
                RenderSession(session, forceActivity: false);
                _view.SetActivity(
                    "保存履歴を切り替えました。バインド値と結果行は保存されていません。");
            }
            catch (Exception exception)
            {
                _view.SetActivity(
                    $"保存履歴を読み込めませんでした: {ToSafeMessage(exception)}");
            }
        }

        private async Task LoadAvailableHistoryAsync()
        {
            if (_sessionId.HasValue || _disposed)
            {
                return;
            }

            try
            {
                IReadOnlyList<SqlOptimizationSession> sessions =
                    await _store.LoadAllAsync().ConfigureAwait(true);
                SqlOptimizationSession? latest = sessions
                    .OrderByDescending(session => session.UpdatedAt)
                    .FirstOrDefault();
                _view.SetSavedSessions(
                    sessions
                        .OrderByDescending(session => session.UpdatedAt)
                        .Select(ToSavedSessionDisplay),
                    latest?.SessionId.ToString("D"));
                if (latest is null)
                {
                    return;
                }

                _sessionId = latest.SessionId;
                LoadSessionInput(latest);
                RenderSession(latest, forceActivity: false);
                _view.SetActivity(
                    "前回の履歴を読込ました。バインド値は保存されていないため、再実行には値の再入力と承認が必要です。");
            }
            catch (Exception exception)
            {
                _view.SetActivity(
                    $"保存履歴を読み込めませんでした: {ToSafeMessage(exception)}");
            }
        }

        private async Task RefreshHistoryChoicesAsync(Guid selectedSessionId)
        {
            try
            {
                IReadOnlyList<SqlOptimizationSession> sessions =
                    await _store.LoadAllAsync().ConfigureAwait(true);
                _view.SetSavedSessions(
                    sessions
                        .OrderByDescending(session => session.UpdatedAt)
                        .Select(ToSavedSessionDisplay),
                    selectedSessionId.ToString("D"));
            }
            catch (Exception exception)
            {
                _view.SetActivity(
                    $"履歴一覧を更新できませんでした: {ToSafeMessage(exception)}");
            }
        }

        private async Task ShowCodexDiagnosticsAsync()
        {
            try
            {
                CodexCliDiagnostics diagnostics =
                    await _diagnostics.CheckAsync().ConfigureAwait(true);
                if (!diagnostics.IsAvailable)
                {
                    _view.SetActivity(
                        diagnostics.Error ?? "Codex CLIが見つかりません。");
                }
                else if (!diagnostics.IsAuthenticated)
                {
                    _view.SetActivity(
                        diagnostics.Error ?? "Codex CLIにログインしていません。");
                }
                else
                {
                    _view.SetActivity(
                        $"Codex CLI準備完了: {diagnostics.Version ?? diagnostics.ExecutablePath}");
                }
            }
            catch (Exception exception)
            {
                _view.SetActivity(
                    $"Codex起動診断に失敗しました: {ToSafeMessage(exception)}");
            }
        }

        private async Task RefreshSessionAsync(bool forceActivity = false)
        {
            if (!_sessionId.HasValue || _disposed ||
                !await _refreshGate.WaitAsync(0).ConfigureAwait(true))
            {
                return;
            }

            try
            {
                SqlOptimizationSession? session =
                    await _sessionService.GetAsync(_sessionId.Value)
                        .ConfigureAwait(true);
                if (session is not null)
                {
                    RenderSession(session, forceActivity);
                }
            }
            catch (ObjectDisposedException) when (_disposed)
            {
            }
            catch (Exception exception)
            {
                _view.SetActivity(
                    $"状態更新に失敗しました: {ToSafeMessage(exception)}");
            }
            finally
            {
                if (!_disposed)
                {
                    _refreshGate.Release();
                }
            }
        }

        private void RenderSession(
            SqlOptimizationSession session,
            bool forceActivity)
        {
            _sessionId = session.SessionId;
            _view.SetSessionId(session.SessionId.ToString("D"));
            _view.SetIterationHistory(
                session.Iterations.Select(
                    iteration => ToDisplay(session, iteration)));
            _view.SetUiState(
                ToFormState(session.Status),
                BuildStatusDetail(session));

            if (forceActivity || _lastObservedStatus != session.Status)
            {
                _view.SetActivity(
                    $"状態: {session.Status} / ラウンド {session.RoundNumber}");
                _lastObservedStatus = session.Status;
            }
        }

        private void LoadSessionInput(SqlOptimizationSession session)
        {
            IReadOnlyList<SqlBindDefinition> definitions =
                session.Approval?.BindDefinitions ??
                (session.LastApprovedBindDefinitions.Count > 0
                    ? session.LastApprovedBindDefinitions
                    :
                session.ExpectedBindNames
                    .Select(name => new SqlBindDefinition(
                        name,
                        OracleBindDataType.Varchar2))
                    .ToArray());

            _view.LoadInputForReview(
                new SqlOptimizerFormInput(
                    session.ConnectionAlias,
                    session.OriginalSql,
                    ObjectiveText(session.Objective),
                    session.Limits.CommandTimeoutSeconds,
                    session.Limits.PreviewRowLimit,
                    session.Limits.MaxCandidatesPerRound,
                    definitions
                        .Select(definition => new SqlOptimizerBindRow(
                            definition.Name,
                            definition.DataType.ToString(),
                            true,
                            null,
                            definition.Size))
                        .ToArray()));
        }

        private void ReportCodexEvent(CodexJsonlEvent codexEvent)
        {
            string message = codexEvent.Message ??
                codexEvent.Status ??
                codexEvent.ItemType ??
                codexEvent.Type;
            _view.SetActivity(
                $"Codex {codexEvent.Type}: {Truncate(message, 1000)}");
            _ = RefreshSessionAsync();
        }

        private void UpdateMcpStatus()
        {
            if (_mcpHost.IsRunning)
            {
                _view.SetMcpStatus($"MCP: {_mcpHost.Endpoint}");
            }
            else
            {
                _view.SetMcpStatus("MCP: 起動失敗");
                if (!string.IsNullOrWhiteSpace(_mcpStartupError))
                {
                    _view.SetActivity(_mcpStartupError);
                }
            }
        }

        private void CancelRun()
        {
            try
            {
                _runCancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            _cliRunner.CancelCurrentRun();
        }

        private static IReadOnlyList<SqlBindParameter> ConvertBindParameters(
            IReadOnlyList<SqlOptimizerBindRow> rows)
        {
            return rows.Select(row =>
            {
                if (!Enum.TryParse(
                        row.OracleType,
                        ignoreCase: true,
                        out OracleBindDataType dataType))
                {
                    throw new SqlValidationException(
                        $"未対応のOracle型です: {row.OracleType}");
                }

                if (!row.IsNull)
                {
                    _ = OracleBindValueConverter.Convert(
                        dataType,
                        row.Value);
                }

                return new SqlBindParameter(
                    row.Name,
                    dataType,
                    row.Value,
                    row.IsNull,
                    row.Size);
            }).ToArray();
        }

        private static SqlOptimizationObjective ParseObjective(string value)
        {
            return value.Trim() switch
            {
                "バランス" => SqlOptimizationObjective.Balanced,
                "応答時間" => SqlOptimizationObjective.ResponseTime,
                "I/O" => SqlOptimizationObjective.InputOutput,
                "計画安定性" => SqlOptimizationObjective.PlanStability,
                _ when Enum.TryParse(
                    value,
                    true,
                    out SqlOptimizationObjective objective) => objective,
                _ => throw new SqlValidationException(
                    $"未対応の最適化目標です: {value}")
            };
        }

        private static string ObjectiveText(SqlOptimizationObjective objective)
        {
            return objective switch
            {
                SqlOptimizationObjective.Balanced => "バランス",
                SqlOptimizationObjective.ResponseTime => "応答時間",
                SqlOptimizationObjective.InputOutput => "I/O",
                SqlOptimizationObjective.PlanStability => "計画安定性",
                _ => objective.ToString()
            };
        }

        private static SqlOptimizerFormState ToFormState(
            SqlOptimizationSessionStatus status)
        {
            return status switch
            {
                SqlOptimizationSessionStatus.Draft => SqlOptimizerFormState.Draft,
                SqlOptimizationSessionStatus.Approved => SqlOptimizerFormState.Approved,
                SqlOptimizationSessionStatus.Running => SqlOptimizerFormState.Running,
                SqlOptimizationSessionStatus.AwaitingUser => SqlOptimizerFormState.AwaitingUser,
                SqlOptimizationSessionStatus.Accepted => SqlOptimizerFormState.Accepted,
                SqlOptimizationSessionStatus.Stopped => SqlOptimizerFormState.Stopped,
                SqlOptimizationSessionStatus.Error => SqlOptimizerFormState.Error,
                _ => SqlOptimizerFormState.Error
            };
        }

        private static string BuildStatusDetail(SqlOptimizationSession session)
        {
            return session.Status switch
            {
                SqlOptimizationSessionStatus.Draft when session.Iterations.Count > 0 =>
                    "保存履歴です。バインド値を再入力し、初回承認してください。",
                SqlOptimizationSessionStatus.Draft =>
                    "SQL、型付きバインド、制限を入力して承認してください。",
                SqlOptimizationSessionStatus.Approved =>
                    "承認済みです。Codexまたは外部Agentが取得できます。",
                SqlOptimizationSessionStatus.Running =>
                    $"Agentが処理中です。残り候補 {session.RemainingCandidatesInRound}。",
                SqlOptimizationSessionStatus.AwaitingUser =>
                    "実測が完了しました。採用、フィードバックして継続、停止を選択してください。",
                SqlOptimizationSessionStatus.Accepted =>
                    "最終候補を採用しました。",
                SqlOptimizationSessionStatus.Stopped =>
                    "処理を停止しました。",
                SqlOptimizationSessionStatus.Error =>
                    session.ErrorMessage ?? "処理中にエラーが発生しました。",
                _ => session.Status.ToString()
            };
        }

        private static SqlOptimizerIterationDisplay ToDisplay(
            SqlOptimizationSession session,
            SqlOptimizationIteration iteration)
        {
            SqlOptimizationIteration? baseline = session.Iterations
                .FirstOrDefault(candidate =>
                    candidate.RoundNumber == iteration.RoundNumber &&
                    candidate.IsBaseline);
            decimal? cost = iteration.EstimatedPlan?.Nodes
                .OrderBy(node => node.Id)
                .Select(node => node.Cost)
                .FirstOrDefault(value => value.HasValue);

            return new SqlOptimizerIterationDisplay
            {
                IterationId = iteration.IterationId.ToString("D"),
                Round = iteration.RoundNumber,
                Candidate = iteration.CandidateNumber,
                Status = iteration.Status.ToString(),
                Cost = ToDisplayCost(cost),
                ElapsedMilliseconds =
                    iteration.ExecutionMetrics is null
                        ? null
                        : Convert.ToInt64(
                            iteration.ExecutionMetrics.Elapsed.TotalMilliseconds),
                Rows = iteration.ExecutionMetrics?.RowsRead,
                PlanHash = iteration.ActualPlan?.PlanHashValue ??
                    iteration.EstimatedPlan?.PlanHashValue ??
                    string.Empty,
                UpdatedAt = (iteration.CompletedAt ?? iteration.CreatedAt)
                    .LocalDateTime,
                SqlDiff = iteration.IsBaseline
                    ? iteration.Sql
                    : BuildLineDiff(baseline?.Sql ?? session.RoundBaseSql, iteration.Sql),
                ExplainPlan = iteration.EstimatedPlan?.FormattedPlan ?? string.Empty,
                AiAnalysis = BuildAnalysis(iteration),
                ActualPlan = iteration.ActualPlan?.FormattedPlan ?? string.Empty,
                ResultPreview = BuildPreviewTable(iteration.PreviewRows)
            };
        }

        private static long? ToDisplayCost(decimal? cost)
        {
            if (!cost.HasValue)
            {
                return null;
            }

            decimal truncated = decimal.Truncate(cost.Value);
            if (truncated > long.MaxValue)
            {
                return long.MaxValue;
            }

            if (truncated < long.MinValue)
            {
                return long.MinValue;
            }

            return decimal.ToInt64(truncated);
        }

        private static SqlOptimizerSavedSessionDisplay ToSavedSessionDisplay(
            SqlOptimizationSession session)
        {
            string id = session.SessionId.ToString("D");
            return new SqlOptimizerSavedSessionDisplay
            {
                SessionId = id,
                DisplayText =
                    $"{session.UpdatedAt.LocalDateTime:yyyy-MM-dd HH:mm} " +
                    $"{session.ConnectionAlias} {session.Status} " +
                    id.Substring(0, 8)
            };
        }

        private static string BuildAnalysis(SqlOptimizationIteration iteration)
        {
            StringBuilder output = new StringBuilder();
            AppendSection(output, "診断", iteration.Diagnosis);
            AppendSection(output, "変更理由", iteration.ChangeReason);
            AppendSection(output, "意味保存の根拠", iteration.SemanticEvidence);
            AppendSection(output, "最終分析", iteration.FinalAnalysis);
            AppendSection(output, "エラー", iteration.ErrorMessage);
            return output.ToString();
        }

        private static void AppendSection(
            StringBuilder output,
            string title,
            string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (output.Length > 0)
            {
                output.AppendLine().AppendLine();
            }

            output.Append(title).AppendLine(":").Append(value.Trim());
        }

        private static string BuildLineDiff(string original, string candidate)
        {
            if (string.Equals(original, candidate, StringComparison.Ordinal))
            {
                return "変更なし";
            }

            string[] left = NormalizeLines(original);
            string[] right = NormalizeLines(candidate);
            StringBuilder diff = new StringBuilder();
            int lineCount = Math.Max(left.Length, right.Length);
            for (int index = 0; index < lineCount; index++)
            {
                string? oldLine = index < left.Length ? left[index] : null;
                string? newLine = index < right.Length ? right[index] : null;
                if (string.Equals(oldLine, newLine, StringComparison.Ordinal))
                {
                    diff.Append("  ").AppendLine(oldLine);
                    continue;
                }

                if (oldLine is not null)
                {
                    diff.Append("- ").AppendLine(oldLine);
                }

                if (newLine is not null)
                {
                    diff.Append("+ ").AppendLine(newLine);
                }
            }

            return diff.ToString();
        }

        private static string[] NormalizeLines(string value)
        {
            return value
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');
        }

        private static DataTable? BuildPreviewTable(
            IReadOnlyList<SqlResultRow> rows)
        {
            if (rows.Count == 0)
            {
                return null;
            }

            DataTable table = new DataTable("ResultPreview");
            foreach (string name in rows[0].Values.Keys)
            {
                table.Columns.Add(name, typeof(object));
            }

            foreach (SqlResultRow row in rows)
            {
                DataRow dataRow = table.NewRow();
                foreach (DataColumn column in table.Columns)
                {
                    dataRow[column] =
                        row.Values.TryGetValue(column.ColumnName, out object? value)
                            ? value ?? DBNull.Value
                            : DBNull.Value;
                }

                table.Rows.Add(dataRow);
            }

            return table;
        }

        private static string ToSafeMessage(Exception exception)
        {
            string message = exception.Message.Trim();
            return Truncate(
                message.Length == 0
                    ? exception.GetType().Name
                    : message,
                4000);
        }

        private static string Truncate(string value, int maximum)
        {
            return value.Length <= maximum
                ? value
                : value.Substring(0, maximum) + "…";
        }
    }
}
