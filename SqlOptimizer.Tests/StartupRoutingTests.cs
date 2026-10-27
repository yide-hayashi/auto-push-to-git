using System.Runtime.ExceptionServices;
using System.Reflection;
using System.Windows.Forms;
using Project_auto_push_to_gitlab.Alerts.Presentation;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

/// <summary>驗證起動要求的主畫面與通知履歷路由；略過原主畫面Load以確保不啟動Oracle或IDE監控。</summary>
/// <remarks>UI：無；xUnit於STA呼叫frm_SessionGrid的ShowStartupWindowAsync，使用真實Designer及隔離的告警畫面。</remarks>
public sealed class StartupRoutingTests
{
    /// <summary>驗證未附履歷ID的通常起動還原主畫面，通知履歷則只開啟告警，不誤走主畫面。</summary>
    /// <param name="notification">True使用測試履歷Guid；False模擬F5或通常重複起動。不修改參數。</param>
    /// <remarks>UI：xUnit直接使用frm_SessionGrid的ShowStartupWindowAsync及notifyIcon，檢查FrmAlerts的lblStatus；不查詢DB或讀取正式設定。</remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NormalLaunchAndNotificationOpenTheirOwnWindows(bool notification)
    {
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                using StartupMainForm main = new();
                using FrmAlerts alerts = new();
                main.Opacity = alerts.Opacity = 0;
                main.ShowInTaskbar = alerts.ShowInTaskbar = false;
                main.StartPosition = alerts.StartPosition = FormStartPosition.Manual;
                main.Location = alerts.Location = new System.Drawing.Point(-30000, -30000);
                int alertsCreated = 0;
                main.AlertFormFactory = () => { alertsCreated++; return alerts; };
                main.ShowStartupWindowAsync(notification ? Guid.NewGuid() : null).GetAwaiter().GetResult();
                Assert.Equal(!notification, main.Visible);
                Assert.Equal(notification, alerts.Visible);
                Assert.Equal(notification ? 1 : 0, alertsCreated);
                if (notification)
                    Assert.Contains("指定履歷", alerts.Controls.Find("lblStatus", true).Single().Text);
                else
                {
                    main.Hide();
                    main.WindowState = FormWindowState.Minimized;
                    main.ShowStartupWindowAsync(null).GetAwaiter().GetResult();
                    Assert.True(main.Visible);
                    Assert.Equal(FormWindowState.Normal, main.WindowState);
                    Assert.Equal(0, alertsCreated);
                }
            }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        failure?.Throw();
    }

    /// <summary>驗證應用程式編譯後的入口明確指向Program，避免專案起動設定落到其他Form。</summary>
    /// <remarks>UI：無；xUnit只讀取應用程式Assembly的EntryPoint，不執行Main或連線DB。</remarks>
    [Fact]
    public void ProjectEntryPointIsProgram()
    {
        Assert.Equal("Project_auto_push_to_gitlab.Program", typeof(frm_SessionGrid).Assembly.EntryPoint?.DeclaringType?.FullName);
    }

    /// <summary>驗證主畫面Load先隱藏時，可在Load完成後接收F5的通常起動要求並還原主畫面，不依賴Shown。</summary>
    /// <remarks>UI：xUnit於STA執行WinForms訊息迴圈，模擬frm_SessionGrid的Load隱藏效果後呼叫ShowStartupWindowAsync；不執行Oracle、IDE監控或正式服務。</remarks>
    [Fact]
    public void InitiallyHiddenLoadCanRestoreMainWindowAfterLoad()
    {
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                using StartupMainForm main = new() { HideDuringLoad = true, Opacity = 0,
                    ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
                    Location = new System.Drawing.Point(-30000, -30000) };
                bool restored = false;
                main.Load += (_, _) => main.BeginInvoke(new Action(() =>
                {
                    main.ShowStartupWindowAsync(null).GetAwaiter().GetResult();
                    restored = main.Visible;
                }));
                EventHandler finish = (_, _) => System.Windows.Forms.Application.ExitThread();
                System.Windows.Forms.Application.Idle += finish;
                try { System.Windows.Forms.Application.Run(main); }
                finally { System.Windows.Forms.Application.Idle -= finish; }
                Assert.True(restored);
            }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        failure?.Throw();
    }

    /// <summary>保留主畫面Designer與起動路由，測試時只略過原Load內的Oracle及IDE監控。</summary>
    /// <remarks>UI：無；StartupRoutingTests建立此測試Form，ShowStartupWindowAsync仍使用真實主畫面的顯示處理。</remarks>
    private sealed class StartupMainForm : frm_SessionGrid
    {
        public bool HideDuringLoad { get; set; }

        /// <summary>建立真實Designer後，僅解除含Oracle及IDE監控的原Load處理，保留測試註冊Load事件的能力。</summary>
        /// <remarks>UI：StartupRoutingTests直接建立；解除frm_SessionGrid的Form1_Load只影響測試Form，不變更正式實例。</remarks>
        public StartupMainForm()
        {
            MethodInfo originalLoad = typeof(frm_SessionGrid).GetMethod("Form1_Load", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), this, originalLoad);
        }

        /// <summary>按測試需求重現初回隱藏效果，並讓測試Load回呼照常執行；原DB事件已在建構時解除。</summary>
        /// <param name="e">WinForms的Load事件引數，不修改內容。</param>
        /// <remarks>UI：StartupRoutingTests的ShowStartupWindowAsync間接使用；此覆寫只存在測試組件，不改正式Form的Load。</remarks>
        protected override void OnLoad(EventArgs e)
        {
            // 只重現畫面隱藏及Load事件順序，不啟動正式環境處理。
            if (HideDuringLoad) Hide();
            base.OnLoad(e);
        }
    }
}
