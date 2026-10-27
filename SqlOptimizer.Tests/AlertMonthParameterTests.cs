using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Windows.Forms;
using Project_auto_push_to_gitlab.Alerts.Application;
using Project_auto_push_to_gitlab.Alerts.Core;
using Project_auto_push_to_gitlab.Alerts.Infrastructure.Persistence;
using Project_auto_push_to_gitlab.Alerts.Presentation;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

/// <summary>驗證當月日期來源的台北月界、格式、保存相容及操作時刻；只使用Fake與隔離保存，不連線Oracle。</summary>
/// <remarks>UI：無；由xUnit呼叫，涵蓋FrmAlertRule的dgvParameters、btnTest及FrmAlerts的btnRun與背景排程。</remarks>
public sealed class AlertMonthParameterTests
{
    /// <summary>驗證首末日依台北月份計算，涵蓋大小月、平閏年、年底及民國補零。</summary>
    /// <param name="reference">含時區的ISO基準時刻，不可為null，不變更。</param>
    /// <param name="format">自動日期格式，不變更。</param>
    /// <param name="first">預期首日字串，不可為null，不變更。</param>
    /// <param name="last">預期末日字串，不可為null，不變更。</param>
    /// <remarks>UI：無；xUnit直接呼叫Bind，來源對應FrmAlertRule的dgvParameters之colSource與colFormat。</remarks>
    [Theory]
    [InlineData("2026-10-27T09:00:00+08:00", AlertDateFormat.RepublicOfChina, "1151001", "1151031")]
    [InlineData("2026-10-27T09:00:00+08:00", AlertDateFormat.Gregorian, "20261001", "20261031")]
    [InlineData("2026-04-15T09:00:00+08:00", AlertDateFormat.RepublicOfChina, "1150401", "1150430")]
    [InlineData("2026-02-15T09:00:00+08:00", AlertDateFormat.RepublicOfChina, "1150201", "1150228")]
    [InlineData("2028-02-29T09:00:00+08:00", AlertDateFormat.RepublicOfChina, "1170201", "1170229")]
    [InlineData("2028-02-29T09:00:00+08:00", AlertDateFormat.Gregorian, "20280201", "20280229")]
    [InlineData("2026-12-31T23:59:59+08:00", AlertDateFormat.Gregorian, "20261201", "20261231")]
    [InlineData("2026-10-31T15:59:59+00:00", AlertDateFormat.RepublicOfChina, "1151001", "1151031")]
    [InlineData("2026-10-31T16:00:00+00:00", AlertDateFormat.RepublicOfChina, "1151101", "1151130")]
    [InlineData("2010-01-04T09:00:00+08:00", AlertDateFormat.RepublicOfChina, "0990101", "0990131")]
    public void MonthBoundariesUseTaipeiAndSelectedFormat(string reference, AlertDateFormat format, string first, string last)
    {
        // 1151027 codex しずくちゃん：以固定日期驗證月界及既有日期字串格式，不使用主機時間。
        AlertRule rule = MonthRule();
        rule.Parameters.ForEach(parameter => parameter.DateFormat = format);
        var binds = AlertSchedule.Bind(rule, DateTimeOffset.Parse(reference, CultureInfo.InvariantCulture));
        Assert.Equal(first, binds[0].Value);
        Assert.Equal(last, binds[1].Value);
    }

    /// <summary>驗證既有數值來源的JSON仍還原原來源及相同的固定、當日與週界值。</summary>
    /// <remarks>UI：無；xUnit驗證保存契約，對應FrmAlertRule的dgvParameters來源。</remarks>
    [Fact]
    public void LegacyNumericSourcesKeepTheirMeaning()
    {
        // 1151027 codex しずくちゃん：從舊版數值JSON確認新增項目不改變既有來源對應。
        AlertRule rule = JsonSerializer.Deserialize<AlertRule>("""
            {"Parameters":[{"Name":"fixed","Source":0,"Value":"kept"},
            {"Name":"date","Source":1},{"Name":"previous","Source":2},{"Name":"monday","Source":3}]}
            """)!;
        var binds = AlertSchedule.Bind(rule, new(2026, 10, 27, 9, 0, 0, TimeSpan.FromHours(8)));
        Assert.Equal(new object?[] { "kept", "20261027", "20261019", "20261026" }, binds.Select(bind => bind.Value));
    }

