using Project_auto_push_to_gitlab.SqlOptimizer.Core;

namespace Project_auto_push_to_gitlab.Alerts.Core;

/// <summary>告警參數的值來源；日期皆以預定執行日計算。</summary>
/// <remarks>UI：FrmAlertRule的dgvParameters之colSource直接列出來源；新增來源只附加於末端，保留既有加密設定的數值對應。</remarks>
public enum AlertParameterSource
{
    Fixed, ScheduleDate, PreviousWeekMonday, ThisWeekMonday,
    // 1151027 codex しずくちゃん：新增當月首日及末日來源，保留既有來源數值。
    ThisMonthFirstDay = 4, ThisMonthLastDay = 5
}
/// <summary>自動日期參數的字串格式。</summary>
public enum AlertDateFormat { Gregorian, RepublicOfChina }
/// <summary>院區取得狀態；錯誤與尚未完成不視為正常零筆。</summary>
public enum AlertCampusStatus { Pending, Normal, Anomaly, Error, Interrupted }
/// <summary>區分手動、定期及尚未完成的執行。</summary>
public enum AlertRunStatus { Running, Completed, Interrupted }

/// <summary>告警的台北排程頻率；數值零保留每週語意，讓未保存頻率的舊設定維持相容。</summary>
public enum AlertScheduleFrequency { Weekly = 0, Daily = 1, Hourly = 2 }

/// <summary>保存具名參數定義及固定值；僅能儲存於加密設定檔。</summary>
public sealed class AlertParameter
{
    // 1151005 codex GPT-6_しずくちゃん：定義加密保存的具名參數預設值。
    public string Name { get; set; } = "";
    public OracleBindDataType DataType { get; set; } = OracleBindDataType.Varchar2;
    public AlertParameterSource Source { get; set; }
    public AlertDateFormat DateFormat { get; set; }
    public string? Value { get; set; }
    public bool IsNull { get; set; }
    public int? Size { get; set; }
}

/// <summary>保存單一SQL告警規則及每週、每天或每小時排程；版本及啟用起點用於去重，不持有UI或連線。</summary>
/// <remarks>
/// UI：FrmAlertRule的cmbFrequency、cmbDay、dtpTime及btnSave經AlertService間接使用。
/// 頻率未保存時預設每週；每週及每天的Time為當日時間，每小時的Time為整點後零到五十九分鐘。
/// </remarks>
public sealed class AlertRule
{
    // 1151005 codex GPT-6_しずくちゃん：建立固定識別碼及三院區停用規則的初始狀態。
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Version { get; set; } = 1;
    public string Name { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string Category { get; set; } = "";
    public string Sql { get; set; } = "";
    public List<string> Campuses { get; set; } = ["KTGH00", "HPK210", "TSHIS"];
    public bool Enabled { get; set; }
    public AlertScheduleFrequency Frequency { get; set; } = AlertScheduleFrequency.Weekly;
    public DayOfWeek Day { get; set; } = DayOfWeek.Monday;
    public TimeSpan Time { get; set; } = TimeSpan.FromHours(9);
    public DateTimeOffset EligibleAfter { get; set; }
    public List<AlertParameter> Parameters { get; set; } = [];
}

/// <summary>保存查詢欄位名稱及資料型別；List位置即原始欄位順序，可包含重複名稱。</summary>
public sealed class AlertColumn
{
    // 1151005 codex GPT-6_しずくちゃん：保留欄名與型別，欄位識別使用所在位置。
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "";
}

/// <summary>保存一院區的取得結果；只有完整讀取、明細保存與Rollback成功才確定筆數。</summary>
public sealed class AlertCampusResult
{
    // 1151005 codex GPT-6_しずくちゃん：設定未確定院區結果，避免視為正常零筆。
    public string Campus { get; set; } = "";
    public AlertCampusStatus Status { get; set; }
    public long RowCount { get; set; }
    public int ChunkCount { get; set; }
    public List<AlertColumn> Columns { get; set; } = [];
    public string? Error { get; set; }
}

/// <summary>保存取得當時的規則快照及三院區摘要；不包含SQL或參數真實值。</summary>
public sealed class AlertRun
{
    // 1151005 codex GPT-6_しずくちゃん：保存取得時點摘要及獨立履歷識別碼。
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RuleId { get; set; }
    public long Sequence { get; set; }
    public long RuleVersion { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Purpose { get; set; } = "";
    public DateTimeOffset ScheduledAt { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public bool Manual { get; set; }
    public AlertRunStatus Status { get; set; }
    public List<AlertCampusResult> Campuses { get; set; } = [];
    public string? NotificationError { get; set; }
}

/// <summary>保存各規則版本最後已完成的預定時間；不隨90日履歷清理刪除。</summary>
public sealed class AlertScheduleLedger
{
    // 1151005 codex GPT-6_しずくちゃん：保留不受履歷期限影響的設定版去重水位。
    public Dictionary<string, DateTimeOffset> Completed { get; set; } = [];
}
