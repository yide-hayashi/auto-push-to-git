using System.Globalization;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;
using Project_auto_push_to_gitlab.SqlOptimizer.Oracle;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

public sealed class OracleBindValueConverterTests
{
    [Theory]
    [InlineData(OracleBindDataType.Int16, "12", typeof(short), "12")]
    [InlineData(OracleBindDataType.Int32, "1234", typeof(int), "1234")]
    [InlineData(OracleBindDataType.Int64, "9876543210", typeof(long), "9876543210")]
    [InlineData(OracleBindDataType.Decimal, "1234.50", typeof(decimal), "1234.50")]
    [InlineData(OracleBindDataType.Single, "1.25", typeof(float), "1.25")]
    [InlineData(OracleBindDataType.Double, "2.5", typeof(double), "2.5")]
    public void Convert_ParsesInvariantNumericTypes(
        OracleBindDataType type,
        string text,
        Type expectedType,
        string expected)
    {
        object converted = OracleBindValueConverter.Convert(type, text);

        Assert.IsType(expectedType, converted);
        Assert.Equal(
            expected,
            System.Convert.ToString(converted, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Convert_ParsesTimestampWithTimeZone()
    {
        object converted = OracleBindValueConverter.Convert(
            OracleBindDataType.TimeStampWithTimeZone,
            "2026-07-24T16:30:45+08:00");

        DateTimeOffset timestamp = Assert.IsType<DateTimeOffset>(converted);
        Assert.Equal(TimeSpan.FromHours(8), timestamp.Offset);
        Assert.Equal(2026, timestamp.Year);
    }

    [Fact]
    public void Convert_ParsesGuidBooleanAndBinary()
    {
        Guid guid = Guid.Parse("d96b83ca-17ac-4c39-b38a-9357256005fb");

        Assert.Equal(
            guid.ToByteArray(),
            Assert.IsType<byte[]>(
                OracleBindValueConverter.Convert(
                    OracleBindDataType.Guid,
                    guid.ToString("D"))));
        Assert.Equal(
            (short)1,
            OracleBindValueConverter.Convert(
                OracleBindDataType.Boolean,
                "true"));
        Assert.Equal(
            new byte[] { 0xCA, 0xFE, 0x01 },
            Assert.IsType<byte[]>(
                OracleBindValueConverter.Convert(
                    OracleBindDataType.Raw,
                    "CA FE 01")));
    }

    [Fact]
    public void Convert_NullReturnsDbNull_AndInvalidValuesFailClosed()
    {
        Assert.Same(
            DBNull.Value,
            OracleBindValueConverter.Convert(
                OracleBindDataType.Int32,
                null));
        Assert.Throws<FormatException>(
            () => OracleBindValueConverter.Convert(
                OracleBindDataType.Int32,
                "1,234"));
        Assert.Throws<FormatException>(
            () => OracleBindValueConverter.Convert(
                OracleBindDataType.Boolean,
                "yes"));
    }
}
