using Project_auto_push_to_gitlab.Alerts.Application;
using Project_auto_push_to_gitlab.Alerts.Core;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;

namespace Project_auto_push_to_gitlab.Alerts.Presentation;

/// <summary>告警新增與編輯畫面，提供每週、每天及每小時的排程輸入；只收集欄位並委派Service，驗證與DB操作不放在事件內。</summary>
/// <remarks>UI：FrmAlerts的btnNew、btnEdit及btnDuplicate直接建立；cmbFrequency切換cmbDay與dtpTime的顯示，btnSave及btnTest委派AlertService。</remarks>
public partial class FrmAlertRule : Form
{
    private AlertController? _controller;
    private AlertRule _rule = new();
    private readonly CancellationTokenSource _closing = new();

    /// <summary>建立Designer相容的編輯畫面，預設每週一及Designer設定的時刻；不啟動服務、讀檔或DB連線。</summary>
    /// <remarks>UI：FrmAlerts的btnNew、btnEdit及btnDuplicate直接建立；初始化FrmAlertRule的cmbFrequency、cmbDay及dtpTime。</remarks>
    public FrmAlertRule()
    {
        // 1151005 codex GPT-6_しずくちゃん：設定入力と処理サービスを分離した編集画面を作ります。
        InitializeComponent();
        cmbDay.SelectedIndex = 0;
        cmbFrequency.SelectedIndex = (int)AlertScheduleFrequency.Weekly;
    }

    /// <summary>以獨立規則快照填入編輯欄位及排程頻率，SQL未設定時保持空白；不儲存規則或執行查詢。</summary>
    /// <param name="controller">已初始化的Controller，不可為null；不變更服務設定。</param>
    /// <param name="rule">不可為null的獨立編輯快照；不更動傳入物件。</param>
    /// <remarks>UI：FrmAlerts的btnNew、btnEdit及btnDuplicate直接呼叫；顯示至FrmAlertRule的txtName、txtPurpose、txtCategory、txtSql、chkEnabled、chkKTGH00、chkHPK210、chkTSHIS、cmbFrequency、cmbDay、dtpTime及dgvParameters。</remarks>
    public void Attach(AlertController controller, AlertRule rule)
    {
        // 1151005 codex GPT-6_しずくちゃん：新規SQLへサンプルを注入せず保存済み値だけを表示します。
        _controller = controller;
        _rule = rule;
        lblId.Text = "ID：" + rule.Id.ToString("D");
        txtName.Text = rule.Name;
        txtCategory.Text = rule.Category;
        txtPurpose.Text = rule.Purpose;
        txtSql.Text = rule.Sql;
        chkKTGH00.Checked = rule.Campuses.Contains("KTGH00");
        chkHPK210.Checked = rule.Campuses.Contains("HPK210");
        chkTSHIS.Checked = rule.Campuses.Contains("TSHIS");
        chkEnabled.Checked = rule.Enabled;
        chkEnabled.Enabled = controller.Service.Rules().Any(item => item.Id == rule.Id);
        cmbDay.SelectedIndex = ((int)rule.Day + 6) % 7;
        cmbFrequency.SelectedIndex = (int)rule.Frequency;
        dtpTime.Value = DateTime.Today.Add(rule.Time);
        foreach (AlertParameter parameter in rule.Parameters)
            dgvParameters.Rows.Add(parameter.Name, parameter.DataType.ToString(), parameter.Source.ToString(),
                parameter.DateFormat.ToString(), parameter.Value, parameter.IsNull, parameter.Size?.ToString());
    }

    /// <summary>讀取全部UI輸入組成獨立規則；每小時只保存指定分鐘，不把隱藏小時混入預定。不執行驗證、SQL或保存。</summary>
    /// <returns>可由Service驗證的規則，包含頻率、星期、時間及完整參數；不回傳null、不更動原規則。</returns>
    /// <exception cref="FormatException">參數Size不是整數時拋出。</exception>
    /// <remarks>UI：FrmAlertRule的btnSave及btnTest經SubmitClicked直接使用；排程輸入來自cmbFrequency、cmbDay及dtpTime，其餘輸入來自txtName、txtCategory、txtPurpose、txtSql、chkEnabled、chkKTGH00、chkHPK210、chkTSHIS及dgvParameters。</remarks>
    private AlertRule ReadInput()
    {
        // 1151005 codex GPT-6_しずくちゃん：入力取得だけを行い業務検証はサービスへ渡します。
        dgvParameters.EndEdit();
        AlertScheduleFrequency frequency = (AlertScheduleFrequency)cmbFrequency.SelectedIndex;
        AlertRule rule = new() { Id = _rule.Id, Version = _rule.Version, Name = txtName.Text.Trim(),
            Category = txtCategory.Text.Trim(), Purpose = txtPurpose.Text.Trim(), Sql = txtSql.Text,
            Enabled = chkEnabled.Checked, Frequency = frequency, Day = (DayOfWeek)((cmbDay.SelectedIndex + 1) % 7),
            Time = frequency == AlertScheduleFrequency.Hourly ? TimeSpan.FromMinutes(dtpTime.Value.Minute) : dtpTime.Value.TimeOfDay,
            Campuses = [], Parameters = [] };
        if (chkKTGH00.Checked) rule.Campuses.Add("KTGH00");
        if (chkHPK210.Checked) rule.Campuses.Add("HPK210");
        if (chkTSHIS.Checked) rule.Campuses.Add("TSHIS");
        foreach (DataGridViewRow row in dgvParameters.Rows)
        {
            if (row.IsNewRow) continue;
            string size = Convert.ToString(row.Cells["colSize"].Value) ?? "";
            rule.Parameters.Add(new AlertParameter {
                Name = Convert.ToString(row.Cells["colName"].Value) ?? "",
                DataType = Enum.Parse<OracleBindDataType>(Convert.ToString(row.Cells["colType"].Value) ?? "Varchar2"),
                Source = Enum.Parse<AlertParameterSource>(Convert.ToString(row.Cells["colSource"].Value) ?? "Fixed"),
                DateFormat = Enum.Parse<AlertDateFormat>(Convert.ToString(row.Cells["colFormat"].Value) ?? "Gregorian"),
                Value = Convert.ToString(row.Cells["colValue"].Value), IsNull = row.Cells["colNull"].Value is true,
                Size = string.IsNullOrWhiteSpace(size) ? null : int.Parse(size) });
        }
        return rule;
    }

