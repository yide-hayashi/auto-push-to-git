using System.Runtime.ExceptionServices;
using System.Text;
using Project_auto_push_to_gitlab.Alerts.Application;
using Project_auto_push_to_gitlab.Alerts.Core;
using Project_auto_push_to_gitlab.Alerts.Infrastructure.Notifications;
using Project_auto_push_to_gitlab.Alerts.Infrastructure.Persistence;
using Project_auto_push_to_gitlab.Alerts.Infrastructure.Logging;
using Project_auto_push_to_gitlab.Alerts.Presentation;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

/// <summary>以Fake查詢及隔離的CurrentUser DPAPI保存驗證告警，不連線Oracle且不操作正式資料。</summary>
public sealed class AlertFeatureTests
{
    /// <summary>驗證新規停用、有效化前不補跑，停止多週後僅補最近一次且重啟不重複。</summary>
    /// <returns>驗證完成Task。</returns><remarks>UI：無；對應FrmAlerts的btnToggle及背景排程。</remarks>
    [Fact]
    public async Task Schedule_EnablingCatchupAndRestartDoNotDuplicate()
    {
        // 1151005 codex GPT-6_しずくちゃん：有効化・停止期間・再起動の実行回数を確認します。
        using TestFolder folder = new();
        FakeClock clock = new();
        FakeExecutor executor = new();
        FakeNotifier notifier = new();
        EncryptedAlertStore store = new(folder.Path);
        await using (AlertService service = new(store, executor, notifier, clock))
        {
            await service.InitializeAsync(false, default);
            AlertRule rule = await service.SaveRuleAsync(Rule(), default);
            Assert.False(rule.Enabled);
            rule.Enabled = true;
            rule = await service.SaveRuleAsync(rule, default);
            await service.RunDueAsync(default);
            Assert.Empty(executor.Calls);
            clock.Now = clock.Now.AddHours(1);
            await service.RunDueAsync(default);
            await service.RunDueAsync(default);
            Assert.Equal(3, executor.Calls.Count);
            Assert.Empty(notifier.Runs);
            clock.Now = clock.Now.AddDays(29);
            await service.RunDueAsync(default);
            Assert.Equal(6, executor.Calls.Count);
            Assert.Equal(new DateTime(2026, 11, 2), service.Runs()[0].ScheduledAt.Date);
        }
        await using AlertService restarted = new(store, executor, notifier, clock);
        await restarted.InitializeAsync(false, default);
        await restarted.RunDueAsync(default);
        Assert.Equal(6, executor.Calls.Count);
    }

    /// <summary>驗證預定日參數以台北週一計算，民國與西元格式及跨年界線一致。</summary>
    /// <remarks>UI：無；對應FrmAlertRule的dgvParameters。</remarks>
    [Fact]
    public void Parameters_UseScheduledWeekAndGregorianOrRocStrings()
    {
        // 1151005 codex GPT-6_しずくちゃん：補完実行の日付は実行日ではなく予定日から求めます。
        AlertRule rule = Rule();
        rule.Parameters = [new() { Name = "ScheduleDate", Source = AlertParameterSource.ScheduleDate },
            new() { Name = "PreviousWeekMonday", Source = AlertParameterSource.PreviousWeekMonday, DateFormat = AlertDateFormat.RepublicOfChina },
            new() { Name = "ThisWeekMonday", Source = AlertParameterSource.ThisWeekMonday }];
        List<SqlBindParameter> binds = AlertSchedule.Bind(rule, new DateTimeOffset(2026, 1, 4, 12, 0, 0, TimeSpan.Zero));
        Assert.Equal("20260104", binds[0].Value);
        Assert.Equal("1141222", binds[1].Value);
        Assert.Equal("20251229", binds[2].Value);
        rule.Parameters[0].DateFormat = AlertDateFormat.RepublicOfChina;
        Assert.Equal("0990104", AlertSchedule.Bind(rule, new DateTimeOffset(2010, 1, 4, 9, 0, 0, TimeSpan.FromHours(8)))[0].Value);
    }