    /// <summary>驗證當月來源及民國格式可經既有CurrentUser DPAPI保存並由新Store還原。</summary>
    /// <returns>隔離保存驗證完成的Task；結束時清理測試目錄。</returns>
    /// <remarks>UI：無；xUnit呼叫Store，對應FrmAlertRule的btnSave經AlertService間接保存；不存取正式設定。</remarks>
    [Fact]
    public async Task MonthSourcesSurviveEncryptedReload()
    {
        // 1151027 codex しずくちゃん：重用隔離目錄與既有加密Store驗證兩個來源持久化。
        using TestFolder folder = new();
        await new EncryptedAlertStore(folder.Path).SaveRulesAsync([MonthRule()], default);
        AlertRule restored = Assert.Single(await new EncryptedAlertStore(folder.Path).LoadRulesAsync(default));
        Assert.Equal(new[] { AlertParameterSource.ThisMonthFirstDay, AlertParameterSource.ThisMonthLastDay },
            restored.Parameters.Select(parameter => parameter.Source));
        Assert.All(restored.Parameters, parameter => Assert.Equal(AlertDateFormat.RepublicOfChina, parameter.DateFormat));
        var binds = AlertSchedule.Bind(restored, new(2026, 10, 27, 9, 0, 0, TimeSpan.FromHours(8)));
        Assert.Equal(new object?[] { "1151001", "1151031" }, binds.Select(bind => bind.Value));
    }

    /// <summary>驗證跨月補跑仍以原預定月份建立當月參數，而非使用真正執行日。</summary>
    /// <returns>Fake排程驗證完成的Task；服務與隔離目錄均於結束時釋放。</returns>
    /// <remarks>UI：無；xUnit經AlertService模擬FrmAlerts背景排程，沿用FakeExecutor記錄日期，不連線DB。</remarks>
    [Fact]
    public async Task CatchupUsesScheduledMonth()
    {
        // 1151027 codex しずくちゃん：十月底的週排程於十一月補跑時仍取十月參數。
        using TestFolder folder = new();
        FakeClock clock = new() { Now = new(2026, 10, 25, 8, 0, 0, TimeSpan.FromHours(8)) };
        FakeExecutor executor = new();
        await using AlertService service = new(new EncryptedAlertStore(folder.Path), executor, new FakeNotifier(), clock);
        await service.InitializeAsync(false, default);
        AlertRule rule = MonthRule();
        rule.Parameters[0].Name = "ScheduleDate";
        rule.Sql = "select :ScheduleDate, :edte from dual";
        rule = await service.SaveRuleAsync(rule, default);
        rule.Enabled = true;
        await service.SaveRuleAsync(rule, default);
        clock.Now = new(2026, 11, 1, 10, 0, 0, TimeSpan.FromHours(8));
        await service.RunDueAsync(default);
        Assert.Equal("1151001", Assert.Single(executor.ScheduleDates));
        Assert.Equal(10, Assert.Single(service.Runs()).ScheduledAt.Month);
    }

