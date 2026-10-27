using Project_auto_push_to_gitlab.Alerts.Infrastructure.KTCC;
using Project_auto_push_to_gitlab.Alerts.Infrastructure.Persistence;
using Project_auto_push_to_gitlab.Alerts.Infrastructure.Logging;
using Project_auto_push_to_gitlab.Alerts.Core;
using Project_auto_push_to_gitlab.Alerts.Presentation;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;
using Project_auto_push_to_gitlab.SqlOptimizer.Oracle;

namespace Project_auto_push_to_gitlab.Alerts.Application;

/// <summary>告警Composition Root，組合正式儲存、KTCC執行與通知，控制背景服務生命週期。</summary>
public sealed class AlertApplicationRuntime : IAsyncDisposable
{
    private readonly AlertService _service;
    private readonly IAlertDiagnostics _diagnostics;
    private readonly string? _notificationError;
    private bool _ready;
    public string? StartupError { get; private set; }

    /// <summary>建立服務但不連線DB、不讀取設定，允許安全建立畫面與Designer。</summary>
    /// <param name="notifier">已註冊或可回報失敗的通知邊界，不可為null。</param>
    /// <param name="notificationError">通知初始化安全錯誤，null表示沒有初始化錯誤。</param>
    /// <remarks>UI：Program建立，FrmAlerts與FrmAlertRule經Controller間接使用。</remarks>
    public AlertApplicationRuntime(IAlertNotifier notifier, string? notificationError = null)
    {
        // 1151005 codex GPT-6_しずくちゃん：既存接続と新しい告警機能を起動時に組み立てます。
        _notificationError = notificationError;
        _diagnostics = new JsonLinesAlertDiagnostics();
        _service = new AlertService(new EncryptedAlertStore(),
            new OracleAlertQueryExecutor(new KtccOracleConnectionFactory(), new OracleSelectStatementValidator()), notifier,
            diagnostics: _diagnostics);
    }

    /// <summary>讀取及復原本機資料後啟動排程；損壞時保留資料並提供唯讀錯誤畫面。</summary>
    /// <param name="token">取消啟動。</param><returns>啟動嘗試完成的Task；失敗詳情不保存敏感值。</returns>
    /// <remarks>UI：Program直接使用，StartupError顯示到FrmAlerts的lblStatus。</remarks>
    public async Task StartAsync(CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：破損した設定の上書きを止め既存アプリは利用可能にします。
        try { await _service.InitializeAsync(true, token).ConfigureAwait(false); _ready = true; }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _diagnostics.Record(exception, "告警初始化");
            StartupError = AlertErrors.ForOperation(exception, "告警初始化");
        }
    }

    /// <summary>建立B案畫面並注入已就緒Controller，初期化失敗時保持操作停用。</summary>
    /// <returns>新的Designer相容Form，不回傳null。</returns>
    /// <remarks>UI：Program及frm_SessionGrid的告警選單直接使用；只建立畫面，不查詢DB。</remarks>
    public FrmAlerts CreateForm()
    {
        // 1151005 codex GPT-6_しずくちゃん：画面は必要になった時だけ作ります。
        FrmAlerts view = new() { StartupError = StartupError ?? _notificationError };
        if (_ready) view.Attach(new AlertController(_service));
        return view;
    }

    /// <summary>取消排程與查詢並等待所有告警資源解放，可重複呼叫。</summary>
    /// <returns>資源解放完成的ValueTask。</returns><remarks>UI：frm_SessionGrid的Close及Program finally直接使用。</remarks>
    public async ValueTask DisposeAsync()
    {
        // 1151005 codex GPT-6_しずくちゃん：メニュー終了とfinallyで重複しても安全に停止します。
        await _service.DisposeAsync().ConfigureAwait(false);
    }
}