    /// <summary>驗證三院區順序、不同欄位、NULL、500筆分割、200筆跨片段頁及部分失敗。</summary>
    /// <returns>驗證完成Task。</returns><remarks>UI：無；對應FrmAlerts三院區卡片與dgvDetails。</remarks>
    [Fact]
    public async Task PartialFailureLargeRowsAndNullArePreserved()
    {
        // 1151005 codex GPT-6_しずくちゃん：大量明細と部分失敗を実際の暗号化保存で確認します。
        using TestFolder folder = new();
        FakeExecutor executor = new() { Count = campus => campus == "KTGH00" ? 1001 : campus == "HPK210" ? -1 : 1 };
        FakeNotifier notifier = new();
        await using AlertService service = new(new EncryptedAlertStore(folder.Path), executor, notifier);
        await service.InitializeAsync(false, default);
        AlertRule rule = await service.SaveRuleAsync(Rule(), default);
        AlertRun run = await service.RunManualAsync(rule.Id, default);
        Assert.Equal(AlertSchedule.CampusOrder, executor.Calls);
        Assert.Equal(1001, run.Campuses[0].RowCount);
        Assert.Equal(3, run.Campuses[0].ChunkCount);
        Assert.Equal(AlertCampusStatus.Error, run.Campuses[1].Status);
        Assert.Equal(1, run.Campuses[2].RowCount);
        Assert.Equal(3, run.Campuses[2].Columns.Count);
        List<string?[]> page = await service.ReadPageAsync(run, "KTGH00", 2, default);
        Assert.Equal(200, page.Count);
        Assert.Equal("ROW-400", page[0][0]);
        Assert.Equal("ROW-599", page[^1][0]);
        Assert.Null(page[0][1]);
        Assert.Single(await service.ReadPageAsync(run, "KTGH00", 5, default));
        Assert.Single(notifier.Runs);
        string summary = System.Text.Json.JsonSerializer.Serialize(run);
        Assert.DoesNotContain("sensitive-value", summary);
        Assert.DoesNotContain(rule.Sql, summary);
        foreach (string file in Directory.EnumerateFiles(folder.Path, "*.bin", SearchOption.AllDirectories))
            Assert.DoesNotContain("ROW-400", Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file)));
    }

    /// <summary>驗證全部零筆不通知，通知失敗不影響已保存歷史。</summary>
    /// <returns>驗證完成Task。</returns><remarks>UI：無；對應Windows通知與dgvHistory。</remarks>
    [Fact]
    public async Task NotificationFailureKeepsCompletedHistory()
    {
        // 1151005 codex GPT-6_しずくちゃん：通知失敗時にも完了した履歴を保持します。
        using TestFolder folder = new();
        FakeExecutor executor = new();
        FakeNotifier notifier = new() { Fail = true };
        EncryptedAlertStore store = new(folder.Path);
        await using AlertService service = new(store, executor, notifier);
        await service.InitializeAsync(false, default);
        AlertRule rule = await service.SaveRuleAsync(Rule(), default);
        await service.RunManualAsync(rule.Id, default);
        Assert.Empty(notifier.Runs);
        executor.Count = _ => 1;
        AlertRun run = await service.RunManualAsync(rule.Id, default);
        Assert.Equal(AlertRunStatus.Completed, run.Status);
        Assert.NotNull(run.NotificationError);
        Assert.Equal(2, (await store.LoadRunsAsync(default)).Count);
    }

    /// <summary>驗證Rollback或明細保存失敗皆為取得錯誤，不可顯示已讀的部分筆數。</summary>
    /// <returns>驗證完成Task。</returns><remarks>UI：無；對應院區取得失敗狀態。</remarks>
    [Fact]
    public async Task RollbackAndStorageFailuresNeverPublishPartialCounts()
    {
        // 1151005 codex GPT-6_しずくちゃん：読み取り後のRollbackと保存失敗を正常結果から除外します。
        using TestFolder folder = new();
        FakeExecutor executor = new() { Count = _ => 501, RollbackFailure = true };
        await using AlertService service = new(new EncryptedAlertStore(folder.Path), executor, new FakeNotifier());
        await service.InitializeAsync(false, default);
        AlertRule rule = await service.SaveRuleAsync(Rule(), default);
        AlertRun run = await service.RunManualAsync(rule.Id, default);
        Assert.All(run.Campuses, result => { Assert.Equal(AlertCampusStatus.Error, result.Status); Assert.Equal(0, result.RowCount); });
        executor.RollbackFailure = false;
        Directory.Move(System.IO.Path.Combine(folder.Path, "Details"), System.IO.Path.Combine(folder.Path, "OldDetails"));
        await File.WriteAllTextAsync(System.IO.Path.Combine(folder.Path, "Details"), "blocked");
        run = await service.RunManualAsync(rule.Id, default);
        Assert.All(run.Campuses, result => Assert.Equal(AlertCampusStatus.Error, result.Status));
    }

    /// <summary>驗證取消保存中斷履歷，重新初始化重跑同一預定後才推進去重水位。</summary>
    /// <returns>驗證完成Task。</returns><remarks>UI：無；對應正式結束與下次啟動補跑。</remarks>
    [Fact]
    public async Task InterruptedScheduleIsRetriedAfterRestart()
    {
        // 1151005 codex GPT-6_しずくちゃん：取消時の予定を再起動後に一度だけ再実行します。
        using TestFolder folder = new();
        FakeClock clock = new();
        FakeExecutor executor = new() { Block = true };
        EncryptedAlertStore store = new(folder.Path);
        await using (AlertService service = new(store, executor, new FakeNotifier(), clock))
        {
            await service.InitializeAsync(false, default);
            AlertRule rule = await service.SaveRuleAsync(Rule(), default);
            rule.Enabled = true;
            await service.SaveRuleAsync(rule, default);
            clock.Now = clock.Now.AddHours(1);
            using CancellationTokenSource stop = new();
            Task running = service.RunDueAsync(stop.Token);
            await executor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            stop.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
            Assert.Equal(AlertRunStatus.Interrupted, service.Runs()[0].Status);
        }
        executor.Block = false;
        await using AlertService resumed = new(store, executor, new FakeNotifier(), clock);
        await resumed.InitializeAsync(false, default);
        await resumed.RunDueAsync(default);
        Assert.Equal(AlertRunStatus.Completed, resumed.Runs()[0].Status);
        int calls = executor.Calls.Count;
        await resumed.RunDueAsync(default);
        Assert.Equal(calls, executor.Calls.Count);
    }

    /// <summary>驗證手動與定期競爭仍只有一個查詢執行中，服務Dispose能取消連線工作。</summary>
    /// <returns>驗證完成Task。</returns><remarks>UI：無；對應btnRun及正式結束。</remarks>
    [Fact]
    public async Task ManualQueueIsSerialAndShutdownCancelsActiveWork()
    {
        // 1151005 codex GPT-6_しずくちゃん：二つの手動要求を同時実行させず終了で解放します。
        using TestFolder folder = new();
        FakeExecutor executor = new() { Block = true };
        AlertService service = new(new EncryptedAlertStore(folder.Path), executor, new FakeNotifier());
        await service.InitializeAsync(false, default);
        AlertRule rule = await service.SaveRuleAsync(Rule(), default);
        Task first = service.RunManualAsync(rule.Id, default);
        await executor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task second = service.RunManualAsync(rule.Id, default);
        Assert.Single(executor.Calls);
        await service.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        Assert.Single(service.Runs());
    }

    /// <summary>驗證90日只清理履歷及明細，保留設定、去重資訊與邊界日資料。</summary>
    /// <returns>驗證完成Task。</returns><remarks>UI：無；對應啟動及日次清理。</remarks>
    [Fact]
    public async Task RetentionKeepsRulesLedgerAndNinetyDayBoundary()
    {
        // 1151005 codex GPT-6_しずくちゃん：専用の古い履歴だけが削除されることを確認します。
        using TestFolder folder = new();
        EncryptedAlertStore store = new(folder.Path);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        AlertRun expired = new() { StartedAt = now.AddDays(-91) };
        AlertRun boundary = new() { StartedAt = now.AddDays(-90) };
        await store.SaveRunAsync(expired, default);
        await store.SaveRunAsync(boundary, default);
        await store.WriteChunkAsync(expired.Id, "KTGH00", 0, [["fake"]], default);
        await store.SaveRulesAsync([Rule()], default);
        await store.SaveLedgerAsync(new() { Completed = new() { ["fake"] = now } }, default);
        await store.CleanupAsync(now, default);
        Assert.Equal(boundary.Id, Assert.Single(await store.LoadRunsAsync(default)).Id);
        Assert.False(Directory.Exists(System.IO.Path.Combine(folder.Path, "Details", expired.Id.ToString("N"))));
        Assert.Single(await store.LoadRulesAsync(default));
        Assert.Single((await store.LoadLedgerAsync(default)).Completed);
    }

    /// <summary>驗證加密檔可解密、缺少明細及損壞設定不被當作空資料。</summary>
    /// <returns>驗證完成Task。</returns><remarks>UI：無；對應初始化錯誤與明細錯誤。</remarks>
    [Fact]
    public async Task CorruptOrMissingEncryptedFilesAreReported()
    {
        // 1151005 codex GPT-6_しずくちゃん：設定と明細の破損を明確な失敗として扱います。
        using TestFolder folder = new();
        EncryptedAlertStore store = new(folder.Path);
        await store.SaveRulesAsync([Rule()], default);
        Assert.Single(await store.LoadRulesAsync(default));
        await File.WriteAllBytesAsync(System.IO.Path.Combine(folder.Path, "rules.bin"), [1, 2, 3]);
        await Assert.ThrowsAnyAsync<Exception>(() => store.LoadRulesAsync(default));
        await Assert.ThrowsAnyAsync<IOException>(() => store.ReadPageAsync(Guid.NewGuid(),
            new() { Campus = "KTGH00", Status = AlertCampusStatus.Anomaly, RowCount = 1, Columns = [new() { Name = "X" }] }, 0, default));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.WriteChunkAsync(Guid.NewGuid(), "../outside", 0, [["fake"]], default));
    }

    /// <summary>驗證通知只接受有效Guid，任意字串不形成開檔路徑或命令。</summary>
    /// <param name="input">不可信的通知引數。</param><remarks>UI：無；對應通知點擊啟動。</remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("runId=../outside")]
    [InlineData("runId=00000000-0000-0000-0000-000000000000")]
    public void NotificationActivationRejectsInvalidIds(string? input)
    {
        // 1151005 codex GPT-6_しずくちゃん：通知の入力を不透明な履歴IDへ限定します。
        Assert.Null(WindowsAlertNotifier.ParseRunId(input));
        Guid id = Guid.NewGuid();
        Assert.Equal(id, WindowsAlertNotifier.ParseRunId("runId=" + id.ToString("D")));
    }

    /// <summary>驗證三個Form與UserControl可在STA建立及解析資源，未啟動DB或正式服務。</summary>
    /// <remarks>UI：直接建立FrmAlerts、FrmAlertRule及CampusSummaryCard。</remarks>
    [Fact]
    public void DesignerFormsInitializeWithoutExternalServices()
    {
        // 1151005 codex GPT-6_しずくちゃん：画面とカードのDesigner構築をSTAで検証します。
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                using FrmAlerts view = new();
                using FrmAlertRule editor = new();
                using CampusSummaryCard card = new();
                Assert.Equal("KTGH00", card.Campus);
                Assert.Equal("FrmAlerts", view.Name);
                Assert.NotNull(view.Controls.Find("dgvDetails", true).Single());
                Assert.Equal("", Assert.IsType<System.Windows.Forms.TextBox>(editor.Controls.Find("txtSql", true).Single()).Text);
                foreach (string name in new[] { "FrmAlerts", "FrmAlertRule", "CampusSummaryCard" })
                    Assert.Contains(typeof(FrmAlerts).Assembly.GetManifestResourceNames(), resource => resource.EndsWith(name + ".resources"));
                view.ShowInTaskbar = false;
                view.Opacity = 0;
                view.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                view.Location = new System.Drawing.Point(-30000, -30000);
                view.Show();
                view.CreateControl();
                view.PerformLayout();
                using System.Drawing.Bitmap preview = new(view.Width, view.Height);
                view.DrawToBitmap(preview, new System.Drawing.Rectangle(0, 0, view.Width, view.Height));
                string artifact = System.IO.Path.Combine(AppContext.BaseDirectory, "alerts-b-preview.png");
                preview.Save(artifact);
                view.Close();
            }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        failure?.Throw();
    }

    /// <summary>建立無患者資料的Fake規則，只有參數化SELECT供驗證器使用。</summary>
    /// <returns>新的停用規則。</returns><remarks>UI：無；僅供本測試建立輸入。</remarks>
    private static AlertRule Rule()
    {
        // 1151005 codex GPT-6_しずくちゃん：実データを含まないテスト規則を作ります。
        return new() { Name = "資料檢查", Category = "資料品質", Purpose = "Fake驗證", Sql = "select :ScheduleDate as CHECK_DATE from dual",
            Parameters = [new() { Name = "ScheduleDate", Source = AlertParameterSource.ScheduleDate }] };
    }

    /// <summary>驗證兩個實例只允許第一個啟動背景服務，履歷ID由使用者限定Pipe轉送。</summary>
    /// <returns>IPC驗證完成Task。</returns><remarks>UI：無；使用獨立測試Scope，完全不觸碰正式單一起動識別。</remarks>
    [Fact]
    public async Task SingleInstanceRedirectsNotificationIdAndNormalLaunch()
    {
        // 1151005 codex GPT-6_しずくちゃん：単一起動と通知IDの転送を隔離したIPCで確認します。
        string scope = Guid.NewGuid().ToString("N");
        await using AlertSingleInstance first = new(scope);
        await using AlertSingleInstance second = new(scope);
        Assert.True(first.IsPrimary);
        Assert.False(second.IsPrimary);
        TaskCompletionSource<Guid?> activation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        first.Activated += id => activation.TrySetResult(id);
        first.Start();
        Guid expected = Guid.NewGuid();
        await second.RedirectAsync(expected);
        Assert.Equal(expected, await activation.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        activation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await second.RedirectAsync(null);
        Assert.Null(await activation.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    /// <summary>驗證卡片彙總各規則最新結果、搜尋及複製不變更已保存規則。</summary>
    /// <returns>Dashboard驗證完成Task。</returns><remarks>UI：無；對應三院區卡片、txtSearch及btnDuplicate。</remarks>
    [Fact]
    public async Task DashboardTotalsLatestPerRuleAndDuplicateIsDisabled()
    {
        // 1151005 codex GPT-6_しずくちゃん：最新結果だけの集計と規則複製の独立性を確認します。
        using TestFolder folder = new();
        FakeExecutor executor = new() { Count = _ => 2 };
        await using AlertService service = new(new EncryptedAlertStore(folder.Path), executor, new FakeNotifier());
        await service.InitializeAsync(false, default);
        AlertRule first = await service.SaveRuleAsync(Rule(), default);
        AlertRule secondInput = Rule();
        secondInput.Name = "另一規則";
        AlertRule second = await service.SaveRuleAsync(secondInput, default);
        await service.RunManualAsync(first.Id, default);
        executor.Count = _ => 3;
        await service.RunManualAsync(first.Id, default);
        await service.RunManualAsync(second.Id, default);
        AlertController controller = new(service);
        Assert.All(controller.Dashboard(null, null, null, null).Campuses, campus => Assert.Equal(6, campus.Anomalies));
        Assert.All(controller.Dashboard(first.Name, null, null, null).Campuses, campus => Assert.Equal(3, campus.Anomalies));
        AlertRule copy = controller.Duplicate(first.Id);
        Assert.NotEqual(first.Id, copy.Id);
        Assert.False(copy.Enabled);
        copy.Parameters[0].Name = "changed";
        Assert.Equal("ScheduleDate", service.Rules().Single(rule => rule.Id == first.Id).Parameters[0].Name);
    }

    /// <summary>驗證通知履歷可於B案顯示，重複欄位、NULL與修改前的分類搜尋均保留。</summary>
    /// <returns>STA畫面驗證完成Task。</returns><remarks>UI：直接操作FrmAlerts的dgvDetails、tabCampuses與cmbCategory，不連線Oracle。</remarks>
    [Fact]
    public async Task BViewRendersSnapshotDetailsWithDuplicateColumns()
    {
        // 1151005 codex GPT-6_しずくちゃん：通知から開く取得明細とB案の実画面を検証します。
        using TestFolder folder = new();
        FakeExecutor executor = new() { Count = campus => campus == "KTGH00" ? 12 : campus == "HPK210" ? 3 : 0 };
        await using AlertService service = new(new EncryptedAlertStore(folder.Path), executor, new FakeNotifier());
        await service.InitializeAsync(false, default);
        AlertRule rule = await service.SaveRuleAsync(Rule(), default);
        AlertRun run = await service.RunManualAsync(rule.Id, default);
        rule.Category = "變更後分類";
        rule = await service.SaveRuleAsync(rule, default);
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                System.Windows.Forms.Application.EnableVisualStyles();
                using FrmAlerts view = new();
                view.ShowInTaskbar = false;
                view.Opacity = 0;
                view.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                view.Location = new(-30000, -30000);
                view.Attach(new AlertController(service));
                Task opening = view.OpenRunAsync(run.Id);
                DateTime limit = DateTime.UtcNow.AddSeconds(10);
                while (!opening.IsCompleted && DateTime.UtcNow < limit)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                Assert.True(opening.IsCompleted);
                opening.GetAwaiter().GetResult();
                System.Windows.Forms.DataGridView grid = Assert.IsType<System.Windows.Forms.DataGridView>(view.Controls.Find("dgvDetails", true).Single());
                Assert.Equal(12, grid.Rows.Count);
                Assert.Equal(2, grid.Columns.Cast<System.Windows.Forms.DataGridViewColumn>().Count(column => column.Visible));
                Assert.Equal("VALUE", grid.Columns[0].HeaderText);
                Assert.Equal("VALUE", grid.Columns[1].HeaderText);
                Assert.Equal(DBNull.Value, grid.Rows[0].Cells[1].Value);
                System.Windows.Forms.ComboBox categories = Assert.IsType<System.Windows.Forms.ComboBox>(view.Controls.Find("cmbCategory", true).Single());
                Assert.Contains("資料品質", categories.Items.Cast<string>());
                Assert.Contains("變更後分類", categories.Items.Cast<string>());
                using System.Drawing.Bitmap preview = new(view.Width, view.Height);
                view.DrawToBitmap(preview, new(0, 0, view.Width, view.Height));
                preview.Save(System.IO.Path.Combine(AppContext.BaseDirectory, "alerts-b-data-preview.png"));
                using FrmAlertRule editor = new();
                editor.Attach(new AlertController(service), rule);
                editor.ShowInTaskbar = false;
                editor.Opacity = 0;
                editor.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
                editor.Location = new(-30000, -30000);
                editor.Show();
                using System.Drawing.Bitmap editorPreview = new(editor.Width, editor.Height);
                editor.DrawToBitmap(editorPreview, new(0, 0, editor.Width, editor.Height));
                editorPreview.Save(System.IO.Path.Combine(AppContext.BaseDirectory, "alerts-editor-preview.png"));
                editor.Close();
                view.Close();
            }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)));
        failure?.Throw();
    }

    /// <summary>驗證技術Log不保存原始Exception中的病人或參數值，紀錄失敗可觀察。</summary>
    /// <returns>Log驗證完成Task。</returns><remarks>UI：無；使用隔離Temp目錄驗證正式Writer。</remarks>
    [Fact]
    public async Task DiagnosticLogSanitizesValuesAndReportsWriteFailure()
    {
        // 1151005 codex GPT-6_しずくちゃん：実際の例外テキストがログへ漏れないことを確認します。
        using TestFolder folder = new();
        JsonLinesAlertDiagnostics diagnostics = new(folder.Path);
        diagnostics.Record(new InvalidOperationException("sensitive-value"), "告警資料取得");
        string contents = await File.ReadAllTextAsync(System.IO.Path.Combine(folder.Path, "alerts.jsonl"));
        Assert.DoesNotContain("sensitive-value", contents);
        Assert.Null(diagnostics.Error);
        string blocked = System.IO.Path.Combine(folder.Path, "blocked");
        await File.WriteAllTextAsync(blocked, "fake");
        JsonLinesAlertDiagnostics unavailable = new(blocked);
        unavailable.Record(new InvalidOperationException("sensitive-value"), "告警資料取得");
        Assert.NotNull(unavailable.Error);
    }

    /// <summary>驗證佇列等待不改變手動日期，執行中的版本不混入後續編輯內容。</summary>
    /// <returns>快照與操作時間驗證完成Task。</returns>
    /// <remarks>UI：無；對應FrmAlerts的btnRun與FrmAlertRule的btnSave。</remarks>
    [Fact]
    public async Task QueuedManualUsesOperationDateAndRunningSnapshot()
    {
        // 1151005 codex GPT-6_しずくちゃん：待ち時間の日付変更と編集中の更新を実行の快照から分離します。
        using TestFolder folder = new();
        FakeClock clock = new();
        FakeExecutor executor = new() { Continue = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using AlertService service = new(new EncryptedAlertStore(folder.Path), executor, new FakeNotifier(), clock);
        await service.InitializeAsync(false, default);
        AlertRule rule = await service.SaveRuleAsync(Rule(), default);
        Task<AlertRun> first = service.RunManualAsync(rule.Id, default);
        await executor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<AlertRun> queued = service.RunManualAsync(rule.Id, default);
        DateTimeOffset requested = clock.Now.ToUniversalTime();
        rule.Name = "變更後的名稱";
        AlertRule edited = await service.SaveRuleAsync(rule, default);
        clock.Now = clock.Now.AddDays(1);
        executor.Continue.SetResult();
        AlertRun original = await first;
        AlertRun next = await queued;
        Assert.Equal("資料檢查", original.Name);
        Assert.Equal(edited.Name, next.Name);
        Assert.Equal(requested, next.ScheduledAt);
        Assert.Equal(new[] { "資料檢查", "資料檢查", "資料檢查", edited.Name, edited.Name, edited.Name }, executor.Names);
        Assert.All(executor.ScheduleDates, date => Assert.Equal("20261005", date));
    }

    /// <summary>驗證改期及停用後不補舊預定，未保存測試不變更排程設定。</summary>
    /// <returns>設定版本與試跑驗證完成Task。</returns>
    /// <remarks>UI：無；對應FrmAlerts的btnToggle及FrmAlertRule的btnTest。</remarks>
    [Fact]
    public async Task ScheduleChangesStartAtNextDateAndTestDoesNotSaveRule()
    {
        // 1151005 codex GPT-6_しずくちゃん：予定変更の新しい起点と未保存テストの独立性を確認します。
        using TestFolder folder = new();
        FakeClock clock = new();
        FakeExecutor executor = new();
        await using AlertService service = new(new EncryptedAlertStore(folder.Path), executor, new FakeNotifier(), clock);
        await service.InitializeAsync(false, default);
        AlertRule rule = await service.SaveRuleAsync(Rule(), default);
        rule.Enabled = true;
        rule = await service.SaveRuleAsync(rule, default);
        clock.Now = clock.Now.AddDays(2);
        rule.Day = DayOfWeek.Tuesday;
        rule = await service.SaveRuleAsync(rule, default);
        await service.RunDueAsync(default);
        Assert.Empty(executor.Calls);
        clock.Now = new(2026, 10, 13, 9, 0, 0, TimeSpan.FromHours(8));
        await service.RunDueAsync(default);
        Assert.Equal(3, executor.Calls.Count);
        rule.Enabled = false;
        rule = await service.SaveRuleAsync(rule, default);
        clock.Now = clock.Now.AddDays(20);
        await service.RunDueAsync(default);
        Assert.Equal(3, executor.Calls.Count);
        rule.Enabled = true;
        rule = await service.SaveRuleAsync(rule, default);
        await service.RunDueAsync(default);
        Assert.Equal(3, executor.Calls.Count);
        string savedName = rule.Name;
        rule.Name = "未保存測試";
        AlertRun tested = await service.RunTestAsync(rule, default);
        Assert.True(tested.Manual);
        Assert.Equal(clock.Now.ToUniversalTime(), tested.ScheduledAt);
        Assert.Equal(savedName, Assert.Single(service.Rules()).Name);
        Assert.Equal("20261102", executor.ScheduleDates[^1]);
    }

    /// <summary>驗證取消回呼拋錯仍等待查詢退出，重複Dispose等待相同的結束工作。</summary>
    /// <returns>結束清理驗證完成Task。</returns>
    /// <remarks>UI：無；對應Program與通知區域Close的結束路徑。</remarks>
    [Fact]
    public async Task ShutdownWaitsForQueryEvenWhenCancellationCallbackFails()
    {
        // 1151005 codex GPT-6_しずくちゃん：取消処理の例外でも実行キューを解放するまで待ちます。
        using TestFolder folder = new();
        FakeExecutor executor = new() { Block = true, CancelFailure = true };
        AlertService service = new(new EncryptedAlertStore(folder.Path), executor, new FakeNotifier());
        await service.InitializeAsync(false, default);
        AlertRule rule = await service.SaveRuleAsync(Rule(), default);
        Task running = service.RunManualAsync(rule.Id, default);
        await executor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task stopping = service.DisposeAsync().AsTask();
        Assert.Same(stopping, service.DisposeAsync().AsTask());
        await stopping;
        Assert.NotNull(service.SystemError);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Equal(AlertRunStatus.Interrupted, Assert.Single(service.Runs()).Status);
    }
}

