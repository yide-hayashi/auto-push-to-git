using System.Data;
using System.Diagnostics;
using System.Globalization;
using Project_auto_push_to_gitlab.SqlOptimizer.Codex;

namespace Project_auto_push_to_gitlab
{
    /// <summary>
    /// 負責顯示AI SQL最佳化流程、讀取使用者輸入，並將Oracle、MCP與Codex操作委派給Controller。
    /// </summary>
    public partial class frm_SqlOptimizer : Form
    {
        private SqlOptimizerFormState _state = SqlOptimizerFormState.Draft;
        private bool _suppressInputChanged;
        // 1150820 codex しずくちゃん_GPT-5：避免同步履歷時重複觸發選取事件與明細重繪。
        private bool _suppressHistorySelectionChanged;
        private bool _suppressCodexSelectionChanged;
        private bool _codexCatalogLoading;
        private CodexModelCatalog? _codexModelCatalog;

        /// <summary>
        /// 建立SQL最佳化畫面、設定預設條件並初始化Codex執行設定為繼承全域值。
        /// </summary>
        /// <remarks>
        /// UI：主程式直接建立frm_SqlOptimizer；建構時不連線Oracle、不啟動Codex，也不讀取LocalAppData。
        /// </remarks>
        public frm_SqlOptimizer()
        {
            // 1150820 codex しずくちゃん_gpt-5.6-sol：初始化動態Codex設定欄，但不把模型或能力固定在畫面程式內。
            InitializeComponent();
            cmbHospitalArea.SelectedIndex = 0;
            cmbGoal.SelectedIndex = 0;
            WireInputInvalidation();
            WireCodexExecutionSettings();
            SetCodexModelCatalog(
                null,
                CodexExecutionSelection.InheritDefaults,
                "Codex全体設定を継承します。");
            SetSessionId(string.Empty);
            SetMcpStatus("MCP: 未接続");
            SetUiState(SqlOptimizerFormState.Draft, "SQLと実行条件を入力してください。");
        }

        public event EventHandler? ApproveRequested;

        public event EventHandler? StartCodexRequested;

        public event EventHandler? RefreshCodexModelsRequested;

        public event EventHandler? CodexExecutionSelectionChanged;

        public event EventHandler? CopySessionIdRequested;

        public event EventHandler? ConfigureMcpRequested;

        public event EventHandler? StopRequested;

        public event EventHandler? AcceptRequested;

        public event EventHandler? ContinueRequested;

        public event EventHandler? ApprovalInvalidated;

        public event EventHandler? SelectedIterationChanged;

        public event EventHandler? SavedSessionSelected;

        public string SessionId { get; private set; } = string.Empty;

        public string FeedbackText => txtFeedback.Text.Trim();

        public string? SelectedIterationId
        {
            get
            {
                if (gridHistory.CurrentRow?.Tag is SqlOptimizerIterationDisplay iteration)
                {
                    return iteration.IterationId;
                }

                return null;
            }
        }

        public SqlOptimizerFormState CurrentState => _state;

        /// <summary>
        /// 取得目前畫面選定的Codex模型、推論量與服務層，供下一次子程序固定使用。
        /// </summary>
        /// <returns>有模型目錄時回傳三個選擇值；目錄不可用時回傳全部為null的繼承設定。</returns>
        /// <remarks>
        /// UI：由SqlOptimizerUiController在btnStartCodex與三個ComboBox變更時直接讀取。
        /// 本屬性不修改UI或Codex全域設定。
        /// </remarks>
        public CodexExecutionSelection SelectedCodexExecutionSelection
        {
            get
            {
                // 1150820 codex しずくちゃん_gpt-5.6-sol：將三個ComboBox目前值整理為單次Codex執行選擇。
                if (cmbCodexModel.SelectedItem is not CodexModelOption model)
                {
                    return CodexExecutionSelection.InheritDefaults;
                }

                string? reasoning =
                    (cmbCodexReasoning.SelectedItem as CodexReasoningEffortOption)
                    ?.Value;
                string? serviceTier =
                    (cmbCodexSpeed.SelectedItem as CodexServiceTierOption)
                    ?.Id;
                return new CodexExecutionSelection(
                    model.Model,
                    reasoning,
                    serviceTier);
            }
        }

        public string? SelectedSavedSessionId =>
            (cmbSavedSessions.SelectedItem as SqlOptimizerSavedSessionDisplay)
            ?.SessionId;

