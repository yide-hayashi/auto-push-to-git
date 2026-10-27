using Project_auto_push_to_gitlab.Alerts.Core;

namespace Project_auto_push_to_gitlab.Alerts.Application;

/// <summary>畫面用摘要資料，將結果模型與Grid表示分離，保留可擴充圖表的原始摘要。</summary>
public sealed class AlertDashboard
{
    // 1151005 codex GPT-6_しずくちゃん：分離畫面搜尋資料與執行摘要模型。
    public List<AlertRule> Rules { get; set; } = [];
    public List<AlertRun> Latest { get; set; } = [];
    public List<AlertRun> History { get; set; } = [];
    public List<AlertCampusSummary> Campuses { get; set; } = [];
}

/// <summary>依搜尋條件彙總每個規則最新結果的院區數字與未取得狀態。</summary>
public sealed class AlertCampusSummary
{
    // 1151005 codex GPT-6_しずくちゃん：分別保存院區異常件數、取得錯誤及尚未完成狀態。
    public string Campus { get; set; } = "";
    public long Anomalies { get; set; }
    public int Errors { get; set; }
    public int Pending { get; set; }
}

/// <summary>處理搜尋、院區彙總、規則複製與服務委派；不持有Form或Control。</summary>
public sealed class AlertController
{
    public AlertService Service { get; }

    /// <summary>建立告警畫面流程入口，不觸發查詢。</summary>
    /// <param name="service">已初始化的服務，不可為null。</param>
    /// <exception cref="ArgumentNullException">service為null時拋出。</exception>
    /// <remarks>UI：Runtime建立後注入FrmAlerts；txtSearch、cmbCategory、btnRun及FrmAlertRule的btnSave使用服務，輸出到dgvResults與dgvDetails。</remarks>
    public AlertController(AlertService service)
    {
        // 1151005 codex GPT-6_しずくちゃん：画面フローから実行サービスへ委譲します。
        Service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>搜尋規則及履歷並彙總各院區最新結果，未執行不當作正常。</summary>
    /// <param name="search">警告名稱或用途的搜尋字串，空值表示全部。</param>
    /// <param name="category">分類，空值表示全部。</param><param name="from">開始日，null不限制。</param>
    /// <param name="to">結束日，null不限制；包含該日。</param>
    /// <returns>篩選後的獨立顯示資料，不回傳null。</returns>
    /// <remarks>UI：FrmAlerts的txtSearch、cmbCategory、dtpFrom與dtpTo直接使用；輸出到三院區卡片與三個Grid。</remarks>
    public AlertDashboard Dashboard(string? search, string? category, DateTime? from, DateTime? to)
    {
        // 1151005 codex GPT-6_しずくちゃん：検索した一覧全体の最新結果をカードへ集計します。
        string query = search?.Trim() ?? "";
        bool Matches(string name, string purpose, string kind) =>
            (query.Length == 0 || name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || purpose.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            && (string.IsNullOrEmpty(category) || kind == category);
        List<AlertRule> rules = Service.Rules().Where(rule => Matches(rule.Name, rule.Purpose, rule.Category)).ToList();
        List<AlertRun> all = Service.Runs();
        List<AlertRun> history = all.Where(run => Matches(run.Name, run.Purpose, run.Category))
            .Where(run => (!from.HasValue || TimeZoneInfo.ConvertTime(run.StartedAt, AlertSchedule.Taipei).Date >= from.Value.Date)
                && (!to.HasValue || TimeZoneInfo.ConvertTime(run.StartedAt, AlertSchedule.Taipei).Date <= to.Value.Date)).ToList();
        List<AlertRun> latest = history.GroupBy(run => run.RuleId).Select(group => group.First()).ToList();
        List<AlertCampusSummary> campuses = AlertSchedule.CampusOrder.Select(campus =>
        {
            List<AlertCampusResult> results = latest.SelectMany(run => run.Campuses).Where(result => result.Campus == campus).ToList();
            return new AlertCampusSummary { Campus = campus,
                Anomalies = results.Where(result => result.Status == AlertCampusStatus.Anomaly).Sum(result => result.RowCount),
                Errors = results.Count(result => result.Status == AlertCampusStatus.Error),
                Pending = results.Count(result => result.Status is AlertCampusStatus.Pending or AlertCampusStatus.Interrupted)
                    + rules.Count(rule => rule.Campuses.Contains(campus) && latest.All(run => run.RuleId != rule.Id)) };
        }).ToList();
        return new AlertDashboard { Rules = rules, History = history, Latest = latest, Campuses = campuses };
    }

    /// <summary>建立新ID且停用的規則副本，不立即保存或執行。</summary>
    /// <param name="id">已保存的來源規則ID。</param><returns>獨立可編輯的新規則。</returns>
    /// <remarks>UI：FrmAlerts的btnDuplicate直接使用，輸出至FrmAlertRule全部欄位。</remarks>
    public AlertRule Duplicate(Guid id)
    {
        // 1151005 codex GPT-6_しずくちゃん：複製は別IDの無効な規則にします。
        AlertRule rule = Service.Rules().Single(rule => rule.Id == id);
        rule.Id = Guid.NewGuid();
        rule.Version = 1;
        rule.Name += "（複製）";
        rule.Enabled = false;
        rule.EligibleAfter = default;
        return rule;
    }

    /// <summary>將院區取得狀態轉為畫面使用的繁體中文。</summary>
    /// <param name="result">null表示尚未執行，不修改內容。</param><returns>狀態及件數文字。</returns>
    /// <remarks>UI：FrmAlerts的dgvResults、dgvHistory及院區頁籤直接使用。</remarks>
    public static string Status(AlertCampusResult? result)
    {
        // 1151005 codex GPT-6_しずくちゃん：エラーと未実行を正常ゼロ件から分けて表示します。
        return result?.Status switch
        {
            AlertCampusStatus.Normal => "正常 0",
            AlertCampusStatus.Anomaly => $"異常 {result.RowCount:N0}",
            AlertCampusStatus.Error => "取得失敗",
            AlertCampusStatus.Interrupted => "已中斷",
            AlertCampusStatus.Pending => "執行中",
            _ => "未執行"
        };
    }
}
