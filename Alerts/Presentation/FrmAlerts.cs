using System.Data;
using Project_auto_push_to_gitlab.Alerts.Application;
using Project_auto_push_to_gitlab.Alerts.Core;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;

namespace Project_auto_push_to_gitlab.Alerts.Presentation;

/// <summary>B案三院區總覽畫面；只收集操作、委派Controller及呈現取得時點結果，明細欄寬依本頁資料自動調整。</summary>
/// <remarks>UI：主畫面告警功能與通知路由直接建立FrmAlerts；dgvResults與dgvHistory提供執行選擇，tabCampuses及分頁按鈕切換dgvDetails。資料讀取由AlertController及Service負責，本Form只處理顯示與輸入，不直接查詢DB。</remarks>
public partial class FrmAlerts : Form
{
    private AlertController? _controller;
    private Guid? _selectedRun;
    private int _page;
    private bool _rendering;
    private CancellationTokenSource? _details;
    private readonly CancellationTokenSource _closing = new();
    // 1151027 codex ツバサちゃん：各Grid的排序只保留於目前畫面，明細以欄名及型別判斷是否可沿用。
    private readonly Dictionary<DataGridView, (string Column, bool Descending)> _gridSorts = [];
    private (string Name, string DataType)[] _detailSchema = [];
    private (Guid RunId, string Campus, int Page)? _detailPage;
    public string? StartupError { get; set; }

    /// <summary>建立無外部副作用的Designer相容畫面。</summary>
    /// <remarks>UI：主畫面告警功能及通知點擊直接建立；不連線DB。</remarks>
    public FrmAlerts()
    {
        // 1151005 codex GPT-6_しずくちゃん：承認済みB案の画面を静的Designerで初期化します。
        InitializeComponent();
    }

    /// <summary>設定已初始化的Controller並訂閱結果更新。</summary>
    /// <param name="controller">不可為null，畫面不取得服務生命週期所有權。</param>
    /// <remarks>UI：Runtime直接呼叫；更新三院區卡片及全部Grid。</remarks>
    public void Attach(AlertController controller)
    {
        // 1151005 codex GPT-6_しずくちゃん：背景結果の通知をUIへ安全に接続します。
        _controller = controller;
        controller.Service.Changed += ServiceChanged;
        Render();
    }

    /// <summary>回應背景摘要變更，透過BeginInvoke轉回UI執行緒。</summary>
    /// <param name="sender">服務來源。</param><param name="e">無資料的事件引數。</param>
    /// <remarks>UI：FrmAlerts全部Grid及lblStatus間接更新。</remarks>
    private void ServiceChanged(object? sender, EventArgs e)
    {
        // 1151005 codex GPT-6_しずくちゃん：背景スレッドはControlを直接変更しません。
        if (!_closing.IsCancellationRequested && IsHandleCreated)
            BeginInvoke(new Action(async () => { Render(); await RenderDetailsAsync(); }));
    }