        public bool TryCaptureInput(out SqlOptimizerFormInput? input, out string validationMessage)
        {
            input = null;
            validationMessage = string.Empty;

            string sql = txtSql.Text.Trim();
            if (sql.Length == 0)
            {
                validationMessage = "SQLを入力してください。";
                return false;
            }

            string hospitalArea = Convert.ToString(cmbHospitalArea.SelectedItem, CultureInfo.InvariantCulture)
                ?? cmbHospitalArea.Text.Trim();
            if (hospitalArea.Length == 0)
            {
                validationMessage = "接続先を選択してください。";
                return false;
            }

            List<SqlOptimizerBindRow> bindRows = new();
            HashSet<string> bindNames = new(StringComparer.OrdinalIgnoreCase);

            foreach (DataGridViewRow row in gridBindings.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                string name = Convert.ToString(row.Cells[colBindName.Index].Value, CultureInfo.InvariantCulture)?.Trim()
                    ?? string.Empty;
                string oracleType = Convert.ToString(row.Cells[colBindType.Index].Value, CultureInfo.InvariantCulture)?.Trim()
                    ?? string.Empty;
                bool isNull = Convert.ToBoolean(row.Cells[colBindNull.Index].Value ?? false, CultureInfo.InvariantCulture);
                string? value = isNull
                    ? null
                    : Convert.ToString(row.Cells[colBindValue.Index].Value, CultureInfo.InvariantCulture);
                string sizeText = Convert.ToString(row.Cells[colBindSize.Index].Value, CultureInfo.InvariantCulture)?.Trim()
                    ?? string.Empty;

                if (name.Length == 0)
                {
                    validationMessage = $"バインド変数の{row.Index + 1}行目に名前を入力してください。";
                    return false;
                }

                if (!bindNames.Add(name.TrimStart(':')))
                {
                    validationMessage = $"バインド変数「{name}」が重複しています。";
                    return false;
                }

                if (oracleType.Length == 0)
                {
                    validationMessage = $"バインド変数「{name}」のOracle型を選択してください。";
                    return false;
                }

                if (!isNull && value is null)
                {
                    validationMessage =
                        $"バインド :{name} は値を入力するか、NULLを選択してください。";
                    return false;
                }

                int? size = null;
                if (sizeText.Length > 0)
                {
                    if (!int.TryParse(sizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedSize)
                        || parsedSize <= 0)
                    {
                        validationMessage = $"バインド変数「{name}」のサイズは1以上の整数で入力してください。";
                        return false;
                    }

                    size = parsedSize;
                }

                bindRows.Add(new SqlOptimizerBindRow(name.TrimStart(':'), oracleType, isNull, value, size));
            }

            input = new SqlOptimizerFormInput(
                hospitalArea,
                sql,
                Convert.ToString(cmbGoal.SelectedItem, CultureInfo.InvariantCulture) ?? cmbGoal.Text,
                Decimal.ToInt32(numTimeoutSeconds.Value),
                Decimal.ToInt32(numPreviewRows.Value),
                Decimal.ToInt32(numMaxCandidates.Value),
                bindRows);
            return true;
        }

        public void SetSessionId(string? sessionId)
        {
            RunOnUiThread(() =>
            {
                SessionId = sessionId?.Trim() ?? string.Empty;
                lblSessionId.Text = SessionId.Length == 0
                    ? "Session: 未作成"
                    : $"Session: {SessionId}";
                btnCopySessionId.Enabled = SessionId.Length > 0;
                btnStartCodex.Enabled = _state == SqlOptimizerFormState.Approved && SessionId.Length > 0;
            });
        }

        public void SetMcpStatus(string status)
        {
            RunOnUiThread(() => lblMcpStatus.Text = status);
        }

        /// <summary>
        /// 將Codex模型目錄與偏好選擇套用至三個ComboBox，並依模型能力重建推論量及速度選項。
        /// </summary>
        /// <param name="catalog">CLI或應用程式快取提供的模型目錄；null或空目錄時改為繼承Codex全域設定。</param>
        /// <param name="preferred">希望還原的模型、推論量與服務層；null時使用目錄預設值。</param>
        /// <param name="status">顯示於lblCodexCatalogStatus的取得或替代狀態；null時顯示空字串。</param>
        /// <remarks>
        /// UI：由SqlOptimizerUiController在Shown與btnRefreshCodexModels完成後直接使用。
        /// 程式化重建選項時不觸發CodexExecutionSelectionChanged，也不使SQL承認失效。
        /// </remarks>
        public void SetCodexModelCatalog(
            CodexModelCatalog? catalog,
            CodexExecutionSelection? preferred,
            string? status)
        {
            RunOnUiThread(() =>
            {
                // 1150820 codex しずくちゃん_gpt-5.6-sol：以CLI回傳能力重建選項，並保留未來新增的未知字串值。
                _suppressCodexSelectionChanged = true;
                cmbCodexModel.BeginUpdate();
                cmbCodexReasoning.BeginUpdate();
                cmbCodexSpeed.BeginUpdate();
                try
                {
                    _codexModelCatalog = catalog;
                    cmbCodexModel.Items.Clear();
                    cmbCodexReasoning.Items.Clear();
                    cmbCodexSpeed.Items.Clear();

                    if (catalog is not { Models.Count: > 0 })
                    {
                        cmbCodexModel.Items.Add("Codex既定値");
                        cmbCodexReasoning.Items.Add("Codex既定値");
                        cmbCodexSpeed.Items.Add("Codex既定値");
                        cmbCodexModel.SelectedIndex = 0;
                        cmbCodexReasoning.SelectedIndex = 0;
                        cmbCodexSpeed.SelectedIndex = 0;
                        lblCodexCatalogStatus.Text = status ?? string.Empty;
                        return;
                    }

                    CodexExecutionSelection selection =
                        CodexExecutionSelectionResolver.Resolve(
                            catalog,
                            preferred,
                            out string? warning);
                    cmbCodexModel.Items.AddRange(catalog.Models.Cast<object>().ToArray());
                    CodexModelOption selectedModel = catalog.Models
                        .First(item => string.Equals(
                            item.Model,
                            selection.Model,
                            StringComparison.OrdinalIgnoreCase));
                    cmbCodexModel.SelectedItem = selectedModel;
                    PopulateCodexCapabilityOptions(selectedModel, selection);
                    lblCodexCatalogStatus.Text = CombineCodexStatus(
                        status,
                        warning,
                        selectedModel.MigrationMessage);
                }
                finally
                {
                    cmbCodexSpeed.EndUpdate();
                    cmbCodexReasoning.EndUpdate();
                    cmbCodexModel.EndUpdate();
                    _suppressCodexSelectionChanged = false;
                }
            });
        }

