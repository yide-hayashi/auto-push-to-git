using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.Windows.ApplicationModel.DynamicDependency;
using Project_auto_push_to_gitlab.Alerts.Core;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;

namespace Project_auto_push_to_gitlab.Alerts.Infrastructure.Notifications;

/// <summary>管理未封裝應用程式的通知註冊與啟動參數，不包含病人明細或參數值。</summary>
public sealed class WindowsAlertNotifier : IAlertNotifier, IDisposable
{
    private bool _bootstrapped;
    private bool _registered;
    public string? Error { get; private set; }
    public event Action<Guid?>? Activated;
    public Guid? InitialRunId { get; private set; }

    /// <summary>初始化Windows App Runtime及通知，失敗轉為不含敏感內容的狀態。</summary>
    /// <remarks>UI：Program在建立FrmAlerts之前呼叫；通知啟動ID於畫面建立後開啟履歷。</remarks>
    public void Start()
    {
        // 1151005 codex GPT-6_しずくちゃん：通知の登録は受信ハンドラーを接続した後に行います。
        try
        {
            _bootstrapped = Bootstrap.TryInitialize(Microsoft.WindowsAppSDK.Release.MajorMinor,
                Microsoft.WindowsAppSDK.Release.VersionTag,
                new PackageVersion(Microsoft.WindowsAppSDK.Runtime.Version.UInt64),
                Bootstrap.InitializeOptions.None, out _);
            if (!_bootstrapped) { Error = "Windows App Runtime未安裝；通知無法使用。"; return; }
            AppNotificationManager.Default.NotificationInvoked += OnNotification;
            AppNotificationManager.Default.Register();
            _registered = true;
            AppActivationArguments activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activation.Data is AppNotificationActivatedEventArgs args) InitialRunId = ParseRunId(args.Argument);
        }
        catch (Exception exception) { Error = AlertErrors.ForOperation(exception, "Windows通知初始化"); }
    }

    /// <summary>解析通知的不透明履歷ID，拒絕路徑及其他任意內容。</summary>
    /// <param name="argument">通知引數，null或格式錯誤不產生ID。</param><returns>有效Guid或null。</returns>
    /// <remarks>UI：FrmAlerts經啟動路由間接使用；不執行引數內容。</remarks>
    public static Guid? ParseRunId(string? argument)
    {
        // 1151005 codex GPT-6_しずくちゃん：クリック情報は履歴IDだけを受け取ります。
        string? value = argument?.Split('&').FirstOrDefault(part => part.StartsWith("runId=", StringComparison.Ordinal))?[6..];
        return Guid.TryParse(value, out Guid id) && id != Guid.Empty ? id : null;
    }

    /// <summary>轉送通知點擊事件，不直接操作背景執行緒以外的UI。</summary>
    /// <param name="sender">Windows通知管理器，不修改。</param><param name="args">啟動引數，不修改。</param>
    /// <remarks>UI：由Program的啟動路由間接顯示FrmAlerts及指定履歷。</remarks>
    private void OnNotification(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        // 1151005 codex GPT-6_しずくちゃん：通知クリックを画面作成後に処理できるよう転送します。
        Guid? id = ParseRunId(args.Argument);
        if (id.HasValue)
        {
            InitialRunId = id;
            Activated?.Invoke(id);
        }
    }

    /// <summary>以警告名稱、分類及院區件數建立一筆通知，僅附加履歷ID。</summary>
    /// <param name="run">完整保存摘要，不可為null，不變更內容。</param><returns>通知送出完成的Task。</returns>
    /// <exception cref="InvalidOperationException">通知未註冊、被停用或未成功送出時拋出。</exception>
    /// <remarks>UI：AlertService間接使用，點擊顯示FrmAlerts的dgvHistory及dgvDetails。</remarks>
    public Task NotifyAsync(AlertRun run)
    {
        // 1151005 codex GPT-6_しずくちゃん：患者情報を含まない院区別摘要だけを通知します。
        if (!_registered || AppNotificationManager.Default.Setting != AppNotificationSetting.Enabled)
            throw new InvalidOperationException("Windows通知無法使用。");
        string counts = string.Join(" | ", run.Campuses.Select(result => $"{result.Campus}: " +
            (result.Status == AlertCampusStatus.Error ? "取得失敗" : $"{result.RowCount:N0}筆")));
        AppNotification notification = new AppNotificationBuilder().AddArgument("runId", run.Id.ToString("D"))
            .AddText(run.Name).AddText(run.Category).AddText(counts).BuildNotification();
        AppNotificationManager.Default.Show(notification);
        if (notification.Id == 0) throw new InvalidOperationException("Windows通知未送出。");
        return Task.CompletedTask;
    }

    /// <summary>解除本次通知事件及Runtime；保留Windows通知與下次啟動所需的註冊。</summary>
    /// <remarks>UI：Program結束時呼叫，不刪除歷史或通知註冊資料。</remarks>
    public void Dispose()
    {
        // 1151005 codex GPT-6_しずくちゃん：次回の通知起動を保持したまま通知リソースを解放します。
        if (_registered) AppNotificationManager.Default.Unregister();
        if (_bootstrapped)
        {
            AppNotificationManager.Default.NotificationInvoked -= OnNotification;
            Bootstrap.Shutdown();
        }
        _registered = false;
        _bootstrapped = false;
    }
}
