using System.Data;
using Project_auto_push_to_gitlab.Alerts.Core;
using Project_auto_push_to_gitlab.Alerts.Presentation;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

/// <summary>驗證四種告警Grid的欄位投影與排序比較鍵；只建立架空模型，不存取Oracle或正式保存。</summary>
/// <remarks>UI：無；xUnit呼叫AlertGridData，對應FrmAlerts的dgvResults、dgvRules、dgvHistory及dgvDetails。</remarks>
public sealed class AlertGridDataSortingTests
{
    /// <summary>驗證院區欄依指定狀態順序排序，且異常2筆排在異常10筆之前。</summary>
    /// <remarks>UI：無；xUnit以dgvResults的欄名KTGH00驗證AlertGridData投影及FrmAlerts的表頭排序。</remarks>
    [Fact]
    public void ResultsSortCampusStatusesAndAnomalyCounts()
    {
        // 1151027 codex しずくちゃん：以無識別個資的假履歷確認院區狀態與筆數各自排序。
        List<AlertRun> runs = [
            Run("pending", AlertCampusStatus.Pending), Run("error", AlertCampusStatus.Error),
            Run("ten", AlertCampusStatus.Anomaly, 10), Run("normal", AlertCampusStatus.Normal),
            Run("interrupted", AlertCampusStatus.Interrupted), Run("two", AlertCampusStatus.Anomaly, 2),
            Run("unrun", null)];
        DataTable table = AlertGridData.Results(runs);
        AlertGridData.Sort(table, "KTGH00", false);
        Assert.Equal(new[] { "unrun", "normal", "two", "ten", "pending", "interrupted", "error" }.Select(StableGuid),
            Identifiers(table));
        AlertGridData.Sort(table, "KTGH00", true);
        Assert.Equal(new[] { "error", "interrupted", "pending", "ten", "two", "normal", "unrun" }.Select(StableGuid),
            Identifiers(table));
    }

    /// <summary>驗證名稱相同時穩定排序不交換原列順序。</summary>
    /// <remarks>UI：無；xUnit以dgvResults的「名稱」欄模擬FrmAlerts表頭排序。</remarks>
    [Fact]
    public void EqualTextKeysRetainOriginalOrder()
    {
        // 1151027 codex しずくちゃん：排序相同名稱時維持履歷原始先後。
        DataTable table = AlertGridData.Results([Run("first", null, name: "相同"), Run("second", null, name: "相同")]);
        AlertGridData.Sort(table, "名稱", true);
        Assert.Equal(new[] { "first", "second" }.Select(StableGuid), Identifiers(table));
    }

    /// <summary>驗證履歷以完整DateTime排序，即使畫面時間顯示到相同秒仍保留正確次序。</summary>
    /// <remarks>UI：無；xUnit以dgvHistory的「執行時間」欄模擬FrmAlerts表頭排序。</remarks>
    [Fact]
    public void HistorySortUsesOriginalTimestampPrecision()
    {
        // 1151027 codex しずくちゃん：畫面同秒的執行時間仍依原始時間順序排序。
        DataTable table = AlertGridData.History([
            Run("later", null, started: new(2026, 10, 27, 9, 0, 0, 900, TimeSpan.FromHours(8))),
            Run("earlier", null, started: new(2026, 10, 27, 9, 0, 0, 100, TimeSpan.FromHours(8)))]);
        Assert.Equal(table.Rows[0]["執行時間"], table.Rows[1]["執行時間"]);
        AlertGridData.Sort(table, "執行時間", false);
        Assert.Equal(new[] { "earlier", "later" }.Select(StableGuid), Identifiers(table));
    }

    /// <summary>驗證Boolean欄以布林值排序，且未啟用規則的下次執行固定位於最後。</summary>
    /// <remarks>UI：無；xUnit以dgvRules的「啟用」及「下次執行」欄模擬FrmAlerts表頭排序。</remarks>
    [Fact]
    public void RulesSortBooleanAndKeepDisabledLast()
    {
        // 1151027 codex しずくちゃん：啟用狀態使用布林值比較，停用無論方向都固定在下次執行末尾。
        AlertRule disabled = Rule("disabled", false);
        AlertRule enabled = Rule("enabled", true);
        DateTimeOffset now = new(2026, 10, 27, 8, 0, 0, TimeSpan.FromHours(8));
        DataTable table = AlertGridData.Rules([disabled, enabled], now);
        AlertGridData.Sort(table, "啟用", false);
        Assert.Equal(new[] { "disabled", "enabled" }, RuleNames(table));
        AlertGridData.Sort(table, "下次執行", false);
        Assert.Equal("disabled", RuleNames(table)[^1]);
        AlertGridData.Sort(table, "下次執行", true);
        Assert.Equal("disabled", RuleNames(table)[^1]);
    }

