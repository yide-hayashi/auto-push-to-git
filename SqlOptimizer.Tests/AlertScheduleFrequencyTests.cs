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

/// <summary>驗證三種告警頻率的台北日界、保存相容、補跑去重及Designer輸入；只使用Fake與隔離保存，不連線Oracle。</summary>
/// <remarks>UI：無；由xUnit呼叫，涵蓋FrmAlertRule的cmbFrequency、cmbDay、dtpTime、btnSave及FrmAlerts的背景排程。</remarks>
public sealed class AlertScheduleFrequencyTests
{
    /// <summary>以固定參考時間驗證當次與下次預定，涵蓋恰好到期、UTC換日、整點跨日及閏年。</summary>
    /// <param name="frequency">已定義的測試排程頻率，不變更。</param>
    /// <param name="time">當日時間或整點後分鐘，格式hh:mm，不可為null。</param>
    /// <param name="reference">含UTC偏移的ISO參考時間，不可為null。</param>
    /// <param name="expectedLatest">含台北偏移的ISO最近預定，不可為null。</param>
    /// <param name="expectedNext">含台北偏移的ISO下次預定，不可為null。</param>
    /// <remarks>UI：無；xUnit呼叫AlertSchedule，對應FrmAlerts的dgvRules與背景排程。</remarks>
    [Theory]
    [InlineData(AlertScheduleFrequency.Weekly, "09:00", "2026-10-05T08:59:59+08:00", "2026-09-28T09:00:00+08:00", "2026-10-05T09:00:00+08:00")]
    [InlineData(AlertScheduleFrequency.Weekly, "09:00", "2026-10-05T09:00:00+08:00", "2026-10-05T09:00:00+08:00", "2026-10-12T09:00:00+08:00")]
    [InlineData(AlertScheduleFrequency.Daily, "09:00", "2026-10-05T08:59:59+08:00", "2026-10-04T09:00:00+08:00", "2026-10-05T09:00:00+08:00")]
    [InlineData(AlertScheduleFrequency.Daily, "09:00", "2026-10-05T09:00:00+08:00", "2026-10-05T09:00:00+08:00", "2026-10-06T09:00:00+08:00")]
    [InlineData(AlertScheduleFrequency.Daily, "09:00", "2026-10-05T17:30:00+00:00", "2026-10-05T09:00:00+08:00", "2026-10-06T09:00:00+08:00")]
    [InlineData(AlertScheduleFrequency.Daily, "00:00", "2028-02-29T00:00:00+08:00", "2028-02-29T00:00:00+08:00", "2028-03-01T00:00:00+08:00")]
    [InlineData(AlertScheduleFrequency.Hourly, "00:15", "2026-10-06T00:14:59+08:00", "2026-10-05T23:15:00+08:00", "2026-10-06T00:15:00+08:00")]
    [InlineData(AlertScheduleFrequency.Hourly, "00:00", "2026-10-05T16:00:00+00:00", "2026-10-06T00:00:00+08:00", "2026-10-06T01:00:00+08:00")]
    [InlineData(AlertScheduleFrequency.Hourly, "00:59", "2026-10-06T23:59:00+08:00", "2026-10-06T23:59:00+08:00", "2026-10-07T00:59:00+08:00")]
    public void SchedulesRespectTaipeiBoundaries(AlertScheduleFrequency frequency, string time,
        string reference, string expectedLatest, string expectedNext)
    {
        AlertRule rule = new() { Frequency = frequency, Time = TimeSpan.Parse(time, CultureInfo.InvariantCulture) };
        DateTimeOffset now = DateTimeOffset.Parse(reference, CultureInfo.InvariantCulture);
        DateTimeOffset latest = AlertSchedule.Latest(rule, now);
        DateTimeOffset next = AlertSchedule.Next(rule, now);
        Assert.Equal(DateTimeOffset.Parse(expectedLatest, CultureInfo.InvariantCulture), latest);
        Assert.Equal(DateTimeOffset.Parse(expectedNext, CultureInfo.InvariantCulture), next);
        Assert.Equal(TimeSpan.FromHours(8), latest.Offset);
        Assert.Equal(TimeSpan.FromHours(8), next.Offset);
        Assert.True(latest <= now && next > now);
    }