        /// <summary>
        /// 設定Codex模型目錄是否正在讀取，並同步三個選擇欄與更新Button的可用狀態。
        /// </summary>
        /// <param name="isLoading">正在讀取CLI目錄時為true，讀取結束時為false。</param>
        /// <param name="status">讀取中或完成狀態；null時保留lblCodexCatalogStatus原文。</param>
        /// <remarks>
        /// UI：由SqlOptimizerUiController在Shown與btnRefreshCodexModels的取得前後直接使用。
        /// SQL輸入與承認狀態不受此方法影響。
        /// </remarks>
        public void SetCodexCatalogLoading(
            bool isLoading,
            string? status = null)
        {
            RunOnUiThread(() =>
            {
                // 1150820 codex しずくちゃん_gpt-5.6-sol：目錄讀取期間暫停設定操作並維持其他SQL控制項狀態。
                _codexCatalogLoading = isLoading;
                if (status is not null)
                {
                    lblCodexCatalogStatus.Text = status;
                }

                UpdateCodexExecutionControlsEnabled();
            });
        }

        /// <summary>
        /// 更新Codex目錄的取得、警告或fallback狀態文字。
        /// </summary>
        /// <param name="status">顯示於lblCodexCatalogStatus的文字；null視為空字串。</param>
        /// <remarks>
        /// UI：由SqlOptimizerUiController在目錄取得或選擇保存失敗時直接使用。
        /// </remarks>
        public void SetCodexCatalogStatus(string? status)
        {
            // 1150820 codex しずくちゃん_gpt-5.6-sol：集中更新動態模型目錄的狀態與fallback說明。
            RunOnUiThread(
                () => lblCodexCatalogStatus.Text = status ?? string.Empty);
        }

        /// <summary>
        /// 依指定模型重建其支援的推論量與速度，並套用已校正的選擇值。
        /// </summary>
        /// <param name="model">目前模型ComboBox選取的目錄項目，不可為null。</param>
        /// <param name="selection">已依該模型能力校正的執行選擇，不可為null。</param>
        /// <remarks>
        /// UI：由SetCodexModelCatalog與cmbCodexModel選擇事件間接更新cmbCodexReasoning及cmbCodexSpeed。
        /// 每次先清除既有推論量與速度選項，避免切換模型時累加上一個模型的項目。
        /// 標準速度default一律加入；其他速度只採用CLI目錄實際回傳值。
        /// 依selection還原支援的速度；不支援時選取標準速度，且不修改傳入物件。
        /// </remarks>
        private void PopulateCodexCapabilityOptions(
            CodexModelOption model,
            CodexExecutionSelection selection)
        {
            // 1150820 codex しずくちゃん_gpt-5.6-sol：依模型能力建立推論量與速度清單，且永遠補入標準速度。
            cmbCodexReasoning.Items.Clear();
            cmbCodexSpeed.Items.Clear();
            if (model.SupportedReasoningEfforts.Count == 0)
            {
                cmbCodexReasoning.Items.Add("Codex既定値");
                cmbCodexReasoning.SelectedIndex = 0;
            }
            else
            {
                cmbCodexReasoning.Items.AddRange(
                    model.SupportedReasoningEfforts.Cast<object>().ToArray());
                cmbCodexReasoning.SelectedItem =
                    model.SupportedReasoningEfforts.FirstOrDefault(item =>
                        string.Equals(
                            item.Value,
                            selection.ReasoningEffort,
                            StringComparison.OrdinalIgnoreCase))
                    ?? model.SupportedReasoningEfforts[0];
            }

            List<CodexServiceTierOption> serviceTiers =
            [
                new CodexServiceTierOption(
                    "default",
                    "標準 (default)",
                    "Codexの標準速度を使用します。")
            ];
            serviceTiers.AddRange(model.ServiceTiers.Where(item =>
                !string.Equals(
                    item.Id,
                    "default",
                    StringComparison.OrdinalIgnoreCase)));
            cmbCodexSpeed.Items.AddRange(serviceTiers.Cast<object>().ToArray());
            cmbCodexSpeed.SelectedItem = serviceTiers.FirstOrDefault(item =>
                string.Equals(
                    item.Id,
                    selection.ServiceTier,
                    StringComparison.OrdinalIgnoreCase))
                ?? serviceTiers[0];
        }

        /// <summary>
        /// 將不為空白的Codex狀態訊息合併為畫面用單行文字。
        /// </summary>
        /// <param name="messages">依顯示順序提供的訊息；可包含null或空白。</param>
        /// <returns>以半形空白連結的有效訊息；全部空白時回傳空字串。</returns>
        /// <remarks>UI：僅供frm_SqlOptimizer的lblCodexCatalogStatus組字使用。</remarks>
        private static string CombineCodexStatus(params string?[] messages)
        {
            // 1150820 codex しずくちゃん_gpt-5.6-sol：排除空白訊息以避免狀態欄產生多餘間隔。
            return string.Join(
                " ",
                messages.Where(message => !string.IsNullOrWhiteSpace(message)));
        }

        public void SetSavedSessions(
            IEnumerable<SqlOptimizerSavedSessionDisplay> sessions,
            string? selectedSessionId)
        {
            ArgumentNullException.ThrowIfNull(sessions);
            RunOnUiThread(() =>
            {
                SqlOptimizerSavedSessionDisplay[] items = sessions.ToArray();
                cmbSavedSessions.BeginUpdate();
                try
                {
                    cmbSavedSessions.Items.Clear();
                    cmbSavedSessions.Items.AddRange(items);
                    SqlOptimizerSavedSessionDisplay? selected = items
                        .FirstOrDefault(item => string.Equals(
                            item.SessionId,
                            selectedSessionId,
                            StringComparison.OrdinalIgnoreCase));
                    cmbSavedSessions.SelectedItem = selected;
                }
                finally
                {
                    cmbSavedSessions.EndUpdate();
                }
            });
        }

