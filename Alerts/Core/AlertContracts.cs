using Project_auto_push_to_gitlab.SqlOptimizer.Core;

namespace Project_auto_push_to_gitlab.Alerts.Core;

/// <summary>告警SQL的逐筆讀取邊界，正式實作負責Rollback，Fake不需連線Oracle。</summary>
public interface IAlertQueryExecutor
{
    /// <summary>逐筆傳送原始欄位及列值，成功返回代表查詢與Rollback皆完成。</summary>
    /// <param name="rule">本次固定的有效規則，不可為null，不變更內容。</param>
    /// <param name="campus">受支援的院區別名，不可為空。</param>
    /// <param name="parameters">已驗證的參數值，不可為null，不變更內容。</param>
    /// <param name="columns">接收一次欄位結構的非null回呼。</param>
    /// <param name="row">接收各列的非null回呼；null欄位代表DB NULL。</param>
    /// <param name="token">取消查詢與讀取使用的Token。</param>
    /// <returns>查詢及Rollback完成的Task；任一步驟失敗會拋出例外。</returns>
    /// <remarks>UI：由FrmAlerts的btnRun經AlertService間接使用；資料送至dgvDetails，不直接操作UI。</remarks>
    Task ExecuteAsync(AlertRule rule, string campus, IReadOnlyList<SqlBindParameter> parameters,
        Func<List<AlertColumn>, Task> columns, Func<string?[], Task> row, CancellationToken token);
}

/// <summary>告警設定、履歷、分割明細及去重資訊的加密儲存邊界。</summary>
public interface IAlertStore
{
    /// <summary>讀取設定；不存在時回傳空List，損壞時拋出例外。</summary>
    /// <param name="token">取消檔案讀取。</param>
    /// <returns>獨立的設定List，不回傳null。</returns>
    /// <remarks>UI：FrmAlerts的dgvRules經AlertService間接使用。</remarks>
    Task<List<AlertRule>> LoadRulesAsync(CancellationToken token);
    /// <summary>原子保存全部規則，不變更傳入List。</summary>
    /// <param name="rules">已驗證的完整設定，不可為null。</param><param name="token">取消保存。</param>
    /// <returns>完整保存的Task；失敗拋出例外。</returns>
    /// <remarks>UI：FrmAlertRule的btnSave經AlertService間接使用。</remarks>
    Task SaveRulesAsync(List<AlertRule> rules, CancellationToken token);
    /// <summary>讀取保留期間的履歷摘要；損壞不當作零筆。</summary>
    /// <param name="token">取消讀取。</param><returns>摘要List，查無資料為空List。</returns>
    /// <remarks>UI：FrmAlerts的dgvResults與dgvHistory經AlertService間接使用。</remarks>
    Task<List<AlertRun>> LoadRunsAsync(CancellationToken token);
    /// <summary>原子保存單次執行摘要。</summary>
    /// <param name="run">不可為null，不修改內容。</param><param name="token">取消保存。</param>
    /// <returns>保存完成的Task；失敗拋出例外。</returns>
    /// <remarks>UI：FrmAlerts的btnRun及排程經AlertService間接更新dgvHistory。</remarks>
    Task SaveRunAsync(AlertRun run, CancellationToken token);
    /// <summary>讀取不受履歷到期影響的去重資訊。</summary>
    /// <param name="token">取消讀取。</param><returns>去重資訊；不存在時回傳空內容。</returns>
    /// <remarks>UI：無；由AlertService的排程呼叫。</remarks>
    Task<AlertScheduleLedger> LoadLedgerAsync(CancellationToken token);
    /// <summary>原子保存去重資訊。</summary>
    /// <param name="ledger">不可為null，不修改內容。</param><param name="token">取消保存。</param>
    /// <returns>保存完成的Task。</returns><remarks>UI：無；由AlertService呼叫。</remarks>
    Task SaveLedgerAsync(AlertScheduleLedger ledger, CancellationToken token);
    /// <summary>保存一個院區的500筆以內明細片段。</summary>
    /// <param name="runId">執行ID。</param><param name="campus">受支援院區別名。</param>
    /// <param name="chunk">從零開始的片段序號。</param><param name="rows">不可為null，不修改內容。</param>
    /// <param name="token">取消保存。</param><returns>加密保存完成的Task。</returns>
    /// <remarks>UI：FrmAlerts的dgvDetails經AlertService間接使用。</remarks>
    Task WriteChunkAsync(Guid runId, string campus, int chunk, List<string?[]> rows, CancellationToken token);
    /// <summary>讀取指定院區的明細頁，頁大小固定為200筆。</summary>
    /// <param name="runId">執行ID。</param><param name="campus">已完整取得的院區摘要，不更動內容。</param>
    /// <param name="page">從零開始的頁碼。</param><param name="token">取消讀取。</param>
    /// <returns>該頁列值；末頁後為空List，損壞或缺檔拋出例外。</returns>
    /// <remarks>UI：FrmAlerts的btnPrevious與btnNext經Controller間接顯示dgvDetails。</remarks>
    Task<List<string?[]>> ReadPageAsync(Guid runId, AlertCampusResult campus, int page, CancellationToken token);
    /// <summary>清理超過90日的履歷及明細，僅能操作專用保存目錄。</summary>
    /// <param name="now">清理判斷用時間，不更動設定或去重資訊。</param><param name="token">取消清理。</param>
    /// <returns>清理完成的Task；損壞檔案或刪除失敗拋出例外。</returns>
    /// <remarks>UI：無；由AlertService啟動及日次維護呼叫。</remarks>
    Task CleanupAsync(DateTimeOffset now, CancellationToken token);
}

/// <summary>可由正式Windows通知或Fake替換的通知邊界。</summary>
public interface IAlertNotifier
{
    /// <summary>傳送摘要通知；不得包含明細、SQL及參數。</summary>
    /// <param name="run">已完整保存的執行摘要，不可為null，不變更內容。</param>
    /// <returns>通知處理完成的Task；無法傳送時拋出例外。</returns>
    /// <remarks>UI：由AlertService間接使用；點擊後顯示FrmAlerts的dgvHistory與dgvDetails。</remarks>
    Task NotifyAsync(AlertRun run);
}

/// <summary>可替換的告警技術錯誤紀錄，正式實作必須遮罩原始例外內容。</summary>
public interface IAlertDiagnostics
{
    string? Error { get; }
    /// <summary>記錄操作名稱及去識別化錯誤，不保存SQL、參數或資料值。</summary>
    /// <param name="exception">原始例外，不可為null，只供去識別化。</param><param name="operation">程式定義的操作名稱，不得使用SQL或輸入值。</param>
    /// <remarks>UI：無；AlertService及Runtime呼叫，紀錄失敗必須由Error回報。</remarks>
    void Record(Exception exception, string operation);
}
