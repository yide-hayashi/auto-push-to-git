using Project_auto_push_to_gitlab.SqlOptimizer.Core;

namespace Project_auto_push_to_gitlab.Alerts.Core;

/// <summary>沿用既有錯誤去識別化並將告警畫面訊息本地化，不顯示原始Oracle或輸入內容。</summary>
public static class AlertErrors
{
    /// <summary>先去識別化，再將安全錯誤種類轉為繁體中文畫面訊息。</summary>
    /// <param name="exception">原例外，不可為null，不保存內容。</param><param name="operation">程式固定的繁體中文操作名稱。</param>
    /// <returns>不含患者值、SQL、參數或認證內容的安全訊息。</returns>
    /// <remarks>UI：FrmAlerts的lblStatus及lblPage、FrmAlertRule的lblMessage經錯誤流程間接使用。</remarks>
    public static string ForOperation(Exception exception, string operation)
    {
        // 1151005 codex GPT-6_しずくちゃん：既存の匿名化処理を使い表示文言だけを繁體中文へ変えます。
        string safe = SqlOptimizerErrorSanitizer.ForOperation(exception, operation);
        int oracle = safe.IndexOf("ORA-", StringComparison.Ordinal);
        if (oracle >= 0 && oracle + 9 <= safe.Length) return $"{operation}失敗（{safe.Substring(oracle, 9)}）。";
        if (exception is OperationCanceledException) return operation + "已取消。";
        if (exception is TimeoutException) return operation + "逾時。";
        return operation + "失敗，請確認設定與執行紀錄。";
    }
}