/// <summary>Fake固定時鐘，讓排程驗證不依賴等待或主機時區。</summary>
internal sealed class FakeClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 5, 8, 0, 0, TimeSpan.FromHours(8));
    /// <summary>讀取可控制的UTC時間。</summary><returns>目前測試時間。</returns><remarks>UI：無；AlertService測試呼叫。</remarks>
    public override DateTimeOffset GetUtcNow()
    {
        // 1151005 codex GPT-6_しずくちゃん：予定のテスト用時刻を返します。
        return Now.ToUniversalTime();
    }
}

/// <summary>模擬逐筆查詢、院區錯誤及Rollback失敗，完全不建立DB連線。</summary>
internal sealed class FakeExecutor : IAlertQueryExecutor
{
    public List<string> Calls { get; } = [];
    public Func<string, int> Count { get; set; } = _ => 0;
    public bool RollbackFailure { get; set; }
    public bool Block { get; set; }
    public bool CancelFailure { get; set; }
    public TaskCompletionSource? Continue { get; set; }
    public List<string> Names { get; } = [];
    public List<object?> ScheduleDates { get; } = [];
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>按測試設定產生假列並回報可控制的失敗。</summary>
    /// <param name="rule">Fake規則，不修改。</param><param name="campus">目前院區。</param><param name="parameters">已計算值，不修改。</param>
    /// <param name="columns">接收Fake欄位回呼。</param><param name="row">接收Fake列回呼。</param><param name="token">取消Fake等待。</param>
    /// <returns>Fake查詢完成Task。</returns><remarks>UI：無；由AlertService測試呼叫。</remarks>
    /// <exception cref="InvalidOperationException">指定院區取得失敗時拋出。</exception>
    /// <exception cref="AggregateException">指定Rollback失敗時拋出。</exception>
    /// <exception cref="IOException">指定取消回呼失敗時由取消操作拋出。</exception>
    public async Task ExecuteAsync(AlertRule rule, string campus, IReadOnlyList<SqlBindParameter> parameters,
        Func<List<AlertColumn>, Task> columns, Func<string?[], Task> row, CancellationToken token)
    {
        // 1151005 codex GPT-6_しずくちゃん：個人情報を使わず列構成と失敗を再現します。
        Calls.Add(campus);
        Names.Add(rule.Name);
        ScheduleDates.Add(parameters.FirstOrDefault(parameter =>
            string.Equals(parameter.Name, "ScheduleDate", StringComparison.OrdinalIgnoreCase))?.Value);
        using CancellationTokenRegistration cancellation = token.Register(() =>
        {
            if (CancelFailure) throw new IOException("Fake取消回呼失敗。");
        });
        Entered.TrySetResult();
        if (Block) await Task.Delay(Timeout.Infinite, token);
        if (Continue is not null) await Continue.Task.WaitAsync(token);
        int count = Count(campus);
        if (count < 0) throw new InvalidOperationException("sensitive-value");
        List<AlertColumn> metadata = [new() { Name = "VALUE", DataType = "VARCHAR2" }, new() { Name = "VALUE", DataType = "VARCHAR2" }];
        if (campus == "TSHIS") metadata.Add(new() { Name = "EXTRA", DataType = "NUMBER" });
        await columns(metadata);
        for (int index = 0; index < count; index++)
            await row(campus == "TSHIS" ? [$"ROW-{index}", null, "1"] : [$"ROW-{index}", null]);
        if (RollbackFailure) throw new AggregateException("Rollback失敗", new IOException("sensitive-value"));
    }
}

