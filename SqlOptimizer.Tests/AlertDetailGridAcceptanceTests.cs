using System.Data;
using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using Project_auto_push_to_gitlab.Alerts.Presentation;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

/// <summary>以架空資料驗證明細Grid在窄寬畫面及空頁、滿頁的可讀性；不建立服務或DB連線。</summary>
/// <remarks>UI：xUnit於STA直接建立FrmAlerts並操作dgvDetails，涵蓋Designer設定及本頁欄寬顯示。</remarks>
public sealed class AlertDetailGridAcceptanceTests
{
    /// <summary>驗證大量欄位可橫向閱讀、長文不撐爆寬度、末行內容納入計算，並可手動調整及重新計算。</summary>
    /// <param name="viewportWidth">測試Form的用戶端寬度，單位為目前環境像素，不變更。</param>
    /// <param name="rowCount">架空明細筆數，只使用0、2或200，不變更。</param>
    /// <remarks>UI：直接操作FrmAlerts的dgvDetails；其餘Grid只檢查原有Fill設定未變更。截圖僅含固定架空值，存放於測試輸出目錄。</remarks>
    [Theory]
    [InlineData(1100, 0)]
    [InlineData(1100, 2)]
    [InlineData(1100, 200)]
    [InlineData(1600, 0)]
    [InlineData(1600, 2)]
    [InlineData(1600, 200)]
    public void DetailsRemainReadableAndScrollable(int viewportWidth, int rowCount)
    {
        // 1151027 codex しずくちゃん：以實際Form驗證多欄與分頁邊界，不使用患者資料或真實SQL。
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                System.Windows.Forms.Application.EnableVisualStyles();
                using FrmAlerts view = new();
                view.ShowInTaskbar = false;
                view.Opacity = 0;
                view.StartPosition = FormStartPosition.Manual;
                view.Location = new(-30000, -30000);
                view.ClientSize = new(viewportWidth, 780);
                view.Show();
                DataGridView grid = Assert.IsType<DataGridView>(view.Controls.Find("dgvDetails", true).Single());
                using DataTable page = new();
                for (int index = 0; index < 20; index++) page.Columns.Add("column" + index, typeof(string));
                for (int index = 0; index < rowCount; index++)
                {
                    object[] values = Enumerable.Repeat<object>("N", 20).ToArray();
                    values[1] = index == rowCount - 1 ? new string('W', 200) : "短文";
                    values[3] = DBNull.Value;
                    page.Rows.Add(values);
                }
                grid.DataSource = page;
                for (int index = 0; index < grid.Columns.Count; index++)
                {
                    grid.Columns[index].HeaderText = "FLAG";
                    grid.Columns[index].DefaultCellStyle.NullValue = "NULL";
                }
                grid.Columns[1].HeaderText = "DESCRIPTION";
                grid.Columns[2].HeaderText = "LONG_HEADER_" + new string('W', 120);
                grid.Columns[3].HeaderText = "NULL_VALUE";
                MethodInfo resize = typeof(FrmAlerts).GetMethod("ResizeDetailColumns", BindingFlags.Instance | BindingFlags.NonPublic)!;
                resize.Invoke(view, null);
                view.PerformLayout();
                Assert.Equal(DataGridViewAutoSizeColumnsMode.None, grid.AutoSizeColumnsMode);
                Assert.True(grid.AllowUserToResizeColumns);
                Assert.True(grid.ShowCellToolTips);
                Assert.Equal(ScrollBars.Both, grid.ScrollBars);
                Assert.Equal(DataGridViewTriState.False, grid.DefaultCellStyle.WrapMode);
                Assert.Equal(DataGridViewTriState.False, grid.ColumnHeadersDefaultCellStyle.WrapMode);
                Assert.All(grid.Columns.Cast<DataGridViewColumn>(), column =>
                    Assert.InRange(column.Width, (int)Math.Round(80 * grid.DeviceDpi / 96d), (int)Math.Round(360 * grid.DeviceDpi / 96d)));
                Assert.Equal("FLAG", grid.Columns[0].HeaderText);
                Assert.Equal("FLAG", grid.Columns[4].HeaderText);
                Assert.True(grid.Columns[2].Width > grid.Columns[0].Width);
                if (rowCount > 0)
                {
                    Assert.True(grid.Columns[1].Width > grid.Columns[0].Width);
                    Assert.Equal(DBNull.Value, grid.Rows[0].Cells[3].Value);
                    Assert.Equal("NULL", grid.Rows[0].Cells[3].FormattedValue);
                }
                Assert.Equal(rowCount, grid.Rows.Count);
                Assert.True(grid.Columns.GetColumnsWidth(DataGridViewElementStates.Visible) > grid.ClientSize.Width);
                grid.FirstDisplayedScrollingColumnIndex = grid.Columns.Count - 1;
                Assert.True(grid.Columns[grid.Columns.Count - 1].Displayed);
                Assert.True(grid.HorizontalScrollingOffset > 0);
                grid.FirstDisplayedScrollingColumnIndex = 0;
                int originalWidth = grid.Columns[0].Width;
                grid.Columns[0].Width = originalWidth + 40;
                Assert.Equal(originalWidth + 40, grid.Columns[0].Width);
                resize.Invoke(view, null);
                Assert.Equal(originalWidth, grid.Columns[0].Width);
                foreach (string name in new[] { "dgvResults", "dgvRules", "dgvHistory" })
                    Assert.Equal(DataGridViewAutoSizeColumnsMode.Fill,
                        Assert.IsType<DataGridView>(view.Controls.Find(name, true).Single()).AutoSizeColumnsMode);
                using Bitmap preview = new(view.Width, view.Height);
                view.DrawToBitmap(preview, new(0, 0, view.Width, view.Height));
                preview.Save(Path.Combine(AppContext.BaseDirectory, $"alerts-detail-width-{viewportWidth}-{rowCount}.png"));
                if (rowCount > 0)
                {
                    int longWidth = grid.Columns[1].Width;
                    foreach (DataRow row in page.Rows) row[1] = "短文";
                    resize.Invoke(view, null);
                    Assert.True(grid.Columns[1].Width < longWidth);
                }
                view.Close();
            }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)));
        failure?.Throw();
    }
}