    /// <summary>依搜尋輸入取得Dashboard並更新三個摘要Grid與院區卡片，重新套用各自排序及固定ID選取。</summary>
    /// <remarks>UI：FrmAlerts的txtSearch、cmbCategory、chkDate、dtpFrom及dtpTo直接使用，背景ServiceChanged間接使用；顯示於dgvResults、dgvRules、dgvHistory及三院區卡片。只轉換既有摘要，不重新執行SQL或保存排序。</remarks>
    private void Render()
    {
        // 1151005 codex GPT-6_しずくちゃん：検索条件と最新の集計だけを表示へ反映します。
        if (_controller is null || _rendering || IsDisposed) return;
        _rendering = true;
        try
        {
            string category = cmbCategory.Text == "全部分類" ? "" : cmbCategory.Text;
            string[] categories = new[] { "全部分類" }.Concat(_controller.Service.Rules().Select(rule => rule.Category)
                .Concat(_controller.Service.Runs().Select(run => run.Category)).Distinct().Order()).ToArray();
            if (!cmbCategory.Items.Cast<string>().SequenceEqual(categories))
            {
                cmbCategory.Items.Clear();
                cmbCategory.Items.AddRange(categories);
                cmbCategory.SelectedItem = categories.Contains(category) ? category : "全部分類";
            }
            AlertDashboard dashboard = _controller.Dashboard(txtSearch.Text, category,
                chkDate.Checked ? dtpFrom.Value.Date : null, chkDate.Checked ? dtpTo.Value.Date : null);
            // 1151027 codex ツバサちゃん：使用可排序的獨立顯示表，依固定ID還原操作對象而非沿用列位置。
            Guid? selectedRule = SelectedId(dgvRules);
            Guid? selectedHistory = SelectedId(dgvHistory);
            if (!_selectedRun.HasValue && dashboard.Latest.Count > 0) _selectedRun = dashboard.Latest[0].Id;
            BindGrid(dgvResults, AlertGridData.Results(dashboard.Latest), _selectedRun);
            BindGrid(dgvRules, AlertGridData.Rules(dashboard.Rules, DateTimeOffset.UtcNow), selectedRule);
            BindGrid(dgvHistory, AlertGridData.History(dashboard.History), selectedHistory);
            foreach ((CampusSummaryCard card, AlertCampusSummary summary) in new[] { cardKTGH00, cardHPK210, cardTSHIS }.Zip(dashboard.Campuses))
            {
                card.CountText = summary.Anomalies.ToString("N0");
                card.StateText = string.Join("／", new[] { summary.Errors > 0 ? $"取得失敗 {summary.Errors}項" : null,
                    summary.Pending > 0 ? $"未完成 {summary.Pending}項" : null }.Where(value => value is not null));
                bool hasResult = dashboard.Latest.Any(run => run.Campuses.Any(result => result.Campus == summary.Campus));
                if (card.StateText.Length == 0) card.StateText = hasResult ? "筆異常" : "尚無結果";
                bool abnormal = summary.Errors > 0 || summary.Anomalies > 0;
                card.BackColor = abnormal ? summary.Campus == "HPK210" ? Color.FromArgb(255, 248, 230) : Color.FromArgb(255, 241, 239)
                    : summary.Pending > 0 || !hasResult ? Color.FromArgb(241, 244, 248) : Color.FromArgb(238, 249, 244);
                card.ForeColor = abnormal ? summary.Campus == "HPK210" ? Color.FromArgb(150, 91, 0) : Color.FromArgb(197, 43, 35)
                    : summary.Pending > 0 || !hasResult ? Color.FromArgb(75, 91, 105) : Color.FromArgb(24, 125, 69);
            }
            lblStatus.Text = _controller.Service.SystemError ?? StartupError ?? "台北時間排程　｜　歷史保存：90天　｜　通知及明細僅儲存於本機";
        }
        finally { _rendering = false; }
    }

