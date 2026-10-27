using System.Globalization;
using System.Text;

namespace Project_auto_push_to_gitlab.Alerts.Presentation;

/// <summary>將有限十進位文字轉為精確排序順位，不轉成decimal或double，避免高精度數字與科學記號失真。</summary>
/// <remarks>UI：無；由AlertGridData間接供FrmAlerts的dgvDetails使用。只建立本頁排序鍵，不修改原始字串、保存資料或存取DB。</remarks>
internal static class ExactNumericSort
{
    /// <summary>當全部非NULL文字均為有限十進位數字時，依數值大小建立相同值共用的昇冪順位。</summary>
    /// <param name="values">不可為null的本頁原始文字，不修改集合或元素；null元素保留NULL，允許正負號、小數點及e或E指數。</param>
    /// <param name="ranks">成功時回傳與values同長度的順位，NULL元素仍為null；失敗時回傳空陣列，不提供部分排序結果。</param>
    /// <returns>全部非NULL值可精確解析時為True，空集合與全NULL亦為True；任一值無效、非有限數值或指數超出Int32範圍時為False。</returns>
    /// <exception cref="ArgumentNullException">values為null時拋出。</exception>
    /// <remarks>UI：無；AlertGridData由FrmAlerts的dgvDetails間接使用順位。順位從零開始，同值共用順位，由呼叫端以原行序保留穩定排序。</remarks>
    internal static bool TryGetRanks(IReadOnlyList<string?> values, out int?[] ranks)
    {
        // 1151027 codex しずくちゃん：以精確十進位比較建立本頁順位，任一無效值即整欄退回文字排序。
        ArgumentNullException.ThrowIfNull(values);
        ranks = new int?[values.Count];
        List<(int Index, NumericValue Value)> numbers = [];
        for (int index = 0; index < values.Count; index++)
        {
            string? text = values[index];
            if (text is null) continue;
            if (!TryParse(text, out NumericValue value))
            {
                ranks = [];
                return false;
            }
            numbers.Add((index, value));
        }
        numbers.Sort((left, right) => Compare(left.Value, right.Value));
        int rank = 0;
        for (int index = 0; index < numbers.Count; index++)
        {
            if (index > 0 && Compare(numbers[index - 1].Value, numbers[index].Value) != 0) rank++;
            ranks[numbers[index].Index] = rank;
        }
        return true;
    }

    /// <summary>解析不變文化的有限十進位文字，正規化符號、有效數字及十進位指數，不展開指數造成的大量零。</summary>
    /// <param name="text">不可為null的原始文字，不修改；允許首尾空白、單一小數點及可選的有號Int32指數，不允許分組符號或特殊浮點值。</param>
    /// <param name="value">成功時為正規化數值；失敗時為預設值，呼叫端不可用於比較。</param>
    /// <returns>至少有一位數字且全部格式有效時為True；空字串、錯誤格式或超出指數範圍時為False。</returns>
    /// <remarks>UI：無；TryGetRanks經AlertGridData間接服務FrmAlerts的dgvDetails，不產生顯示值或改變NULL。</remarks>
    private static bool TryParse(string text, out NumericValue value)
    {
        // 1151027 codex しずくちゃん：分離有效桁與指數，保留超過decimal範圍的數值精度。
        value = default;
        ReadOnlySpan<char> source = text.AsSpan().Trim();
        if (source.IsEmpty) return false;
        int sign = 1;
        if (source[0] is '+' or '-')
        {
            sign = source[0] == '-' ? -1 : 1;
            source = source[1..];
        }
        if (source.IsEmpty) return false;
        int exponent = 0;
        int exponentIndex = source.IndexOfAny('e', 'E');
        if (exponentIndex >= 0)
        {
            if (!int.TryParse(source[(exponentIndex + 1)..], NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out exponent)) return false;
            source = source[..exponentIndex];
        }
        StringBuilder digits = new(source.Length);
        bool decimalPoint = false;
        int fractionalDigits = 0;
        foreach (char character in source)
        {
            if (character == '.' && !decimalPoint)
            {
                decimalPoint = true;
                continue;
            }
            if (character is < '0' or > '9') return false;
            digits.Append(character);
            if (decimalPoint) fractionalDigits++;
        }
        if (digits.Length == 0) return false;
        string significant = digits.ToString().TrimStart('0');
        if (significant.Length == 0)
        {
            value = new(0, "", 0);
            return true;
        }
        int length = significant.Length;
        while (significant[length - 1] == '0') length--;
        long scale = (long)exponent - fractionalDigits + significant.Length - length;
        value = new(sign, significant[..length], scale);
        return true;
    }

    /// <summary>以符號、十進位位數與有效數字精確比較兩個已正規化值，避免指數展開與浮點誤差。</summary>
    /// <param name="left">解析成功的左側數值，不修改。</param>
    /// <param name="right">解析成功的右側數值，不修改。</param>
    /// <returns>左值較小時回傳負數，相等時為零，較大時回傳正數；正負零均視為相同值。</returns>
    /// <remarks>UI：無；TryGetRanks經AlertGridData間接提供FrmAlerts的dgvDetails數值順位，不依賴目前文化。</remarks>
    private static int Compare(NumericValue left, NumericValue right)
    {
        // 1151027 codex しずくちゃん：先比較數量級，再逐桁比較，負數反向處理絕對值順序。
        int comparison = left.Sign.CompareTo(right.Sign);
        if (comparison != 0) return comparison;
        if (left.Sign == 0) return 0;
        comparison = ((long)left.Digits.Length + left.Exponent).CompareTo((long)right.Digits.Length + right.Exponent);
        if (comparison != 0) return comparison * left.Sign;
        int length = Math.Max(left.Digits.Length, right.Digits.Length);
        for (int index = 0; index < length; index++)
        {
            char leftDigit = index < left.Digits.Length ? left.Digits[index] : '0';
            char rightDigit = index < right.Digits.Length ? right.Digits[index] : '0';
            comparison = leftDigit.CompareTo(rightDigit);
            if (comparison != 0) return comparison * left.Sign;
        }
        return 0;
    }

    /// <summary>保存正規化十進位數值；零不含有效桁，其他數值的有效桁不含前導與尾端零。</summary>
    /// <param name="Sign">負數為-1、零為0、正數為1。</param>
    /// <param name="Digits">不含符號及小數點的有效數字；零使用空字串，不可為null。</param>
    /// <param name="Exponent">有效數字整數值乘以十的此冪次，使用Int64避免小數位與Int32指數運算溢位。</param>
    /// <remarks>UI：無；僅由ExactNumericSort內部解析及比較使用，間接服務FrmAlerts的dgvDetails，不保存至設定或履歷。</remarks>
    private readonly record struct NumericValue(int Sign, string Digits, long Exponent);
}
