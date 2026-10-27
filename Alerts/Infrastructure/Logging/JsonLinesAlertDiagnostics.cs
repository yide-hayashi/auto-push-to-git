using System.Text.Json;
using Project_auto_push_to_gitlab.Alerts.Core;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;

namespace Project_auto_push_to_gitlab.Alerts.Infrastructure.Logging;

/// <summary>在專用LocalAppData Logs保存已去識別化的技術紀錄，DB Log Table未指定時仍可替換。</summary>
public sealed class JsonLinesAlertDiagnostics : IAlertDiagnostics
{
    private readonly string _file;
    private readonly object _gate = new();
    public string? Error { get; private set; }

    /// <summary>指定本機紀錄路徑，建構時不寫入檔案。</summary>
    /// <param name="root">測試可使用隔離路徑；null使用AutoCallGit的Logs。</param>
    /// <remarks>UI：無；AlertApplicationRuntime組合本Writer。</remarks>
    public JsonLinesAlertDiagnostics(string? root = null)
    {
        // 1151005 codex GPT-6_しずくちゃん：ソース外に匿名化済みの技術ログを保存します。
        _file = Path.Combine(root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutoCallGit", "Logs"), "alerts.jsonl");
    }

    /// <summary>使用既有錯誤Sanitizer只保存時間與安全訊息，Writer失敗回報Error。</summary>
    /// <param name="exception">原始例外，不可為null，不保存完整Message或StackTrace。</param>
    /// <param name="operation">程式固定操作名稱，不包含輸入值。</param>
    /// <remarks>UI：無；由AlertService取得錯誤時呼叫；紀錄失敗不掩蓋原操作失敗並顯示Error。</remarks>
    public void Record(Exception exception, string operation)
    {
        // 1151005 codex GPT-6_しずくちゃん：Oracleの原例外や実際のパラメーターをログへ出しません。
        string line = JsonSerializer.Serialize(new { Time = DateTimeOffset.UtcNow,
            Message = SqlOptimizerErrorSanitizer.ForOperation(exception, operation) });
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
                File.AppendAllText(_file, line + Environment.NewLine);
                Error = null;
            }
            catch (Exception) { Error = "告警技術紀錄無法保存。"; }
        }
    }
}
