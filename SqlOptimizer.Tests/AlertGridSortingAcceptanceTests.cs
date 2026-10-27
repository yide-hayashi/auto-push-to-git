using System.Data;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using Project_auto_push_to_gitlab.Alerts.Application;
using Project_auto_push_to_gitlab.Alerts.Core;
using Project_auto_push_to_gitlab.Alerts.Infrastructure.Persistence;
using Project_auto_push_to_gitlab.Alerts.Presentation;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

/// <summary>驗證FrmAlerts四個Grid的真實表頭事件、方向箭頭、個別排序、固定識別選取及明細欄位位置。</summary>
/// <remarks>UI：xUnit於STA直接操作FrmAlerts的dgvResults、dgvRules、dgvHistory、dgvDetails及tabCampuses；只使用Fake查詢與隔離DPAPI保存，不連線Oracle。</remarks>
[Collection("Alert grid UI acceptance")]
public sealed class AlertGridSortingAcceptanceTests
{
    /// <summary>以WinForms表頭事件驗證三個摘要清單及下方明細能昇降冪排序並保留選取鍵與欄寬。</summary>
    /// <returns>Fake保存、STA畫面及事件檢查完成的Task；以using及finally釋放專用目錄與Form。</returns>
    /// <remarks>UI：直接操作FrmAlerts的dgvResults、dgvRules、dgvHistory、dgvDetails及tabCampuses；資料固定使用三筆架空履歷、三條假規則及FakeExecutor產生的明細，不執行DB或真實SQL。</remarks>
    [Fact]
    public async Task HeaderClicksSortEveryGridAndKeepSelectedRecords()
    {
        // 1151027 codex しずくちゃん：透過WinForms原生表頭事件驗證排序及操作對象保護。
        using TestFolder folder = new();
        EncryptedAlertStore store = new(folder.Path);
        string[] names = ["Zulu", "Alpha", "Bravo"];
        FakeExecutor executor = new() { Count = campus => campus == "KTGH00" ? 201 : campus == "HPK210" ? 3 : 0 };
        await using AlertService service = new(store, executor, new FakeNotifier(), new FakeClock());
        await service.InitializeAsync(false, default);
        AlertRun[] runs = new AlertRun[names.Length];
        foreach (int index in Enumerable.Range(0, names.Length))
        {
            AlertRule rule = await service.SaveRuleAsync(CreateRule(names[index]), default);
            runs[index] = await service.RunManualAsync(rule.Id, default);
        }

        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                System.Windows.Forms.Application.EnableVisualStyles();
                // 1151027 codex ツバサちゃん：測試STA明確使用WinForms同步內容，非同步繫結必須回到同一個畫面執行緒。
                using WindowsFormsSynchronizationContext context = new();
                SynchronizationContext.SetSynchronizationContext(context);
                using FrmAlerts view = new();
                view.ShowInTaskbar = false;
                view.Opacity = 0;
                view.StartPosition = FormStartPosition.Manual;
                view.Location = new(-30000, -30000);
                view.Attach(new AlertController(service));
                view.Show();
                PumpUntil(() => view.Controls.Find("dgvResults", true).Single() is DataGridView grid && grid.Rows.Count == names.Length,
                    TimeSpan.FromSeconds(10));

                DataGridView results = FindGrid(view, "dgvResults");
                DataGridView ruleGrid = FindGrid(view, "dgvRules");
                DataGridView history = FindGrid(view, "dgvHistory");
                DataGridView details = FindGrid(view, "dgvDetails");
                Guid selectedRun = runs[2].Id;
                Task initialOpening = view.OpenRunAsync(selectedRun);
                PumpUntil(() => initialOpening.IsCompleted, TimeSpan.FromSeconds(10));
                initialOpening.GetAwaiter().GetResult();
                Assert.Equal(2, details.Columns.Cast<DataGridViewColumn>().Count(column => column.Visible));
                Assert.Equal(200, details.Rows.Count);
                Assert.Equal(selectedRun, SelectedId(results));
                ClickHeader(results, "名稱");
                Assert.Equal(SortOrder.Ascending, results.Columns["名稱"].HeaderCell.SortGlyphDirection);
                Assert.Equal(selectedRun, SelectedId(results));
                AssertOrderedNames(results, "名稱", ["Alpha", "Bravo", "Zulu"]);
                ClickHeader(results, "名稱");
                Assert.Equal(SortOrder.Descending, results.Columns["名稱"].HeaderCell.SortGlyphDirection);
                Assert.Equal(selectedRun, SelectedId(results));
                AssertOrderedNames(results, "名稱", ["Zulu", "Bravo", "Alpha"]);

                Guid selectedRule = Guid.Empty;
                foreach (DataGridViewRow row in ruleGrid.Rows)
                {
                    if (Convert.ToString(row.Cells["名稱"].Value) != "Zulu") continue;
                    selectedRule = Assert.IsType<Guid>(row.Cells["Id"].Value);
                    ruleGrid.CurrentCell = row.Cells["名稱"];
                    row.Selected = true;
                }
                Assert.NotEqual(Guid.Empty, selectedRule);
                ClickHeader(ruleGrid, "名稱");
                ClickHeader(history, "名稱");
                Assert.Equal(SortOrder.Ascending, ruleGrid.Columns["名稱"].HeaderCell.SortGlyphDirection);
                Assert.Equal(SortOrder.Ascending, history.Columns["名稱"].HeaderCell.SortGlyphDirection);
                Assert.Equal(SortOrder.Descending, results.Columns["名稱"].HeaderCell.SortGlyphDirection);
                Assert.Equal(selectedRule, SelectedId(ruleGrid));
                AssertOrderedNames(ruleGrid, "名稱", ["Alpha", "Bravo", "Zulu"]);
                AssertOrderedNames(history, "名稱", ["Alpha", "Bravo", "Zulu"]);

                // 頁面重新繫結後仍套用各Grid自己的排序，並以Guid還原操作對象。
                MethodInfo render = typeof(FrmAlerts).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)!;
                render.Invoke(view, null);
                Assert.Equal(selectedRun, SelectedId(results));
                Assert.Equal(selectedRule, SelectedId(ruleGrid));
                Assert.Equal(SortOrder.Descending, results.Columns["名稱"].HeaderCell.SortGlyphDirection);
                Assert.Equal(SortOrder.Ascending, ruleGrid.Columns["名稱"].HeaderCell.SortGlyphDirection);
                Assert.Equal(SortOrder.Ascending, history.Columns["名稱"].HeaderCell.SortGlyphDirection);
                AssertOrderedNames(results, "名稱", ["Zulu", "Bravo", "Alpha"]);
                AssertOrderedNames(ruleGrid, "名稱", ["Alpha", "Bravo", "Zulu"]);