/// <summary>記錄摘要通知並模擬Windows通知停用，不顯示桌面通知。</summary>
internal sealed class FakeNotifier : IAlertNotifier
{
    public bool Fail { get; set; }
    public List<AlertRun> Runs { get; } = [];
    /// <summary>記錄通知，指定失敗時拋出例外。</summary>
    /// <param name="run">摘要，不修改。</param><returns>完成Task。</returns>
    /// <exception cref="InvalidOperationException">Fake設定通知失敗時以失敗Task回傳。</exception>
    /// <remarks>UI：無；由AlertService測試呼叫。</remarks>
    public Task NotifyAsync(AlertRun run)
    {
        // 1151005 codex GPT-6_しずくちゃん：通知の件数だけをFakeへ記録します。
        Runs.Add(run);
        return Fail ? Task.FromException(new InvalidOperationException("disabled")) : Task.CompletedTask;
    }
}

/// <summary>隔離本次測試的專用Temp子目錄，只清理已驗證在該目錄內的檔案。</summary>
internal sealed class TestFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AutoCallGit-AlertTests", Guid.NewGuid().ToString("N")));
    /// <summary>清理隔離的測試目錄，確認根路徑及無ReparsePoint後才刪除。</summary>
    /// <exception cref="InvalidOperationException">測試路徑超出專用目錄或包含連結時拋出。</exception>
    /// <remarks>UI：無；告警Fake及加密保存測試finally使用。</remarks>
    public void Dispose()
    {
        // 1151005 codex GPT-6_しずくちゃん：テスト専用の生成物だけを削除します。
        string expected = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AutoCallGit-AlertTests")) + System.IO.Path.DirectorySeparatorChar;
        if (!Path.StartsWith(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("測試目錄越界。");
        if (Directory.Exists(Path))
        {
            if ((File.GetAttributes(Path) & FileAttributes.ReparsePoint) != 0
                || Directory.EnumerateFileSystemEntries(Path, "*", SearchOption.AllDirectories)
                    .Any(entry => (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0))
                throw new InvalidOperationException("測試目錄含連結。");
            Directory.Delete(Path, true);
        }
    }
}
