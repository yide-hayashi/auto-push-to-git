using System.Globalization;
using Project_auto_push_to_gitlab.Alerts.Presentation;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

/// <summary>驗證明細排序所用有限十進位文字比較的數值精度、空值及整欄退回契約，不連線DB。</summary>
/// <remarks>UI：無；由xUnit呼叫ExactNumericSort，對應AlertGridData間接服務的FrmAlerts之dgvDetails。</remarks>
public sealed class ExactNumericSortTests
{
    /// <summary>驗證數值大小而非字元順序，包含負數、小數及科學記號。</summary>
    /// <remarks>UI：無；xUnit驗證dgvDetails經AlertGridData使用的比較順位。</remarks>
    [Fact]
    public void NumericRanksHandleSignsFractionsAndExponents()
    {
        // 1151027 codex しずくちゃん：確認數值2與10及不同表示格式依實際大小排序。
        string?[] values = ["10", "2", "-10", "-2", ".5", "5e-2", "1E2", "1.01", "-1.1"];
        Assert.True(ExactNumericSort.TryGetRanks(values, out int?[] ranks));
        Assert.Equal(new int?[] { 7, 6, 0, 1, 4, 3, 8, 5, 2 }, ranks);
    }

    /// <summary>驗證超過decimal精度的相鄰38桁整數仍可分辨，且不改動輸入字串。</summary>
    /// <remarks>UI：無；xUnit驗證dgvDetails保存原文與高精度排序的契約。</remarks>
    [Fact]
    public void AdjacentHighPrecisionValuesKeepTheirDifference()
    {
        // 1151027 codex しずくちゃん：以末位不同的38桁數值排除浮點轉換造成的相等誤判。
        string?[] values = ["12345678901234567890123456789012345679", "12345678901234567890123456789012345678",
            "-12345678901234567890123456789012345678", "-12345678901234567890123456789012345679"];
        string?[] original = values.ToArray();
        Assert.True(ExactNumericSort.TryGetRanks(values, out int?[] ranks));
        Assert.Equal(new int?[] { 3, 2, 1, 0 }, ranks);
        Assert.Equal(original, values);
    }

    /// <summary>驗證大正負指數與Int32邊界指數不造成展開、溢位或有限數值捨入。</summary>
    /// <remarks>UI：無；xUnit驗證dgvDetails的科學記號比較，未建立Oracle連線。</remarks>
    [Fact]
    public void ExtremeExponentsCompareWithoutExpansion()
    {
        // 1151027 codex しずくちゃん：比較量級而不建立大量零，保留Oracle及浮點範圍的文字值。
        string?[] values = ["1e308", "1e-308", "1e130", "1e-130", "1e2147483647", "1e-2147483648"];
        Assert.True(ExactNumericSort.TryGetRanks(values, out int?[] ranks));
        Assert.Equal(new int?[] { 4, 1, 3, 2, 5, 0 }, ranks);
    }

    /// <summary>驗證同值共用順位、正負零相等及NULL仍保留，供呼叫端以原行序穩定排序。</summary>
    /// <remarks>UI：無；xUnit驗證AlertGridData使用dgvDetails順位時不將NULL當作零。</remarks>
    [Fact]
    public void EquivalentValuesShareRanksAndNullRemainsNull()
    {
        // 1151027 codex しずくちゃん：同值只給一個順位，原格式與NULL不受影響。
        string?[] values = [null, "-0", "+0.000e20", "0", "1.0", "1e0", "+001.000", "10e-1"];
        Assert.True(ExactNumericSort.TryGetRanks(values, out int?[] ranks));
        Assert.Equal(new int?[] { null, 0, 0, 0, 1, 1, 1, 1 }, ranks);
    }

    /// <summary>驗證空頁及全NULL頁仍建立成功且維持原筆數，不需要數值內容才能顯示。</summary>
    /// <remarks>UI：無；xUnit驗證FrmAlerts的空明細頁可經AlertGridData處理。</remarks>
    [Fact]
    public void EmptyAndAllNullPagesSucceed()
    {
        // 1151027 codex しずくちゃん：空資料不當成轉換失敗，全NULL保留相同長度。
        Assert.True(ExactNumericSort.TryGetRanks([], out int?[] empty));
        Assert.Empty(empty);
        Assert.True(ExactNumericSort.TryGetRanks(new string?[] { null, null }, out int?[] nulls));
        Assert.Equal(new int?[] { null, null }, nulls);
    }

    /// <summary>驗證任一非有限或無效值使整欄失敗，不提供部分數值順位。</summary>
    /// <param name="invalid">必須拒絕的文字，不可為null，不變更。</param>
    /// <remarks>UI：無；xUnit驗證AlertGridData可對FrmAlerts的dgvDetails整欄退回文字排序。</remarks>
    [Theory]
    [InlineData("")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1,000")]
    [InlineData("1.2.3")]
    [InlineData("1e2147483648")]
    [InlineData("1e-2147483649")]
    [InlineData("1e")]
    [InlineData(".")]
    [InlineData("１２")]
    public void InvalidValueRejectsWholeColumn(string invalid)
    {
        // 1151027 codex しずくちゃん：禁止無效值以零或部分成功方式混入數值排序。
        Assert.False(ExactNumericSort.TryGetRanks(new string?[] { "2", invalid, "10" }, out int?[] ranks));
        Assert.Empty(ranks);
    }

    /// <summary>驗證執行緒文化不影響小數點及科學記號的解讀。</summary>
    /// <param name="cultureName">測試使用的文化名稱，不可為null，不變更。</param>
    /// <remarks>UI：無；xUnit暫時設定目前文化並於finally還原，對應dgvDetails的不變文化數值原文。</remarks>
    [Theory]
    [InlineData("fr-FR")]
    [InlineData("ar-SA")]
    public void ComparisonDoesNotDependOnCurrentCulture(string cultureName)
    {
        // 1151027 codex しずくちゃん：明細原文使用不變文化，避免使用者環境改變數值排序。
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            Assert.True(ExactNumericSort.TryGetRanks(new string?[] { "10.5", "2.5", "  +.25e1  " }, out int?[] ranks));
            Assert.Equal(new int?[] { 1, 0, 0 }, ranks);
            Assert.False(ExactNumericSort.TryGetRanks(new string?[] { "2,5" }, out _));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }
}