                // 點擊重複欄名的第二欄，必須依內部位置排序並保留手動欄寬。
                Assert.Equal("VALUE", details.Columns.Cast<DataGridViewColumn>().First(column => column.Visible).HeaderText);
                Assert.Equal("VALUE", details.Columns.Cast<DataGridViewColumn>().Last(column => column.Visible).HeaderText);
                DataGridViewColumn secondDetail = details.Columns.Cast<DataGridViewColumn>().Last(column => column.Visible);
                int originalWidth = secondDetail.Width;
                secondDetail.Width = originalWidth + 24;
                ClickHeader(details, secondDetail.DataPropertyName);
                Assert.Equal("VALUE", secondDetail.HeaderText);
                Assert.Equal(SortOrder.Ascending, secondDetail.HeaderCell.SortGlyphDirection);
                Assert.Equal(originalWidth + 24, secondDetail.Width);

                // 1151027 codex ツバサちゃん：跨頁仍沿用同一欄位排序，驗證200筆頁面及末頁一筆均可復原方向。
                string detailColumn = secondDetail.DataPropertyName;
                Button next = Assert.IsType<Button>(view.Controls.Find("btnNext", true).Single());
                Button previous = Assert.IsType<Button>(view.Controls.Find("btnPrevious", true).Single());
                next.PerformClick();
                PumpUntil(() => details.Rows.Count == 1
                    && details.Columns[detailColumn].HeaderCell.SortGlyphDirection == SortOrder.Ascending
                    && previous.Enabled && !next.Enabled,
                    TimeSpan.FromSeconds(10));
                previous.PerformClick();
                PumpUntil(() => details.Rows.Count == 200
                    && details.Columns[detailColumn].HeaderCell.SortGlyphDirection == SortOrder.Ascending && next.Enabled,
                    TimeSpan.FromSeconds(10));

                // 同じ見出し・型の別院區頁面沿用方向；頁面明細數量隨院區改變。
                TabControl campuses = Assert.IsType<TabControl>(view.Controls.Find("tabCampuses", true).Single());
                campuses.SelectedIndex = 1;
                bool hpkBound = PumpUntilCondition(
                    () => details.Rows.Count == 3
                        && details.Columns[secondDetail.DataPropertyName].HeaderCell.SortGlyphDirection == SortOrder.Ascending,
                    TimeSpan.FromSeconds(10));
                Assert.True(hpkBound, "HPK210明細排序狀態未恢復。" + GetDetailSortDiagnostic(view, details));
                Assert.Equal(SortOrder.Ascending, details.Columns[secondDetail.DataPropertyName].HeaderCell.SortGlyphDirection);
                Assert.Equal("VALUE", details.Columns[secondDetail.DataPropertyName].HeaderText);