    /// <summary>驗證手動與測試執行排隊跨月後，仍使用排隊前操作時刻的月份。</summary>
    /// <param name="testRun">True使用未保存輸入的測試查詢；False使用已保存規則的立即執行，不變更。</param>
    /// <returns>Fake佇列驗證完成的Task；finally解除阻塞，服務及目錄於結束時釋放。</returns>
    /// <remarks>UI：無；xUnit模擬FrmAlertRule的btnTest與FrmAlerts的btnRun；不連線DB。</remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueuedOperationKeepsRequestedMonth(bool testRun)
    {
        // 1151027 codex しずくちゃん：使用既有Fake閘門固定排隊順序，不以等待秒數模擬跨月。
        using TestFolder folder = new();
        FakeClock clock = new() { Now = new(2026, 10, 31, 23, 59, 0, TimeSpan.FromHours(8)) };
        FakeExecutor executor = new() { Continue = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        await using AlertService service = new(new EncryptedAlertStore(folder.Path), executor, new FakeNotifier(), clock);
        await service.InitializeAsync(false, default);
        AlertRule rule = MonthRule();
        rule.Parameters[0].Name = "ScheduleDate";
        rule.Sql = "select :ScheduleDate, :edte from dual";
        rule = await service.SaveRuleAsync(rule, default);
        Task<AlertRun> blocking = service.RunManualAsync(rule.Id, default);
        try
        {
            await executor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Task<AlertRun> queued = testRun ? service.RunTestAsync(rule, default) : service.RunManualAsync(rule.Id, default);
            Assert.False(queued.IsCompleted);
            clock.Now = new(2026, 11, 1, 0, 1, 0, TimeSpan.FromHours(8));
            executor.Continue.TrySetResult();
            await Task.WhenAll(blocking, queued).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(new object?[] { "1151001", "1151001" }, executor.ScheduleDates);
        }
        finally { executor.Continue.TrySetResult(); }
    }

    /// <summary>驗證Designer建立的來源選單包含當月項目，且編輯後能讀回兩個來源。</summary>
    /// <remarks>UI：xUnit於STA直接操作FrmAlertRule的dgvParameters之colSource，Attach還原設定、ReadInput讀取輸入；使用Fake服務。</remarks>
    [Fact]
    public void EditorOffersAndReadsMonthSources()
    {
        // 1151027 codex しずくちゃん：在STA驗證既有Designer選單自動提供新增來源，無須修改配置。
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                using TestFolder folder = new();
                AlertService service = new(new EncryptedAlertStore(folder.Path), new FakeExecutor(), new FakeNotifier());
                try
                {
                    using FrmAlertRule editor = new();
                    AlertRule rule = MonthRule();
                    editor.Attach(new AlertController(service), rule);
                    DataGridView grid = Assert.IsType<DataGridView>(editor.Controls.Find("dgvParameters", true).Single());
                    DataGridViewComboBoxColumn source = Assert.IsType<DataGridViewComboBoxColumn>(grid.Columns["colSource"]);
                    Assert.Contains("ThisMonthFirstDay", source.Items.Cast<string>());
                    Assert.Contains("ThisMonthLastDay", source.Items.Cast<string>());
                    grid.Rows[0].Cells["colSource"].Value = "ThisMonthLastDay";
                    grid.Rows[1].Cells["colSource"].Value = "ThisMonthFirstDay";
                    MethodInfo readInput = typeof(FrmAlertRule).GetMethod("ReadInput", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    AlertRule input = Assert.IsType<AlertRule>(readInput.Invoke(editor, null));
                    Assert.Equal(AlertParameterSource.ThisMonthLastDay, input.Parameters[0].Source);
                    Assert.Equal(AlertParameterSource.ThisMonthFirstDay, input.Parameters[1].Source);
                    Assert.Equal(AlertParameterSource.ThisMonthFirstDay, rule.Parameters[0].Source);
                    AlertSchedule.Validate(input);
                }
                finally { service.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)));
        failure?.Throw();
    }

    /// <summary>建立單院區的獨立月界規則，提供Fake查詢及編輯輸入。</summary>
    /// <returns>使用民國日期、Varchar2及首末日兩個參數的新規則；預設停用，不回傳null。</returns>
    /// <remarks>UI：無；由本測試類別呼叫，欄位對應FrmAlertRule的dgvParameters與chkKTGH00；SQL僅交給Fake。</remarks>
    private static AlertRule MonthRule()
    {
        // 1151027 codex しずくちゃん：集中建立不含患者資料的測試規則，重用既有驗證及Fake。
        return new() { Name = "當月驗證", Category = "測試", Purpose = "驗證當月首末日來源", Campuses = ["KTGH00"],
            Sql = "select :sdte, :edte from dual", Parameters = [
                new() { Name = "sdte", Source = AlertParameterSource.ThisMonthFirstDay, DateFormat = AlertDateFormat.RepublicOfChina },
                new() { Name = "edte", Source = AlertParameterSource.ThisMonthLastDay, DateFormat = AlertDateFormat.RepublicOfChina }] };
    }
}