        public void LoadInputForReview(SqlOptimizerFormInput input)
        {
            ArgumentNullException.ThrowIfNull(input);
            RunOnUiThread(() =>
            {
                cmbHospitalArea.SelectedItem = input.HospitalArea;
                if (cmbHospitalArea.SelectedIndex < 0)
                {
                    cmbHospitalArea.Text = input.HospitalArea;
                }

                txtSql.Text = input.Sql;
                cmbGoal.SelectedItem = input.OptimizationGoal;
                if (cmbGoal.SelectedIndex < 0)
                {
                    cmbGoal.Text = input.OptimizationGoal;
                }

                numTimeoutSeconds.Value = Math.Clamp(
                    input.CommandTimeoutSeconds,
                    Decimal.ToInt32(numTimeoutSeconds.Minimum),
                    Decimal.ToInt32(numTimeoutSeconds.Maximum));
                numPreviewRows.Value = Math.Clamp(
                    input.PreviewRowLimit,
                    Decimal.ToInt32(numPreviewRows.Minimum),
                    Decimal.ToInt32(numPreviewRows.Maximum));
                numMaxCandidates.Value = Math.Clamp(
                    input.MaxCandidatesPerRound,
                    Decimal.ToInt32(numMaxCandidates.Minimum),
                    Decimal.ToInt32(numMaxCandidates.Maximum));

                gridBindings.Rows.Clear();
                foreach (SqlOptimizerBindRow bind in input.Bindings)
                {
                    gridBindings.Rows.Add(
                        bind.Name,
                        bind.OracleType,
                        bind.IsNull,
                        bind.Value,
                        bind.Size);
                }
            });
        }

        public void SetUiState(SqlOptimizerFormState state, string? detail = null)
        {
            RunOnUiThread(() =>
            {
                _state = state;
                lblState.Text = $"状態: {GetStateText(state)}";
                lblDetail.Text = detail ?? string.Empty;

                bool inputEnabled = state is SqlOptimizerFormState.Draft
                    or SqlOptimizerFormState.Approved
                    or SqlOptimizerFormState.Accepted
                    or SqlOptimizerFormState.Stopped
                    or SqlOptimizerFormState.Error;

                cmbHospitalArea.Enabled = inputEnabled;
                cmbGoal.Enabled = inputEnabled;
                txtSql.ReadOnly = !inputEnabled;
                gridBindings.ReadOnly = !inputEnabled;
                gridBindings.AllowUserToAddRows = inputEnabled;
                gridBindings.AllowUserToDeleteRows = inputEnabled;
                numTimeoutSeconds.Enabled = inputEnabled;
                numPreviewRows.Enabled = inputEnabled;
                numMaxCandidates.Enabled = inputEnabled;

                btnApprove.Enabled = state is SqlOptimizerFormState.Draft
                    or SqlOptimizerFormState.Accepted
                    or SqlOptimizerFormState.Stopped
                    or SqlOptimizerFormState.Error;
                btnStartCodex.Enabled = state == SqlOptimizerFormState.Approved && SessionId.Length > 0;
                btnStop.Enabled = state is SqlOptimizerFormState.Running
                    or SqlOptimizerFormState.AwaitingUser;
                btnAccept.Enabled = state == SqlOptimizerFormState.AwaitingUser;
                btnContinue.Enabled = state == SqlOptimizerFormState.AwaitingUser;
                txtFeedback.ReadOnly = state != SqlOptimizerFormState.AwaitingUser;
                btnConfigureMcp.Enabled = state != SqlOptimizerFormState.Running;
                UpdateCodexExecutionControlsEnabled();
                cmbSavedSessions.Enabled = state is SqlOptimizerFormState.Draft
                    or SqlOptimizerFormState.Accepted
                    or SqlOptimizerFormState.Stopped
                    or SqlOptimizerFormState.Error;
                UseWaitCursor = state == SqlOptimizerFormState.Running;
            });
        }