                using System.Drawing.Bitmap preview = new(view.Width, view.Height);
                view.DrawToBitmap(preview, new(0, 0, view.Width, view.Height));
                preview.Save(Path.Combine(AppContext.BaseDirectory, "alerts-grid-sort-preview.png"));

                // 增加原欄名的新院區結構解除欄位置相關排序，避免把第二個VALUE誤套至舊欄位。
                campuses.SelectedIndex = 2;
                PumpUntil(() => details.Columns.Cast<DataGridViewColumn>().Count(column => column.Visible) == 3,
                    TimeSpan.FromSeconds(10));
                Assert.Equal(SortOrder.None, details.Columns[secondDetail.DataPropertyName].HeaderCell.SortGlyphDirection);
                view.Close();
            }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(25)), "Grid排序STA檢查逾時。");
        failure?.Throw();
    }

    /// <summary>建立固定假規則，供三個管理欄位排序使用。</summary>
    /// <param name="name">不含個資的唯一顯示名稱，不修改。</param>
    /// <returns>可保存且停用的每週規則。</returns>
    /// <remarks>UI：無；測試資料由 AlertService 載入至 FrmAlerts 的 dgvRules。</remarks>
    private static AlertRule CreateRule(string name)
    {
        // 1151027 codex しずくちゃん：不連接正式設定或Oracle，建立格式有效的假規則。
        return new() { Name = name, Category = "排序測試", Purpose = "排序測試用途", Sql = "select 1 from dual",
            Campuses = ["KTGH00", "HPK210", "TSHIS"], Enabled = false };
    }

    /// <summary>搜尋指定名稱的Grid並確認畫面只有一個對應控制項。</summary>
    /// <param name="view">已建立的FrmAlerts Form，不修改。</param>
    /// <param name="name">Designer中的唯一DataGridView Name，不可為null。</param>
    /// <returns>Form內指定名稱的Grid。</returns>
    /// <exception cref="InvalidOperationException">Form未包含唯一指定Grid時由Single拋出。</exception>
    /// <remarks>UI：由本測試操作FrmAlerts的dgvResults、dgvRules、dgvHistory或dgvDetails。</remarks>
    private static DataGridView FindGrid(FrmAlerts view, string name)
    {
        // 1151027 codex しずくちゃん：實際使用Designer控制項驗證表頭事件及資料繫結。
        return Assert.IsType<DataGridView>(view.Controls.Find(name, true).Single());
    }

    /// <summary>透過WinForms受保護表頭事件實際觸發Grid的ColumnHeaderMouseClick排序路徑。</summary>
    /// <param name="grid">已繫結DataTable且具可見欄位的Grid，不修改來源資料。</param>
    /// <param name="dataPropertyName">待點擊的內部資料欄位名稱，不可為null。</param>
    /// <exception cref="InvalidOperationException">Grid不存在有效的WinForms表頭事件入口時拋出。</exception>
    /// <remarks>UI：本測試直接以程式模擬使用者左鍵點擊FrmAlerts四個Grid的欄位表頭。</remarks>
    private static void ClickHeader(DataGridView grid, string dataPropertyName)
    {
        // 1151027 codex しずくちゃん：事件反射使用WinForms原生OnColumnHeaderMouseClick而非呼叫表單Handler。
        DataGridViewColumn column = grid.Columns.Cast<DataGridViewColumn>()
            .Single(item => item.Visible && item.DataPropertyName == dataPropertyName);
        MethodInfo click = typeof(DataGridView).GetMethod("OnColumnHeaderMouseClick", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("WinForms表頭事件入口不存在。");
        click.Invoke(grid, [new DataGridViewCellMouseEventArgs(column.Index, -1, 4, 4,
            new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0))]);
    }

    /// <summary>依綁定資料的不可見Guid確認目前選取履歷或規則，避免列位置變動造成誤認。</summary>
    /// <param name="grid">具有Id欄及資料列的摘要Grid，不修改。</param>
    /// <returns>目前列的固定Guid，沒有選取列時回傳null。</returns>
    /// <remarks>UI：由本測試讀取FrmAlerts的dgvResults及dgvRules隱藏Id。</remarks>
    private static Guid? SelectedId(DataGridView grid)
    {
        // 1151027 codex しずくちゃん：排序前後都以固定Guid核對目前執行對象。
        return grid.CurrentRow?.Cells["Id"].Value is Guid id ? id : null;
    }

    /// <summary>驗證指定文字欄位的可見列按預期順序顯示。</summary>
    /// <param name="grid">已排序且含指定文字欄位的Grid，不修改。</param>
    /// <param name="columnName">可見資料欄名稱，不可為null。</param>
    /// <param name="expected">從第一列開始的預期文字值，不可為null。</param>
    /// <remarks>UI：由本測試檢查FrmAlerts四個Grid的實際畫面列順序。</remarks>
    private static void AssertOrderedNames(DataGridView grid, string columnName, string[] expected)
    {
        // 1151027 codex しずくちゃん：儲存格顯示順序與識別欄分開核對。
        Assert.Equal(expected, grid.Rows.Cast<DataGridViewRow>()
            .Select(row => Convert.ToString(row.Cells[columnName].Value)).ToArray());
    }

    /// <summary>以STA訊息迴圈等待資料繫結或非同步明細顯示完成。</summary>
    /// <param name="condition">無副作用的完成判斷函式，不可為null。</param>
    /// <param name="timeout">等待上限，需為正數。</param>
    /// <exception cref="TimeoutException">畫面於指定時間內未達到預期資料列數時拋出。</exception>
    /// <remarks>UI：只在FrmAlerts測試專用STA上泵送Windows訊息；不呼叫服務或DB。</remarks>
    private static void PumpUntil(Func<bool> condition, TimeSpan timeout)
    {
        // 1151027 codex しずくちゃん：使用短時限Windows訊息迴圈穩定等待STA資料繫結。
        DateTime limit = DateTime.UtcNow.Add(timeout);
        while (!condition() && DateTime.UtcNow < limit)
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(5);
        }
        Assert.True(condition(), "Grid事件或資料繫結未於時限內完成。");
    }

    /// <summary>於STA訊息迴圈等待明細資料列與排序箭頭一併更新。</summary>
    /// <param name="condition">檢查明細繫結及排序狀態的函式，不可為null。</param>
    /// <param name="timeout">等待期限，必須為正值。</param>
    /// <returns>期限內完成時回傳true，逾時時回傳false。</returns>
    /// <remarks>UI：等待FrmAlerts的dgvDetails完成院區切換，不讀取或輸出明細資料值。</remarks>
    private static bool PumpUntilCondition(Func<bool> condition, TimeSpan timeout)
    {
        // 1151027 codex ツバサちゃん：以同一STA訊息迴圈等待繫結及排序一起完成，不依賴固定延遲。
        DateTime limit = DateTime.UtcNow.Add(timeout);
        while (!condition() && DateTime.UtcNow < limit)
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(5);
        }
        return condition();
    }

    /// <summary>取得只含欄名、型別與排序鍵的明細排序診斷資訊。</summary>
    /// <param name="view">目前STA執行緒上的FrmAlerts，不修改。</param>
    /// <param name="grid">顯示明細的Grid，不修改。</param>
    /// <returns>列數、欄位型別與排序狀態的診斷文字，不包含儲存格資料。</returns>
    /// <remarks>UI：測試失敗時讀取FrmAlerts的dgvDetails與排序狀態欄位，不輸出明細內容。</remarks>
    private static string GetDetailSortDiagnostic(FrmAlerts view, DataGridView grid)
    {
        // 1151027 codex ツバサちゃん：失敗診斷只保留排序鍵及欄位結構，不記錄明細資料值。
        Dictionary<DataGridView, (string Column, bool Descending)>? sorts = typeof(FrmAlerts)
            .GetField("_gridSorts", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(view)
            as Dictionary<DataGridView, (string Column, bool Descending)>;
        (string Name, string DataType)[]? schema = typeof(FrmAlerts)
            .GetField("_detailSchema", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(view)
            as (string Name, string DataType)[];
        string sortState = sorts is not null && sorts.TryGetValue(grid, out var sort)
            ? $"{sort.Column}:{(sort.Descending ? "Descending" : "Ascending")}" : "none";
        string schemaState = schema is null ? "none" : string.Join(",", schema.Select(column => $"{column.Name}/{column.DataType}"));
        string gridColumns = string.Join(",", grid.Columns.Cast<DataGridViewColumn>()
            .Where(column => column.Visible).Select(column => $"{column.DataPropertyName}/{column.ValueType?.Name}/{column.HeaderCell.SortGlyphDirection}"));
        return $" rows={grid.Rows.Count}; sort={sortState}; schema={schemaState}; visible={gridColumns}";
    }
}

/// <summary>避免此整合測試與其他WinForms STA測試同時建立或銷毀視窗，隔離Windows視窗訊息與全域樣式狀態。</summary>
/// <remarks>UI：無；由xUnit集合排程間接限制AlertGridSortingAcceptanceTests的執行時機，不停用任何測試或產品檢查。</remarks>
[CollectionDefinition("Alert grid UI acceptance", DisableParallelization = true)]
public sealed class AlertGridUiAcceptanceCollection
{
    // 1151027 codex ツバサちゃん：僅序列化本畫面整合測試，避免與其他STA畫面測試互相干擾。
}