    /// <summary>驗證數值來源與字串來源採用對應的比較方式，並保留NULL及重複欄名。</summary>
    /// <remarks>UI：無；xUnit以dgvDetails的欄位置及實際欄名模擬FrmAlerts明細表頭排序；不連線Oracle。</remarks>
    [Fact]
    public void DetailsSortNumericTypesAsNumbersAndCodesAsText()
    {
        // 1151027 codex しずくちゃん：數值欄使用型別中繼資料，代碼欄與重複欄名維持文字及位置語意。
        List<AlertColumn> columns = [new() { Name = "AMOUNT", DataType = "NUMBER" },
            new() { Name = "CODE", DataType = "VARCHAR2" }, new() { Name = "AMOUNT", DataType = "VARCHAR2" }];
        DataTable table = AlertGridData.Details(columns, [
            ["10", "2", "Z"], ["2", "10", "B"], ["1.25", "0012", "A"], ["1e2", null, "C"]]);
        Assert.Equal("AMOUNT", table.Columns[0].Caption);
        Assert.Equal("AMOUNT", table.Columns[2].Caption);
        Assert.Equal(3, table.Columns.Cast<DataColumn>().Count(column => column.ColumnMapping != MappingType.Hidden));
        AlertGridData.Sort(table, "column0", false);
        Assert.Equal(new[] { "1.25", "2", "10", "1e2" }, DetailValues(table, "column0"));
        Assert.Equal("1e2", table.DefaultView[table.DefaultView.Count - 1]["column0"]);
        AlertGridData.Sort(table, "column1", false);
        Assert.Equal(new[] { null, "0012", "10", "2" }, DetailValues(table, "column1"));
        Assert.Equal(new[] { "C", "A", "B", "Z" }, DetailValues(table, "column2"));
        AlertGridData.Sort(table, "column2", false);
        Assert.Equal(new[] { "A", "B", "C", "Z" }, DetailValues(table, "column2"));
        AlertGridData.Sort(table, "column0", true);
        Assert.Equal(new[] { "1e2", "10", "2", "1.25" }, DetailValues(table, "column0"));
    }

    /// <summary>驗證不可解析的數值欄整欄退回文字比較，並確認空明細仍可安全排序。</summary>
    /// <remarks>UI：無；xUnit以dgvDetails的column0模擬FrmAlerts表頭排序與空頁。</remarks>
    [Fact]
    public void InvalidNumericValueFallsBackAndEmptyDetailsSortSafely()
    {
        // 1151027 codex しずくちゃん：未知數值格式不混用比較方式，空頁面仍可接受標題排序。
        DataTable table = AlertGridData.Details([new() { Name = "AMOUNT", DataType = "NUMBER" }],
            [["2"], ["bad"], ["10"]]);
        AlertGridData.Sort(table, "column0", false);
        Assert.Equal(new[] { "10", "2", "bad" }, DetailValues(table, "column0"));
        DataTable empty = AlertGridData.Details([new() { Name = "AMOUNT", DataType = "NUMBER" }], []);
        AlertGridData.Sort(empty, "column0", false);
        Assert.Empty(empty.DefaultView);
    }

