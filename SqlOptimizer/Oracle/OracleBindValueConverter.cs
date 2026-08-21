using System.Globalization;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Oracle
{
    /// <summary>
    /// Converts the culture-invariant WinForms text representation into the
    /// strongly typed value supplied to ODP.NET.
    /// </summary>
    public static class OracleBindValueConverter
    {
        public static object Convert(OracleBindDataType type, object? value)
        {
            if (value is null)
            {
                return DBNull.Value;
            }

            if (value is not string text)
            {
                return type switch
                {
                    OracleBindDataType.Guid when value is Guid guid =>
                        guid.ToByteArray(),
                    OracleBindDataType.Boolean when value is bool boolean =>
                        (short)(boolean ? 1 : 0),
                    _ => value
                };
            }

            return type switch
            {
                OracleBindDataType.Int16 => short.Parse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture),
                OracleBindDataType.Int32 => int.Parse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture),
                OracleBindDataType.Int64 => long.Parse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture),
                OracleBindDataType.Decimal => decimal.Parse(
                    text,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture),
                OracleBindDataType.Single => float.Parse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture),
                OracleBindDataType.Double => double.Parse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture),
                OracleBindDataType.Date => DateTime.Parse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces |
                    DateTimeStyles.AssumeLocal),
                OracleBindDataType.TimeStamp => DateTime.Parse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces |
                    DateTimeStyles.RoundtripKind),
                OracleBindDataType.TimeStampWithTimeZone =>
                    DateTimeOffset.Parse(
                        text,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces |
                        DateTimeStyles.RoundtripKind),
                OracleBindDataType.TimeStampWithLocalTimeZone =>
                    DateTime.Parse(
                        text,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces |
                        DateTimeStyles.RoundtripKind),
                OracleBindDataType.Raw => ParseBinary(text),
                OracleBindDataType.Blob => ParseBinary(text),
                OracleBindDataType.Guid => Guid.Parse(text).ToByteArray(),
                OracleBindDataType.Boolean =>
                    (short)(bool.Parse(text) ? 1 : 0),
                _ => text
            };
        }

        private static byte[] ParseBinary(string text)
        {
            string compact = text.Replace(
                " ",
                string.Empty,
                StringComparison.Ordinal);
            if (compact.Length > 0 &&
                compact.Length % 2 == 0 &&
                compact.All(Uri.IsHexDigit))
            {
                return System.Convert.FromHexString(compact);
            }

            return System.Convert.FromBase64String(text);
        }
    }
}