    /// <summary>驗證不含Frequency的舊JSON保留每週星期與時刻，且重新序列化可保存明確頻率。</summary>
    /// <remarks>UI：無；xUnit驗證保存契約，對應FrmAlertRule的cmbFrequency、cmbDay及dtpTime。</remarks>
    [Fact]
    public void LegacyJsonDefaultsToWeekly()
    {
        AlertRule rule = JsonSerializer.Deserialize<AlertRule>("""{"Day":2,"Time":"10:30:00"}""")!;
        Assert.Equal(AlertScheduleFrequency.Weekly, rule.Frequency);
        Assert.Equal(DayOfWeek.Tuesday, rule.Day);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 10, 30, 0, TimeSpan.FromHours(8)),
            AlertSchedule.Next(rule, new DateTimeOffset(2026, 10, 6, 10, 0, 0, TimeSpan.FromHours(8))));
        Assert.Contains("\"Frequency\":0", JsonSerializer.Serialize(rule));
    }

    /// <summary>驗證未定義頻率與不合法時間不可保存；每小時不得含小時、秒或負分鐘。</summary>
    /// <param name="frequency">待拒絕的頻率，不變更。</param>
    /// <param name="time">待拒絕的TimeSpan字串，不可為null。</param>
    /// <remarks>UI：無；xUnit呼叫驗證，對應FrmAlertRule的btnSave及btnTest經AlertService使用。</remarks>
    [Theory]
    [InlineData((AlertScheduleFrequency)99, "00:00:00")]
    [InlineData(AlertScheduleFrequency.Hourly, "01:00:00")]
    [InlineData(AlertScheduleFrequency.Hourly, "00:00:01")]
    [InlineData(AlertScheduleFrequency.Hourly, "-00:01:00")]
    [InlineData(AlertScheduleFrequency.Daily, "1.00:00:00")]
    public void InvalidFrequencyOrTimeCannotBeSaved(AlertScheduleFrequency frequency, string time)
    {
        AlertRule rule = ValidRule(frequency, TimeSpan.Parse(time, CultureInfo.InvariantCulture));
        Assert.Throws<ArgumentException>(() => AlertSchedule.Validate(rule));
    }

    /// <summary>驗證新頻率可加密保存、首次啟用不補過去、停機只補最近一次且重啟不重複。</summary>
    /// <param name="frequency">每天或每小時的有效頻率，不變更。</param>
    /// <returns>隔離保存及Fake排程驗證完成的Task；所有服務於結束時Dispose。</returns>
    /// <remarks>UI：無；xUnit透過AlertService驗證FrmAlertRule的btnSave及FrmAlerts的背景排程；不連線DB。</remarks>
    [Theory]
    [InlineData(AlertScheduleFrequency.Daily)]
    [InlineData(AlertScheduleFrequency.Hourly)]
    public async Task SavedFrequencyCatchesUpOnceAndSurvivesRestart(AlertScheduleFrequency frequency)
    {
        using TestFolder folder = new();
        FakeClock clock = new();
        FakeExecutor executor = new();
        EncryptedAlertStore store = new(folder.Path);
        await using (AlertService service = new(store, executor, new FakeNotifier(), clock))
        {
            await service.InitializeAsync(false, default);
            AlertRule rule = await service.SaveRuleAsync(ValidRule(frequency,
                frequency == AlertScheduleFrequency.Daily ? TimeSpan.FromHours(9) : TimeSpan.FromMinutes(15)), default);
            Assert.False(rule.Enabled);
            rule.Enabled = true;
            rule = await service.SaveRuleAsync(rule, default);
            await service.RunDueAsync(default);
            Assert.Empty(executor.Calls);
            clock.Now = new(2026, 10, 5, 9, 20, 0, TimeSpan.FromHours(8));
            await service.RunDueAsync(default);
            await service.RunDueAsync(default);
            Assert.Equal(3, executor.Calls.Count);
            clock.Now = new(2026, 10, 8, 12, 40, 0, TimeSpan.FromHours(8));
            await service.RunDueAsync(default);
            Assert.Equal(6, executor.Calls.Count);
            Assert.Equal(new DateTimeOffset(2026, 10, 8,
                frequency == AlertScheduleFrequency.Daily ? 9 : 12,
                frequency == AlertScheduleFrequency.Daily ? 0 : 15, 0, TimeSpan.FromHours(8)), service.Runs()[0].ScheduledAt);
        }
        await using AlertService restarted = new(store, executor, new FakeNotifier(), clock);
        await restarted.InitializeAsync(false, default);
        Assert.Equal(frequency, Assert.Single(restarted.Rules()).Frequency);
        await restarted.RunDueAsync(default);
        Assert.Equal(6, executor.Calls.Count);
    }

    /// <summary>驗證編輯畫面能切換及還原三種頻率，並以Designer控制項保存指定分鐘；產生無業務資料的實畫面供目視確認。</summary>
    /// <remarks>UI：xUnit於STA直接操作FrmAlertRule的cmbFrequency、cmbDay、dtpTime並經ReadInput取得資料；只附加Fake服務，不查詢DB。</remarks>
    [Fact]
    public void EditorSwitchesFrequencyAndRestoresSavedInputs()
    {
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
                    ComboBox frequency = Assert.IsType<ComboBox>(editor.Controls.Find("cmbFrequency", true).Single());
                    ComboBox day = Assert.IsType<ComboBox>(editor.Controls.Find("cmbDay", true).Single());
                    DateTimePicker time = Assert.IsType<DateTimePicker>(editor.Controls.Find("dtpTime", true).Single());
                    Assert.Equal(new[] { "每週", "每天", "每小時" }, frequency.Items.Cast<string>());
                    Assert.Equal(0, frequency.SelectedIndex);
                    editor.ShowInTaskbar = false;
                    editor.Opacity = 0;
                    editor.StartPosition = FormStartPosition.Manual;
                    editor.Location = new System.Drawing.Point(-30000, -30000);
                    editor.Show();
                    MethodInfo readInput = typeof(FrmAlertRule).GetMethod("ReadInput", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    AlertController controller = new(service);
                    foreach (AlertScheduleFrequency selected in Enum.GetValues<AlertScheduleFrequency>())
                    {
                        AlertRule rule = ValidRule(selected, selected == AlertScheduleFrequency.Hourly
                            ? TimeSpan.FromMinutes(37) : new TimeSpan(9, 37, 0));
                        editor.Attach(controller, rule);
                        Assert.Equal((int)selected, frequency.SelectedIndex);
                        Assert.Equal(selected == AlertScheduleFrequency.Weekly, day.Visible);
                        Assert.Equal(selected == AlertScheduleFrequency.Hourly ? "'每小時 'mm' 分'" : "HH:mm", time.CustomFormat);
                        if (selected == AlertScheduleFrequency.Hourly) time.Value = DateTime.Today.AddHours(17).AddMinutes(37);
                        AlertRule input = Assert.IsType<AlertRule>(readInput.Invoke(editor, null));
                        Assert.Equal(selected, input.Frequency);
                        Assert.Equal(rule.Time, input.Time);
                        AlertSchedule.Validate(input);
                        editor.PerformLayout();
                        Assert.True(time.Right <= time.Parent!.ClientSize.Width);
                        using System.Drawing.Bitmap preview = new(editor.Width, editor.Height);
                        editor.DrawToBitmap(preview, new(0, 0, editor.Width, editor.Height));
                        preview.Save(System.IO.Path.Combine(AppContext.BaseDirectory, $"alerts-schedule-{selected}.png"));
                    }
                    frequency.SelectedIndex = 0;
                    Assert.True(day.Visible);
                    Assert.Equal("HH:mm", time.CustomFormat);
                    editor.Close();
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

    /// <summary>建立獨立的有效Fake規則，提供排程驗證必需欄位；SQL只交給Fake，從不建立Oracle連線。</summary>
    /// <param name="frequency">測試使用的頻率，可傳入未定義值供拒絕路徑；不修改。</param>
    /// <param name="time">測試排程時間，可傳入無效值供拒絕路徑；不修改。</param>
    /// <returns>新規則物件，預設停用、三院區且沒有參數；不回傳null。</returns>
    /// <remarks>UI：無；本測試類別呼叫，輸入對應FrmAlertRule的cmbFrequency及dtpTime。</remarks>
    private static AlertRule ValidRule(AlertScheduleFrequency frequency, TimeSpan time)
    {
        return new() { Name = "排程驗證", Category = "測試", Purpose = "Fake頻率驗證", Sql = "select 1 from dual",
            Frequency = frequency, Time = time };
    }
}