    /// <summary>建立單院區的架空履歷供投影與狀態排序使用。</summary>
    /// <param name="id">測試識別字串，不含真實資料，不變更。</param>
    /// <param name="status">院區狀態；null代表未建立院區結果，不變更。</param>
    /// <param name="rowCount">異常筆數或零；不變更。</param>
    /// <param name="name">履歷顯示名稱；null時使用id，不變更。</param>
    /// <param name="started">執行時間；null時使用固定測試時間，不變更。</param>
    /// <returns>含唯一識別碼及最多一個院區的架空履歷。</returns>
    /// <remarks>UI：無；測試輔助方法，資料形狀對應FrmAlerts的dgvResults及dgvHistory。</remarks>
    private static AlertRun Run(string id, AlertCampusStatus? status, long rowCount = 0,
        string? name = null, DateTimeOffset? started = null)
    {
        // 1151027 codex しずくちゃん：使用固定架空資料避免依賴正式設定與Oracle。
        AlertRun run = new() { Id = Guid.NewGuid(), Name = name ?? id,
            StartedAt = started ?? new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.FromHours(8)),
            ScheduledAt = new(2026, 10, 27, 9, 0, 0, TimeSpan.FromHours(8)) };
        if (status.HasValue)
            run.Campuses.Add(new() { Campus = "KTGH00", Status = status.Value, RowCount = rowCount });
        run.Id = StableGuid(id);
        return run;
    }

    /// <summary>建立可測試啟用狀態及預定時間的停用或啟用規則。</summary>
    /// <param name="name">唯一測試名稱，不變更。</param>
    /// <param name="enabled">排程啟用狀態，不變更。</param>
    /// <returns>含唯一ID的架空週排程規則。</returns>
    /// <remarks>UI：無；測試輔助方法，欄位對應FrmAlerts的dgvRules。</remarks>
    private static AlertRule Rule(string name, bool enabled)
    {
        // 1151027 codex しずくちゃん：建立不用服務或資料庫的排程比較項目。
        return new() { Id = StableGuid(name), Name = name, Category = "測試", Purpose = "排序測試",
            Campuses = ["KTGH00"], Enabled = enabled };
    }

    /// <summary>將固定測試識別字串轉為可重現的Guid。</summary>
    /// <param name="value">非空測試字串，不包含患者識別資料，不變更。</param>
    /// <returns>由字串位元組建立的唯一測試Guid。</returns>
    /// <remarks>UI：無；由本測試類別呼叫，測試Grid隱藏的Id欄。</remarks>
    private static Guid StableGuid(string value)
    {
        // 1151027 codex しずくちゃん：重複執行測試時保持排序識別值可預期。
        byte[] bytes = new byte[16];
        System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(value), bytes);
        return new Guid(bytes);
    }

    /// <summary>從有序表讀取隱藏的履歷識別字串。</summary>
    /// <param name="table">投影完成的結果表，不變更。</param>
    /// <returns>依DataView排序順序回傳名稱，不含null。</returns>
    /// <remarks>UI：無；對應FrmAlerts的dgvResults及dgvHistory之隱藏Id欄。</remarks>
    private static Guid[] Identifiers(DataTable table)
    {
        // 1151027 codex しずくちゃん：畫面隱藏識別碼仍能確認實際履歷選擇順序。
        return table.DefaultView.Cast<DataRowView>().Select(row => (Guid)row["Id"]).ToArray();
    }

    /// <summary>從有序規則表讀取顯示名稱。</summary>
    /// <param name="table">投影完成的規則表，不變更。</param>
    /// <returns>依DataView排序順序回傳規則名稱。</returns>
    /// <remarks>UI：無；對應FrmAlerts的dgvRules。</remarks>
    private static string[] RuleNames(DataTable table)
    {
        // 1151027 codex しずくちゃん：非表示識別碼を使わずに規則の表示順を確認します。
        return table.DefaultView.Cast<DataRowView>().Select(row => (string)row["名稱"]).ToArray();
    }

    /// <summary>依欄位識別碼讀取明細字串或DBNull。</summary>
    /// <param name="table">投影完成的明細表，不變更。</param>
    /// <param name="column">穩定的內部欄位名稱，不變更。</param>
    /// <returns>依DataView排序順序回傳文字與null值。</returns>
    /// <remarks>UI：無；對應FrmAlerts的dgvDetails，欄位位置不依重複的見出し名推測。</remarks>
    private static string?[] DetailValues(DataTable table, string column)
    {
        // 1151027 codex しずくちゃん：NULLと同名列を保った状態で表示順を確認します。
        return table.DefaultView.Cast<DataRowView>().Select(row => row.Row.IsNull(column) ? null : (string)row[column]).ToArray();
    }
}