        /// <summary>
        /// 將最佳化反覆記錄同步至履歷Grid，並在內容未變更時保留既有列與結果預覽繫結。
        /// </summary>
        /// <param name="iterations">依顯示順序提供的反覆記錄，不可為null；本方法不會修改集合或其中物件。</param>
        /// <remarks>
        /// UI：由SqlOptimizerUiController間接更新frm_SqlOptimizer的gridHistory。
        /// 選取列的詳細內容顯示於txtSqlDiff、txtExplainPlan、txtAiAnalysis、txtActualPlan及gridResultPreview。
        /// 相同快照不清除或重建列，以避免定時更新造成畫面持續閃爍。
        /// </remarks>
        public void SetIterationHistory(IEnumerable<SqlOptimizerIterationDisplay> iterations)
        {
            ArgumentNullException.ThrowIfNull(iterations);
            SqlOptimizerIterationDisplay[] iterationSnapshot = iterations.ToArray();

            RunOnUiThread(() =>
            {
                string? selectedId = SelectedIterationId;
                bool canUpdateInPlace = gridHistory.Rows.Count <= iterationSnapshot.Length;
                for (int rowIndex = 0;
                     canUpdateInPlace && rowIndex < gridHistory.Rows.Count;
                     rowIndex++)
                {
                    canUpdateInPlace =
                        gridHistory.Rows[rowIndex].Tag is SqlOptimizerIterationDisplay current &&
                        string.Equals(
                            current.IterationId,
                            iterationSnapshot[rowIndex].IterationId,
                            StringComparison.Ordinal);
                }

                // 1150820 codex しずくちゃん_GPT-5：僅在履歷內容實際變更時更新Grid，避免每1.5秒清空重建而閃爍。
                _suppressHistorySelectionChanged = true;
                gridHistory.SuspendLayout();
                SqlOptimizerIterationDisplay? selectedIterationToRefresh = null;
                try
                {
                    if (!canUpdateInPlace)
                    {
                        gridHistory.Rows.Clear();
                    }

                    int existingRowCount = gridHistory.Rows.Count;
                    for (int rowIndex = 0; rowIndex < existingRowCount; rowIndex++)
                    {
                        DataGridViewRow row = gridHistory.Rows[rowIndex];
                        SqlOptimizerIterationDisplay iteration = iterationSnapshot[rowIndex];
                        if (row.Tag is SqlOptimizerIterationDisplay current &&
                            IterationDisplaysMatch(current, iteration))
                        {
                            continue;
                        }

                        PopulateIterationRow(row, iteration);
                        row.Tag = iteration;
                        if (string.Equals(
                                iteration.IterationId,
                                selectedId,
                                StringComparison.Ordinal))
                        {
                            selectedIterationToRefresh = iteration;
                        }
                    }

                    for (int rowIndex = existingRowCount;
                         rowIndex < iterationSnapshot.Length;
                         rowIndex++)
                    {
                        AddIterationRow(iterationSnapshot[rowIndex]);
                    }

                    DataGridViewRow? rowToSelect = gridHistory.Rows
                        .Cast<DataGridViewRow>()
                        .FirstOrDefault(row =>
                            row.Tag is SqlOptimizerIterationDisplay item &&
                            string.Equals(
                                item.IterationId,
                                selectedId,
                                StringComparison.Ordinal));

                    if (rowToSelect is null && gridHistory.Rows.Count > 0)
                    {
                        rowToSelect = gridHistory.Rows[gridHistory.Rows.Count - 1];
                    }

                    if (rowToSelect is not null && gridHistory.CurrentRow != rowToSelect)
                    {
                        rowToSelect.Selected = true;
                        gridHistory.CurrentCell = rowToSelect.Cells[0];
                        selectedIterationToRefresh =
                            rowToSelect.Tag as SqlOptimizerIterationDisplay;
                    }

                    if (rowToSelect is not null &&
                        selectedIterationToRefresh is null &&
                        (!canUpdateInPlace || selectedId is null))
                    {
                        selectedIterationToRefresh =
                            rowToSelect.Tag as SqlOptimizerIterationDisplay;
                    }
                }
                finally
                {
                    gridHistory.ResumeLayout();
                    _suppressHistorySelectionChanged = false;
                }

                if (selectedIterationToRefresh is not null)
                {
                    ShowIterationDetails(selectedIterationToRefresh);
                }
                else if (gridHistory.Rows.Count == 0)
                {
                    ClearIterationDetails();
                }
            });
        }

        public void UpsertIteration(SqlOptimizerIterationDisplay iteration)
        {
            RunOnUiThread(() =>
            {
                DataGridViewRow? existingRow = gridHistory.Rows
                    .Cast<DataGridViewRow>()
                    .FirstOrDefault(row =>
                        row.Tag is SqlOptimizerIterationDisplay item
                        && string.Equals(item.IterationId, iteration.IterationId, StringComparison.Ordinal));

                if (existingRow == null)
                {
                    AddIterationRow(iteration);
                    existingRow = gridHistory.Rows[gridHistory.Rows.Count - 1];
                }
                else
                {
                    PopulateIterationRow(existingRow, iteration);
                }

                existingRow.Selected = true;
                gridHistory.CurrentCell = existingRow.Cells[0];
                existingRow.Tag = iteration;
                ShowIterationDetails(iteration);
            });
        }

        public void ShowIterationDetails(SqlOptimizerIterationDisplay iteration)
        {
            RunOnUiThread(() =>
            {
                txtSqlDiff.Text = iteration.SqlDiff;
                txtExplainPlan.Text = iteration.ExplainPlan;
                txtAiAnalysis.Text = iteration.AiAnalysis;
                txtActualPlan.Text = iteration.ActualPlan;
                gridResultPreview.DataSource = iteration.ResultPreview;
                lblResultNotice.Text = iteration.ResultPreview == null
                    ? "結果プレビューはありません。バインド値と結果行はAgentへ送信されません。"
                    : $"先頭 {iteration.ResultPreview.Rows.Count:N0} 行のみ表示しています。";
            });
        }

        public void SetActivity(string message)
        {
            RunOnUiThread(() =>
            {
                lblDetail.Text = message;
                txtActivity.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            });
        }

        public void ClearSessionData()
        {
            RunOnUiThread(() =>
            {
                SetSessionId(string.Empty);
                gridHistory.Rows.Clear();
                txtFeedback.Clear();
                txtActivity.Clear();
                ClearIterationDetails();
                SetUiState(SqlOptimizerFormState.Draft, "新しい最適化セッションを入力してください。");
            });
        }

        public void ShowOperationError(string message)
        {
            SetUiState(SqlOptimizerFormState.Error, message);
            SetActivity($"エラー: {message}");
        }