    /// <summary>依選取頻率切換排程輸入；每週顯示星期及時間，每天顯示時間，每小時只顯示分鐘。</summary>
    /// <param name="sender">FrmAlertRule的cmbFrequency；事件允許null，方法不修改來源物件。</param>
    /// <param name="e">選項變更事件引數，不修改內容。</param>
    /// <remarks>UI：FrmAlertRule的cmbFrequency直接使用；僅切換cmbDay的可見性及dtpTime的格式與寬度，不更動輸入時間或執行服務。</remarks>
    private void ScheduleFrequencyChanged(object? sender, EventArgs e)
    {
        AlertScheduleFrequency frequency = (AlertScheduleFrequency)cmbFrequency.SelectedIndex;
        cmbDay.Visible = frequency == AlertScheduleFrequency.Weekly;
        dtpTime.CustomFormat = frequency == AlertScheduleFrequency.Hourly ? "'每小時 'mm' 分'" : "HH:mm";
        dtpTime.Width = frequency == AlertScheduleFrequency.Hourly ? 160 : 110;
    }

    /// <summary>將保存或測試操作委派服務，顯示輸入錯誤並避免重複按鈕操作。</summary>
    /// <param name="sender">btnSave或btnTest。</param><param name="e">事件引數。</param>
    /// <remarks>UI：全部編輯欄位直接使用；測試僅讀取DB並Rollback，結果保存至履歷。</remarks>
    private async void SubmitClicked(object? sender, EventArgs e)
    {
        // 1151005 codex GPT-6_しずくちゃん：保存とテストは同じ入力からサービスへ委譲します。
        if (_controller is null) return;
        btnSave.Enabled = btnTest.Enabled = false;
        try
        {
            AlertRule input = ReadInput();
            if (sender == btnTest)
            {
                AlertRun run = await _controller.Service.RunTestAsync(input, _closing.Token);
                lblMessage.Text = string.Join("　", run.Campuses.Select(result => result.Campus + "：" + AlertController.Status(result)));
            }
            else
            {
                _rule = await _controller.Service.SaveRuleAsync(input, _closing.Token);
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (OperationCanceledException) when (_closing.IsCancellationRequested) { /* 編輯畫面關閉已取消。 */ }
        catch (Exception exception)
        {
            lblMessage.Text = exception switch
            {
                SqlValidationException => "SQL或參數無效。只允許單一SELECT／WITH … SELECT，所有具名參數須完整且不重複。",
                FormatException or OverflowException => "參數值或長度格式無效，請確認型別及固定值。",
                ArgumentException argument when argument.Message.StartsWith("請", StringComparison.Ordinal)
                    || argument.Message.StartsWith("排程", StringComparison.Ordinal)
                    || argument.Message.StartsWith("參數", StringComparison.Ordinal)
                    || argument.Message.StartsWith("自動", StringComparison.Ordinal) => argument.Message,
                ArgumentException => "參數名稱、型別、NULL或長度設定無效。",
                _ => AlertErrors.ForOperation(exception, "告警規則處理")
            };
        }
        finally { if (!IsDisposed) btnSave.Enabled = btnTest.Enabled = true; }
    }

    /// <summary>新增預設固定參數列；不填入真實業務值。</summary>
    /// <param name="sender">btnAddParameter。</param><param name="e">事件引數。</param>
    /// <remarks>UI：dgvParameters直接使用，後續由使用者選擇來源與型別。</remarks>
    private void AddParameterClicked(object? sender, EventArgs e)
    {
        // 1151005 codex GPT-6_しずくちゃん：空の具名パラメーター行を追加します。
        dgvParameters.Rows.Add("", "Varchar2", "Fixed", "Gregorian", "", false, null);
    }

    /// <summary>移除目前選擇的參數列，不變更其他列。</summary>
    /// <param name="sender">btnRemoveParameter。</param><param name="e">事件引數。</param>
    /// <remarks>UI：dgvParameters與btnRemoveParameter直接使用。</remarks>
    private void RemoveParameterClicked(object? sender, EventArgs e)
    {
        // 1151005 codex GPT-6_しずくちゃん：選択された設定行だけを取り除きます。
        if (dgvParameters.CurrentRow is { IsNewRow: false } row) dgvParameters.Rows.Remove(row);
    }

    /// <summary>取消編輯視窗發起的測試讀取，Service仍負責Rollback及保存中斷狀態。</summary>
    /// <param name="sender">本Form。</param><param name="e">事件引數。</param>
    /// <remarks>UI：FrmAlertRule的FormClosed直接使用；不停止常駐排程。</remarks>
    private void EditorClosed(object? sender, FormClosedEventArgs e)
    {
        // 1151005 codex GPT-6_しずくちゃん：閉じた編集画面のテスト要求を取り消します。
        _closing.Cancel();
    }
}
