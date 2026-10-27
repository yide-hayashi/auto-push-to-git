using System.Collections.Concurrent;
using System.Diagnostics;
using Project_auto_push_to_gitlab.Alerts.Application;
using Project_auto_push_to_gitlab.Alerts.Infrastructure.Notifications;
using Project_auto_push_to_gitlab.SqlOptimizer.Application;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;

namespace Project_auto_push_to_gitlab;

/// <summary>WinForms啟動入口，統合單一起動、F5主畫面、通知履歷及告警與原功能的生命週期。</summary>
/// <remarks>UI：直接建立frm_SessionGrid；F5與一般重複起動顯示主畫面，通知識別碼才開啟FrmAlerts的履歷。已存在實例時不重複啟動DB或排程。</remarks>
internal static class Program
{
    /// <summary>僅第一實例啟動服務；F5顯示主畫面、通知顯示指定履歷。F5遇到舊實例時要求先正式結束，避免除錯落入另一個程式。</summary>
    /// <remarks>
    /// UI：直接建立frm_SessionGrid，F5及一般重複起動經ShowStartupWindowAsync顯示主畫面，通知識別碼才顯示FrmAlerts的dgvDetails。
    /// 一般首次啟動維持通知領域常駐。正式結束先取消告警並等待Rollback，再釋放SQL最佳化與單一起動接收。
    /// </remarks>
    [STAThread]
    private static void Main()
    {
        // 1151005 codex GPT-6_しずくちゃん：既存機能を保ちながら単一起動と通知受付を統合します。
        ApplicationConfiguration.Initialize();
        using WindowsAlertNotifier notifier = new();
        notifier.Start();
        AlertSingleInstance instance = new();
        try
        {
            if (!instance.IsPrimary)
            {
                // F5必須使用本次建置的程式；不可把除錯要求交給仍在執行的舊版。
                if (Debugger.IsAttached)
                    MessageBox.Show("已有 AutoCallGit 正在執行。請先從通知區域選單的 Close 正式結束，再按 F5 啟動目前專案。視窗的 × 只會隱藏程式。",
                        "AutoCallGit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    instance.RedirectAsync(notifier.InitialRunId).GetAwaiter().GetResult();
                return;
            }
            SqlOptimizerApplicationRuntime optimizer = new();
            AlertApplicationRuntime alerts = new(notifier, notifier.Error);
            frm_SessionGrid mainForm = new()
            {
                SqlOptimizerFormFactory = optimizer.CreateOptimizerForm,
                AlertFormFactory = alerts.CreateForm,
                StopAlertsAsync = () => alerts.DisposeAsync().AsTask()
            };
            ConcurrentQueue<Guid?> pending = new();
            bool ready = false;
            Action<Guid?> activate = id =>
            {
                pending.Enqueue(id);
                if (Volatile.Read(ref ready) && !mainForm.IsDisposed)
                    mainForm.BeginInvoke(new Action(async () =>
                    {
                        while (pending.TryDequeue(out Guid? runId)) await mainForm.ShowStartupWindowAsync(runId);
                    }));
            };
            notifier.Activated += activate;
            instance.Activated += activate;
            instance.Start();
            if (notifier.InitialRunId.HasValue || Debugger.IsAttached) pending.Enqueue(notifier.InitialRunId);
            try
            {
                optimizer.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                alerts.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                // 原Load會先隱藏主畫面，不能依賴Shown；等Load完成後再處理起動要求。
                mainForm.Load += (_, _) => mainForm.BeginInvoke(new Action(async () =>
                {
                    Volatile.Write(ref ready, true);
                    while (pending.TryDequeue(out Guid? runId)) await mainForm.ShowStartupWindowAsync(runId);
                }));
                Application.Run(mainForm);
            }
            finally
            {
                Volatile.Write(ref ready, false);
                notifier.Activated -= activate;
                instance.Activated -= activate;
                try { alerts.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
                finally
                {
                    optimizer.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    mainForm.Dispose();
                }
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(Alerts.Core.AlertErrors.ForOperation(exception, "AutoCallGit啟動或結束"),
                "AutoCallGit", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { instance.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }
}