        private void WireInputInvalidation()
        {
            txtSql.TextChanged += InputControlChanged;
            cmbHospitalArea.SelectedIndexChanged += InputControlChanged;
            cmbGoal.SelectedIndexChanged += InputControlChanged;
            numTimeoutSeconds.ValueChanged += InputControlChanged;
            numPreviewRows.ValueChanged += InputControlChanged;
            numMaxCandidates.ValueChanged += InputControlChanged;
            gridBindings.CellValueChanged += (_, _) => InvalidateApproval();
            gridBindings.UserDeletedRow += (_, _) => InvalidateApproval();
            gridBindings.CurrentCellDirtyStateChanged += (_, _) =>
            {
                if (gridBindings.IsCurrentCellDirty)
                {
                    gridBindings.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
        }

        /// <summary>
        /// 連結Codex模型、推論量、速度與目錄更新控制項的使用者操作事件。
        /// </summary>
        /// <remarks>
        /// UI：由frm_SqlOptimizer建構式直接使用；事件只委派給Controller，不會使已核准SQL失效。
        /// </remarks>
        private void WireCodexExecutionSettings()
        {
            // 1150820 codex しずくちゃん_gpt-5.6-sol：將Codex設定事件與SQL輸入失效事件保持分離。
            cmbCodexModel.SelectionChangeCommitted +=
                CodexModelSelectionChangeCommitted;
            cmbCodexReasoning.SelectionChangeCommitted +=
                CodexCapabilitySelectionChangeCommitted;
            cmbCodexSpeed.SelectionChangeCommitted +=
                CodexCapabilitySelectionChangeCommitted;
            btnRefreshCodexModels.Click += (_, _) =>
                RefreshCodexModelsRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 使用者切換模型後，依新模型實際能力校正推論量與速度選擇。
        /// </summary>
        /// <param name="sender">cmbCodexModel事件來源。</param>
        /// <param name="e">不含額外資料的選擇事件引數。</param>
        /// <remarks>
        /// UI：由cmbCodexModel直接使用，重建cmbCodexReasoning與cmbCodexSpeed後通知Controller保存。
        /// </remarks>
        private void CodexModelSelectionChangeCommitted(
            object? sender,
            EventArgs e)
        {
            if (_suppressCodexSelectionChanged ||
                _codexModelCatalog is null ||
                cmbCodexModel.SelectedItem is not CodexModelOption model)
            {
                return;
            }

            CodexExecutionSelection requested =
                SelectedCodexExecutionSelection with { Model = model.Model };
            CodexExecutionSelection resolved =
                CodexExecutionSelectionResolver.Resolve(
                    _codexModelCatalog,
                    requested,
                    out string? warning);
            // 1150820 codex しずくちゃん_gpt-5.6-sol：模型變更時將新模型不支援的前次值切回其預設能力。
            _suppressCodexSelectionChanged = true;
            try
            {
                PopulateCodexCapabilityOptions(model, resolved);
                lblCodexCatalogStatus.Text = CombineCodexStatus(
                    warning,
                    model.MigrationMessage);
            }
            finally
            {
                _suppressCodexSelectionChanged = false;
            }

            CodexExecutionSelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 使用者確定推論量或速度後通知Controller保存目前組合。
        /// </summary>
        /// <param name="sender">cmbCodexReasoning或cmbCodexSpeed事件來源。</param>
        /// <param name="e">不含額外資料的選擇事件引數。</param>
        /// <remarks>
        /// UI：由cmbCodexReasoning與cmbCodexSpeed直接使用；不修改SQL輸入或承認狀態。
        /// </remarks>
        private void CodexCapabilitySelectionChangeCommitted(
            object? sender,
            EventArgs e)
        {
            if (!_suppressCodexSelectionChanged)
            {
                // 1150820 codex しずくちゃん_gpt-5.6-sol：只在使用者確定選擇時通知Controller保存。
                CodexExecutionSelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 依Codex執行與目錄讀取狀態同步三個選擇欄及更新Button的Enabled值。
        /// </summary>
        /// <remarks>
        /// UI：由SetUiState與SetCodexCatalogLoading間接使用；Running時停用Codex設定，但不變更SQL承認。
        /// </remarks>
        private void UpdateCodexExecutionControlsEnabled()
        {
            // 1150820 codex しずくちゃん_gpt-5.6-sol：僅在Codex執行或目錄讀取期間鎖定執行設定。
            bool enabled =
                _state != SqlOptimizerFormState.Running &&
                !_codexCatalogLoading;
            cmbCodexModel.Enabled = enabled;
            cmbCodexReasoning.Enabled = enabled;
            cmbCodexSpeed.Enabled = enabled;
            btnRefreshCodexModels.Enabled = enabled;
        }

        private void InputControlChanged(object? sender, EventArgs e)
        {
            InvalidateApproval();
        }

        private void InvalidateApproval()
        {
            if (_suppressInputChanged
                || _state is not (SqlOptimizerFormState.Approved
                    or SqlOptimizerFormState.Stopped
                    or SqlOptimizerFormState.Error
                    or SqlOptimizerFormState.Accepted))
            {
                return;
            }

            SetSessionId(string.Empty);
            SetUiState(SqlOptimizerFormState.Draft, "入力が変更されたため、承認が失効しました。");
            ApprovalInvalidated?.Invoke(this, EventArgs.Empty);
        }

        private void btnApprove_Click(object sender, EventArgs e)
        {
            if (!TryCaptureInput(out _, out string validationMessage))
            {
                lblDetail.Text = validationMessage;
                System.Media.SystemSounds.Exclamation.Play();
                return;
            }

            if (ApproveRequested == null)
            {
                lblDetail.Text = "SQL最適化Controllerが接続されていません。";
                return;
            }

            SetUiState(SqlOptimizerFormState.Approved, "承認処理を開始しました。");
            ApproveRequested.Invoke(this, EventArgs.Empty);
        }

        private void btnStartCodex_Click(object sender, EventArgs e)
        {
            if (StartCodexRequested == null)
            {
                lblDetail.Text = "Codex起動Controllerが接続されていません。";
                return;
            }

            StartCodexRequested.Invoke(this, EventArgs.Empty);
        }

        private void btnCopySessionId_Click(object sender, EventArgs e)
        {
            try
            {
                if (SessionId.Length == 0)
                {
                    lblDetail.Text = "コピーできるSession IDがありません。";
                    return;
                }

                Clipboard.SetText(SessionId);
                lblDetail.Text = "外部Agent用Session IDをコピーしました。";
                CopySessionIdRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                lblDetail.Text = $"Session IDをコピーできませんでした: {ex.Message}";
            }
        }

        private void btnConfigureMcp_Click(object sender, EventArgs e)
        {
            if (ConfigureMcpRequested == null)
            {
                lblDetail.Text = "MCP設定Controllerが接続されていません。";
                return;
            }

            ConfigureMcpRequested.Invoke(this, EventArgs.Empty);
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            SqlOptimizer.Application.SqlOptimizerHelpDocumentOpenResult result =
                SqlOptimizer.Application.SqlOptimizerHelpDocumentLauncher.TryOpen(
                    AppContext.BaseDirectory,
                    Process.Start);

            if (result.Status ==
                SqlOptimizer.Application.SqlOptimizerHelpDocumentOpenStatus.Missing)
            {
                MessageBox.Show(
                    this,
                    $"操作説明書が見つかりません。{Environment.NewLine}{Environment.NewLine}"
                    + $"{result.GuidePath}{Environment.NewLine}{Environment.NewLine}"
                    + "アプリの配布フォルダーにdocsフォルダーと説明書があることを確認してください。",
                    "AI SQL最適化 - 使い方",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (result.Status ==
                SqlOptimizer.Application.SqlOptimizerHelpDocumentOpenStatus.Failed)
            {
                MessageBox.Show(
                    this,
                    $"操作説明書を開けませんでした。{Environment.NewLine}{Environment.NewLine}"
                    + $"{result.GuidePath}{Environment.NewLine}{Environment.NewLine}"
                    + "Microsoft WordなどのDOCX対応アプリがインストールされ、"
                    + ".docxファイルの関連付けが設定されていることを確認してください。",
                    "AI SQL最適化 - 使い方",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            StopRequested?.Invoke(this, EventArgs.Empty);
        }

        private void btnAccept_Click(object sender, EventArgs e)
        {
            if (SelectedIterationId == null)
            {
                lblDetail.Text = "採用する候補を履歴から選択してください。";
                return;
            }

            AcceptRequested?.Invoke(this, EventArgs.Empty);
        }

        private void btnContinue_Click(object sender, EventArgs e)
        {
            if (FeedbackText.Length == 0)
            {
                lblDetail.Text = "継続する理由または要望を入力してください。";
                txtFeedback.Focus();
                return;
            }

            ContinueRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 使用者變更履歷選取列時，顯示該次反覆處理的SQL、計畫、分析及結果預覽。
        /// </summary>
        /// <param name="sender">觸發事件的gridHistory；不可為null且本方法不會修改參考。</param>
        /// <param name="e">選取變更事件資料；不可為null且本方法不會修改內容。</param>
        /// <remarks>
        /// UI：由frm_SqlOptimizer的gridHistory直接使用，詳細資料顯示於detailsTabs內各控制項。
        /// 程式同步履歷期間會抑制事件，完成後再針對有效選取列更新一次。
        /// </remarks>
        private void gridHistory_SelectionChanged(object sender, EventArgs e)
        {
            // 1150820 codex しずくちゃん_GPT-5：同步履歷期間不重複刷新明細與結果Grid。
            if (_suppressHistorySelectionChanged ||
                gridHistory.CurrentRow?.Tag is not SqlOptimizerIterationDisplay iteration)
            {
                return;
            }

            ShowIterationDetails(iteration);
            SelectedIterationChanged?.Invoke(this, EventArgs.Empty);
        }

        private void cmbSavedSessions_SelectionChangeCommitted(
            object sender,
            EventArgs e)
        {
            SavedSessionSelected?.Invoke(this, EventArgs.Empty);
        }

        private void AddIterationRow(SqlOptimizerIterationDisplay iteration)
        {
            int rowIndex = gridHistory.Rows.Add();
            DataGridViewRow row = gridHistory.Rows[rowIndex];
            PopulateIterationRow(row, iteration);
            row.Tag = iteration;
        }

        private void PopulateIterationRow(DataGridViewRow row, SqlOptimizerIterationDisplay iteration)
        {
            row.Cells[colRound.Index].Value = iteration.Round;
            row.Cells[colCandidate.Index].Value = iteration.Candidate;
            row.Cells[colIterationStatus.Index].Value = iteration.Status;
            row.Cells[colCost.Index].Value = iteration.Cost;
            row.Cells[colElapsed.Index].Value = iteration.ElapsedMilliseconds;
            row.Cells[colRows.Index].Value = iteration.Rows;
            row.Cells[colPlanHash.Index].Value = iteration.PlanHash;
            row.Cells[colUpdatedAt.Index].Value = iteration.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 比較兩個履歷顯示快照是否會產生相同的Grid列與詳細內容。
        /// </summary>
        /// <param name="current">目前保存在gridHistory列上的顯示快照，不可為null。</param>
        /// <param name="incoming">本次Controller提供的顯示快照，不可為null。</param>
        /// <returns>所有可見欄位與詳細文字均相同時回傳True；任一內容改變時回傳False。</returns>
        /// <remarks>
        /// UI：由frm_SqlOptimizer的SetIterationHistory直接使用，服務gridHistory與gridResultPreview。
        /// ResultPreview在同一完成時間與執行指標下視為不可變資料，避免輪詢時重建相同DataTable繫結。
        /// </remarks>
        private static bool IterationDisplaysMatch(
            SqlOptimizerIterationDisplay current,
            SqlOptimizerIterationDisplay incoming)
        {
            // 1150820 codex しずくちゃん_GPT-5：以可見狀態判斷是否需要重繪，保留未變更的列與預覽資料來源。
            return string.Equals(current.IterationId, incoming.IterationId, StringComparison.Ordinal) &&
                current.Round == incoming.Round &&
                current.Candidate == incoming.Candidate &&
                string.Equals(current.Status, incoming.Status, StringComparison.Ordinal) &&
                current.Cost == incoming.Cost &&
                current.ElapsedMilliseconds == incoming.ElapsedMilliseconds &&
                current.Rows == incoming.Rows &&
                string.Equals(current.PlanHash, incoming.PlanHash, StringComparison.Ordinal) &&
                current.UpdatedAt == incoming.UpdatedAt &&
                string.Equals(current.SqlDiff, incoming.SqlDiff, StringComparison.Ordinal) &&
                string.Equals(current.ExplainPlan, incoming.ExplainPlan, StringComparison.Ordinal) &&
                string.Equals(current.AiAnalysis, incoming.AiAnalysis, StringComparison.Ordinal) &&
                string.Equals(current.ActualPlan, incoming.ActualPlan, StringComparison.Ordinal);
        }

        private void ClearIterationDetails()
        {
            txtSqlDiff.Clear();
            txtExplainPlan.Clear();
            txtAiAnalysis.Clear();
            txtActualPlan.Clear();
            gridResultPreview.DataSource = null;
            gridResultPreview.Rows.Clear();
            lblResultNotice.Text = "結果プレビューはありません。バインド値と結果行はAgentへ送信されません。";
        }

        private void RunOnUiThread(Action action)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(action);
                return;
            }

            _suppressInputChanged = true;
            try
            {
                action();
            }
            finally
            {
                _suppressInputChanged = false;
            }
        }

        private static string GetStateText(SqlOptimizerFormState state)
        {
            return state switch
            {
                SqlOptimizerFormState.Draft => "Draft",
                SqlOptimizerFormState.Approved => "Approved",
                SqlOptimizerFormState.Running => "Running",
                SqlOptimizerFormState.AwaitingUser => "AwaitingUser",
                SqlOptimizerFormState.Accepted => "Accepted",
                SqlOptimizerFormState.Stopped => "Stopped",
                SqlOptimizerFormState.Error => "Error",
                _ => state.ToString()
            };
        }

        private void frm_SqlOptimizer_Load(object sender, EventArgs e)
        {

        }
    }

    public enum SqlOptimizerFormState
    {
        Draft,
        Approved,
        Running,
        AwaitingUser,
        Accepted,
        Stopped,
        Error
    }

    public sealed class SqlOptimizerFormInput
    {
        public SqlOptimizerFormInput(
            string hospitalArea,
            string sql,
            string optimizationGoal,
            int commandTimeoutSeconds,
            int previewRowLimit,
            int maxCandidatesPerRound,
            IReadOnlyList<SqlOptimizerBindRow> bindings)
        {
            HospitalArea = hospitalArea;
            Sql = sql;
            OptimizationGoal = optimizationGoal;
            CommandTimeoutSeconds = commandTimeoutSeconds;
            PreviewRowLimit = previewRowLimit;
            MaxCandidatesPerRound = maxCandidatesPerRound;
            Bindings = bindings;
        }

        public string HospitalArea { get; }

        public string Sql { get; }

        public string OptimizationGoal { get; }

        public int CommandTimeoutSeconds { get; }

        public int PreviewRowLimit { get; }

        public int MaxCandidatesPerRound { get; }

        public IReadOnlyList<SqlOptimizerBindRow> Bindings { get; }
    }

    public sealed class SqlOptimizerBindRow
    {
        public SqlOptimizerBindRow(string name, string oracleType, bool isNull, string? value, int? size)
        {
            Name = name;
            OracleType = oracleType;
            IsNull = isNull;
            Value = value;
            Size = size;
        }

        public string Name { get; }

        public string OracleType { get; }

        public bool IsNull { get; }

        public string? Value { get; }

        public int? Size { get; }
    }

    public sealed class SqlOptimizerIterationDisplay
    {
        public string IterationId { get; set; } = string.Empty;

        public int Round { get; set; }

        public int Candidate { get; set; }

        public string Status { get; set; } = string.Empty;

        public long? Cost { get; set; }

        public long? ElapsedMilliseconds { get; set; }

        public long? Rows { get; set; }

        public string PlanHash { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public string SqlDiff { get; set; } = string.Empty;

        public string ExplainPlan { get; set; } = string.Empty;

        public string AiAnalysis { get; set; } = string.Empty;

        public string ActualPlan { get; set; } = string.Empty;

        public DataTable? ResultPreview { get; set; }
    }

    public sealed class SqlOptimizerSavedSessionDisplay
    {
        public string SessionId { get; set; } = string.Empty;

        public string DisplayText { get; set; } = string.Empty;

        public override string ToString()
        {
            return DisplayText;
        }
    }
}