    /// <summary>將獨立顯示表繫結至指定Grid，隱藏比較鍵並還原目前畫面排序及選取。</summary>
    /// <param name="grid">FrmAlerts擁有的非null Grid；只修改其顯示與選取，不改變其他Grid。</param>
    /// <param name="data">不可為null的新顯示表；會套用DefaultView排序，不修改原始模型或保存資料。</param>
    /// <param name="selectedKey">摘要固定Guid或明細原頁列序；管理及履歷清單首次無鍵時沿用先頭列選取，其他null或已不存在的鍵表示清除選取。</param>
    /// <remarks>UI：FrmAlerts的Render與RenderDetailsAsync間接使用dgvResults、dgvRules、dgvHistory與dgvDetails。列標題採DataColumn.Caption；Id及__開頭欄位只供內部識別，不顯示。不重算欄寬或查詢DB。</remarks>
    private void BindGrid(DataGridView grid, DataTable data, object? selectedKey)
    {
        // 1151027 codex ツバサちゃん：繫結期間停用選取回呼，避免暫時的首列選取覆蓋既有明細。
        bool wasRendering = _rendering;
        _rendering = true;
        try
        {
            grid.DataSource = data;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                string name = column.DataPropertyName;
                column.Visible = name != "Id" && !name.StartsWith("__", StringComparison.Ordinal);
                column.SortMode = column.Visible ? DataGridViewColumnSortMode.Programmatic : DataGridViewColumnSortMode.NotSortable;
                column.HeaderText = data.Columns[name]!.Caption;
                if (grid == dgvDetails) column.DefaultCellStyle.NullValue = "NULL";
            }
            if (_gridSorts.TryGetValue(grid, out var sort) && data.Columns.Contains(sort.Column))
            {
                AlertGridData.Sort(data, sort.Column, sort.Descending);
                SetSortGlyph(grid, sort.Column, sort.Descending);
            }
            else
            {
                _gridSorts.Remove(grid);
                SetSortGlyph(grid, null, false);
            }
            // 1151027 codex ツバサちゃん：管理及履歷清單首次顯示時保留原有先頭列選取行為。
            if (selectedKey is null && (grid == dgvRules || grid == dgvHistory) && data.DefaultView.Count > 0)
                selectedKey = data.DefaultView[0]["Id"];
            RestoreGridSelection(grid, selectedKey);
        }
        finally { _rendering = wasRendering; }
    }

    /// <summary>回應四個Grid的左鍵表頭點擊，以欄位識別切換昇降冪並保持操作對象與欄寬。</summary>
    /// <param name="sender">來源Grid；允許null或其他物件，此時不處理，不修改來源以外的控制項。</param>
    /// <param name="e">不可為null的表頭滑鼠事件；只處理有效可見欄位的左鍵，不修改事件。</param>
    /// <remarks>UI：FrmAlerts的dgvResults、dgvRules、dgvHistory及dgvDetails之ColumnHeaderMouseClick直接使用。只排序目前繫結資料；明細限目前頁，無DB操作或欄寬重設，失敗訊息顯示於lblStatus。</remarks>
    private void GridColumnHeaderClicked(object? sender, DataGridViewCellMouseEventArgs e)
    {
        // 1151027 codex ツバサちゃん：表頭首次點擊昇冪，再次點擊同欄切換降冪，選取用固定鍵還原。
        if (_rendering || sender is not DataGridView grid || e.Button != MouseButtons.Left
            || e.ColumnIndex < 0 || e.ColumnIndex >= grid.Columns.Count || !grid.Columns[e.ColumnIndex].Visible
            || grid.DataSource is not DataTable data) return;
        string name = grid.Columns[e.ColumnIndex].DataPropertyName;
        bool descending = _gridSorts.TryGetValue(grid, out var previous) && previous.Column == name && !previous.Descending;
        object? key = SelectedRowKey(grid);
        bool wasRendering = _rendering;
        _rendering = true;
        try
        {
            AlertGridData.Sort(data, name, descending);
            _gridSorts[grid] = (name, descending);
            SetSortGlyph(grid, name, descending);
            RestoreGridSelection(grid, key);
        }
        catch (Exception exception) { lblStatus.Text = AlertErrors.ForOperation(exception, "排序"); }
        finally { _rendering = wasRendering; }
    }

    /// <summary>設定目前排序欄的方向箭頭，並清除其他欄位的舊箭頭。</summary>
    /// <param name="grid">非null的來源Grid；只修改該Grid的表頭箭頭。</param>
    /// <param name="columnName">內部DataPropertyName；null表示全部清除，不以可重複的顯示欄名識別。</param>
    /// <param name="descending">True顯示降冪箭頭，False顯示昇冪箭頭；清除時不使用。</param>
    /// <remarks>UI：FrmAlerts的BindGrid與GridColumnHeaderClicked間接更新四個Grid；不變更資料、欄寬或選取。</remarks>
    private static void SetSortGlyph(DataGridView grid, string? columnName, bool descending)
    {
        // 1151027 codex ツバサちゃん：以內部欄位名稱標示方向，重複表頭不會標錯欄。
        foreach (DataGridViewColumn column in grid.Columns)
            column.HeaderCell.SortGlyphDirection = column.Visible && column.DataPropertyName == columnName
                ? descending ? SortOrder.Descending : SortOrder.Ascending : SortOrder.None;
    }

    /// <summary>取得目前選取摘要的固定ID或明細的原頁列序，避免排序後依列位置操作錯誤資料。</summary>
    /// <param name="grid">不可為null的來源Grid；不修改資料或選取。</param>
    /// <returns>摘要的Guid或明細的原頁列序；無目前列、無繫結或無識別欄位時回傳null。</returns>
    /// <remarks>UI：FrmAlerts的GridColumnHeaderClicked與RenderDetailsAsync間接讀取四個Grid；明細識別只在同一執行、院區及頁碼內有效。</remarks>
    private static object? SelectedRowKey(DataGridView grid)
    {
        // 1151027 codex ツバサちゃん：從DataRowView讀取非顯示鍵，不使用排序後的索引。
        string name = grid.Name == "dgvDetails" ? "__order" : "Id";
        return grid.CurrentRow?.DataBoundItem is DataRowView row && row.DataView.Table!.Columns.Contains(name)
            ? row[name] : null;
    }

    /// <summary>依原始識別鍵還原選取列與目前儲存格，不存在的鍵保持未選取。</summary>
    /// <param name="grid">非null的來源Grid；修改其目前列及選取，不改變資料。</param>
    /// <param name="key">固定Guid或本頁原列序；null時清除選取。</param>
    /// <remarks>UI：FrmAlerts的BindGrid與GridColumnHeaderClicked間接操作四個Grid；由呼叫端抑制SelectionChanged，避免排序觸發其他資料讀取。</remarks>
    private static void RestoreGridSelection(DataGridView grid, object? key)
    {
        // 1151027 codex ツバサちゃん：先清除暫時選取，再依固定鍵定位第一個可見儲存格。
        grid.CurrentCell = null;
        grid.ClearSelection();
        if (key is null) return;
        string name = grid.Name == "dgvDetails" ? "__order" : "Id";
        DataGridViewColumn? first = grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(column => column.Visible);
        if (first is null) return;
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.DataBoundItem is not DataRowView data || !data.DataView.Table!.Columns.Contains(name) || !Equals(data[name], key)) continue;
            grid.CurrentCell = row.Cells[first.Index];
            row.Selected = true;
            break;
        }
    }

    /// <summary>首次顯示讀取保存摘要；服務未就緒時停用寫入與執行，不覆寫損壞設定。</summary>
    /// <param name="sender">本Form。</param><param name="e">事件引數。</param>
    /// <remarks>UI：Shown直接使用，更新lblStatus及全部操作按鈕。</remarks>
    private async void ViewShown(object? sender, EventArgs e)
    {
        // 1151005 codex GPT-6_しずくちゃん：初期化失敗時は設定を上書きできない表示にします。
        if (_controller is null)
        {
            lblStatus.Text = StartupError ?? "告警服務尚未初始化。";
            btnNew.Enabled = btnRun.Enabled = btnEdit.Enabled = btnDuplicate.Enabled = btnToggle.Enabled = false;
            return;
        }
        Render();
        await RenderDetailsAsync();
    }

    /// <summary>搜尋條件變更時重新呈現Dashboard並更新選取明細。</summary>
    /// <param name="sender">搜尋控制項。</param><param name="e">事件引數。</param>
    /// <remarks>UI：txtSearch、cmbCategory、chkDate、dtpFrom及dtpTo直接使用。</remarks>
    private async void FilterChanged(object? sender, EventArgs e)
    {
        // 1151005 codex GPT-6_しずくちゃん：検索後の明細を表示条件へ合わせます。
        if (_rendering) return;
        _selectedRun = null;
        _page = 0;
        Render();
        await RenderDetailsAsync();
    }

    /// <summary>從結果或履歷選取取得時點的摘要，不重新執行SQL。</summary>
    /// <param name="sender">結果或履歷Grid。</param><param name="e">事件引數。</param>
    /// <remarks>UI：dgvResults及dgvHistory直接使用，輸出到dgvDetails與院區頁籤。</remarks>
    private async void SelectionChanged(object? sender, EventArgs e)
    {
        // 1151005 codex GPT-6_しずくちゃん：過去の取得結果を再照会せず選択します。
        if (_rendering || sender is not DataGridView grid || SelectedId(grid) is not Guid id) return;
        _selectedRun = id;
        _page = 0;
        await RenderDetailsAsync();
    }

    /// <summary>雙擊歷史列開啟取得時點明細，不重新查詢DB。</summary>
    /// <param name="sender">dgvHistory。</param><param name="e">雙擊列位置，表頭不處理。</param>
    /// <remarks>UI：dgvHistory的CellDoubleClick直接使用，切換tabResults並顯示dgvDetails。</remarks>
    private async void HistoryOpened(object? sender, DataGridViewCellEventArgs e)
    {
        // 1151005 codex GPT-6_しずくちゃん：履歴の行からその取得時点の明細へ移動します。
        if (e.RowIndex >= 0 && SelectedId(dgvHistory) is Guid id) await OpenRunAsync(id);
    }

    /// <summary>依院區及頁碼讀取最多200筆明細，沿用相同欄名與型別的頁面排序，並依本頁內容調整欄寬；取消過期讀取避免覆蓋新選擇。</summary>
    /// <returns>本次畫面更新完成Task；查無資料時顯示空明細，讀取失敗以狀態文字顯示，不回傳null。</returns>
    /// <remarks>UI：FrmAlerts的tabCampuses、btnPrevious及btnNext直接使用，dgvResults與dgvHistory經選取流程間接使用；明細及欄寬顯示於dgvDetails，摘要與頁數顯示於lblDetails與lblPage。只讀取既有保存明細，不查詢DB或改變保存內容。排序限當頁；欄位結構不同時解除排序，同一執行、院區及頁碼更新時才還原原頁列序選取。</remarks>
    private async Task RenderDetailsAsync()
    {
        // 1151005 codex GPT-6_しずくちゃん：復号は一ページだけ行い古い表示要求を取り消します。
        _details?.Cancel();
        _details?.Dispose();
        _details = CancellationTokenSource.CreateLinkedTokenSource(_closing.Token);
        CancellationToken token = _details.Token;
        if (_controller is null) return;
        AlertRun? run = _controller.Service.Runs().Find(run => run.Id == _selectedRun);
        // 1151027 codex ツバサちゃん：暫存目前明細選取鍵，僅在同一頁更新時還原。
        object? selectedDetail = SelectedRowKey(dgvDetails);
        (Guid RunId, string Campus, int Page)? previousPage = _detailPage;
        dgvDetails.DataSource = null;
        btnPrevious.Enabled = false;
        btnNext.Enabled = false;
        lblPage.Text = "0 / 0";
        if (run is null) { lblDetails.Text = "請選擇告警或執行紀錄"; return; }
        for (int index = 0; index < 3; index++)
        {
            string campus = AlertSchedule.CampusOrder[index];
            tabCampuses.TabPages[index].Text = campus + "（" + AlertController.Status(run.Campuses.Find(result => result.Campus == campus)) + "）";
        }
        string selected = AlertSchedule.CampusOrder[Math.Max(0, tabCampuses.SelectedIndex)];
        AlertCampusResult? result = run.Campuses.Find(result => result.Campus == selected);
        lblDetails.Text = $"{run.Name}　｜　分類：{run.Category}　｜　執行：{TimeZoneInfo.ConvertTime(run.StartedAt, AlertSchedule.Taipei):yyyy/MM/dd HH:mm}　｜　用途：{run.Purpose}";
        if (result is null) { lblPage.Text = "未選取此院區"; return; }
        if (result.Status is not (AlertCampusStatus.Normal or AlertCampusStatus.Anomaly))
        { lblPage.Text = result.Error ?? AlertController.Status(result); return; }
        try
        {
            List<string?[]> rows = await _controller.Service.ReadPageAsync(run, selected, _page, token);
            if (token.IsCancellationRequested || IsDisposed) return;
            // 1151027 codex ツバサちゃん：以內部位置識別重複表頭，結構相同才沿用本頁排序。
            (string Name, string DataType)[] schema = result.Columns.Select(column => (column.Name, column.DataType)).ToArray();
            if (!_detailSchema.SequenceEqual(schema)) _gridSorts.Remove(dgvDetails);
            _detailSchema = schema;
            _detailPage = (run.Id, selected, _page);
            BindGrid(dgvDetails, AlertGridData.Details(result.Columns, rows), previousPage == _detailPage ? selectedDetail : null);
            // 1151027 codex しずくちゃん：實際欄名與NULL顯示設定完成後，再計算本頁自動欄寬。
            ResizeDetailColumns();
            long total = (result.RowCount + 199) / 200;
            lblPage.Text = $"{(total == 0 ? 0 : _page + 1)} / {total:N0}　（{result.RowCount:N0}筆）";
            btnPrevious.Enabled = _page > 0;
            btnNext.Enabled = _page + 1 < total;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { /* 表示要求已被新選擇或關閉取代。 */ }
        catch (Exception exception) { lblPage.Text = AlertErrors.ForOperation(exception, "明細讀取"); }
    }

    /// <summary>依目前明細頁的可見欄名與全部已載入資料計算欄寬，略過隱藏排序鍵，避免大量欄位平均壓縮及長文無限擴寬。</summary>
    /// <remarks>UI：FrmAlerts的dgvDetails由RenderDetailsAsync間接使用；輸入為目前頁面最多200筆明細及實際欄名。以96 DPI的80至360像素為自動寬度範圍，依Grid的DPI縮放；重新顯示明細時重算，手動調整不保存。不改變欄位順序、內容、NULL或其他Grid。</remarks>
    private void ResizeDetailColumns()
    {
        // 1151027 codex しずくちゃん：重用Grid原生內容量測，限制自動寬度並保留手動調整及橫向捲動。
        int minimumWidth = (int)Math.Round(80 * dgvDetails.DeviceDpi / 96d);
        int maximumWidth = (int)Math.Round(360 * dgvDetails.DeviceDpi / 96d);
        foreach (DataGridViewColumn column in dgvDetails.Columns)
        {
            // 1151027 codex ツバサちゃん：隱藏排序順位不參與使用者可見欄位的寬度量測。
            if (!column.Visible) continue;
            int preferredWidth = column.GetPreferredWidth(DataGridViewAutoSizeColumnMode.AllCells, true);
            column.MinimumWidth = minimumWidth;
            column.Width = Math.Clamp(preferredWidth, minimumWidth, maximumWidth);
        }
    }

    /// <summary>通知點擊時開啟指定履歷；已到期時顯示明確訊息。</summary>
    /// <param name="id">通知履歷ID；null表示開啟總覽。</param>
    /// <returns>畫面及明細顯示完成Task。</returns><remarks>UI：Program啟動路由直接使用，顯示tabs及dgvDetails。</remarks>
    public async Task OpenRunAsync(Guid? id)
    {
        // 1151005 codex GPT-6_しずくちゃん：通知で指定された取得時点の履歴を開きます。
        if (id.HasValue) { _selectedRun = id; _page = 0; }
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        tabs.SelectedTab = tabResults;
        Render();
        if (id.HasValue)
        {
            if (_controller?.Service.Runs().Any(run => run.Id == id) != true)
                lblStatus.Text = "指定履歷已到期或無法讀取。";
        }
        await RenderDetailsAsync();
    }

    /// <summary>取得Grid目前選擇的隱藏識別碼。</summary>
    /// <param name="grid">非null的摘要或設定Grid，不修改。</param><returns>已選擇ID；無選擇為null。</returns>
    /// <remarks>UI：dgvResults、dgvHistory及dgvRules直接使用。</remarks>
    private static Guid? SelectedId(DataGridView grid)
    {
        // 1151005 codex GPT-6_しずくちゃん：画面上の行を固定IDで識別します。
        return grid.CurrentRow?.Cells["Id"].Value is Guid id ? id : null;
    }

    /// <summary>將新增、編輯或複製操作委派編輯畫面，保存後更新總覽。</summary>
    /// <param name="sender">btnNew、btnEdit或btnDuplicate。</param><param name="e">事件引數。</param>
    /// <remarks>UI：開啟FrmAlertRule，輸入來自dgvRules選擇。</remarks>
    private void EditClicked(object? sender, EventArgs e)
    {
        // 1151005 codex GPT-6_しずくちゃん：ルール編集だけを専用の画面に委譲します。
        if (_controller is null) return;
        AlertRule rule = new();
        if (sender != btnNew)
        {
            if (SelectedId(dgvRules) is not Guid id) return;
            rule = sender == btnDuplicate ? _controller.Duplicate(id) : _controller.Service.Rules().Single(rule => rule.Id == id);
        }
        using FrmAlertRule editor = new();
        editor.Attach(_controller, rule);
        editor.ShowDialog(this);
        Render();
    }

    /// <summary>切換已保存規則的啟用狀態，排程起點由Service設定。</summary>
    /// <param name="sender">btnToggle。</param><param name="e">事件引數。</param>
    /// <remarks>UI：dgvRules選擇及btnToggle直接使用。</remarks>
    private async void ToggleClicked(object? sender, EventArgs e)
    {
        // 1151005 codex GPT-6_しずくちゃん：有効化は保存済み規則へ明示的に行います。
        if (_controller is null || SelectedId(dgvRules) is not Guid id) return;
        try
        {
            AlertRule rule = _controller.Service.Rules().Single(rule => rule.Id == id);
            rule.Enabled = !rule.Enabled;
            await _controller.Service.SaveRuleAsync(rule, _closing.Token);
            Render();
        }
        catch (Exception exception) { lblStatus.Text = AlertErrors.ForOperation(exception, "規則啟用"); }
    }

    /// <summary>將選取規則的手動執行委派Service，UI保持可操作並顯示保存結果。</summary>
    /// <param name="sender">btnRun。</param><param name="e">事件引數。</param>
    /// <remarks>UI：dgvRules或dgvResults提供ID，完成後更新dgvDetails。</remarks>
    private async void RunClicked(object? sender, EventArgs e)
    {
        // 1151005 codex GPT-6_しずくちゃん：手動実行を待つ間も画面を止めません。
        if (_controller is null) return;
        Guid? id = tabs.SelectedTab == tabRules ? SelectedId(dgvRules)
            : _controller.Service.Runs().Find(run => run.Id == _selectedRun)?.RuleId;
        if (!id.HasValue) { lblStatus.Text = "請在SQL規則管理選擇規則。"; return; }
        btnRun.Enabled = false;
        try
        {
            AlertRun run = await _controller.Service.RunManualAsync(id.Value, _closing.Token);
            await OpenRunAsync(run.Id);
        }
        catch (OperationCanceledException) when (_closing.IsCancellationRequested) { /* 關閉時已取消等待。 */ }
        catch (Exception exception) { lblStatus.Text = AlertErrors.ForOperation(exception, "手動執行"); }
        finally { if (!IsDisposed) btnRun.Enabled = true; }
    }

    /// <summary>處理院區及分頁操作，只更新取得時點明細。</summary>
    /// <param name="sender">院區頁籤或分頁按鈕。</param><param name="e">事件引數。</param>
    /// <remarks>UI：tabCampuses、btnPrevious、btnNext直接使用；更新dgvDetails。</remarks>
    private async void PageChanged(object? sender, EventArgs e)
    {
        // 1151005 codex GPT-6_しずくちゃん：院区変更時は先頭、ページ操作時は指定頁を表示します。
        if (_rendering) return;
        _page = sender == btnNext ? _page + 1 : sender == btnPrevious ? Math.Max(0, _page - 1) : 0;
        await RenderDetailsAsync();
    }

    /// <summary>關閉畫面只取消UI明細讀取，不停止常駐告警排程。</summary>
    /// <param name="sender">本Form。</param><param name="e">事件引數。</param>
    /// <remarks>UI：FrmAlerts的FormClosed直接使用；解除服務事件以免操作已Dispose控制項。</remarks>
    private void ViewClosed(object? sender, FormClosedEventArgs e)
    {
        // 1151005 codex GPT-6_しずくちゃん：閉じた画面のイベントと表示要求を解放します。
        if (_controller is not null) _controller.Service.Changed -= ServiceChanged;
        _closing.Cancel();
        _details?.Cancel();
    }
}
