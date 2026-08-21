using System.Text.Json;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

public sealed class SensitiveJsonSerializationTests
{
    [Fact]
    public void SqlBindParameter_DoesNotSerializeRuntimeValueOrNullState()
    {
        const string secret = "BIND-SECRET-2f0f4df4";
        SqlBindParameter parameter = new(
            "patient_id",
            OracleBindDataType.Varchar2,
            secret,
            isNull: false,
            size: 64);

        string json = JsonSerializer.Serialize(parameter);

        Assert.False(json.Contains(secret, StringComparison.Ordinal));
        Assert.False(json.Contains("\"Value\"", StringComparison.OrdinalIgnoreCase));
        Assert.False(json.Contains("\"IsNull\"", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("PATIENT_ID", json);
    }

    [Fact]
    public void SqlResultRow_DoesNotSerializePreviewValues()
    {
        const string secret = "RESULT-SECRET-e761d61e";
        SqlResultRow row = new(
            new Dictionary<string, object?>
            {
                ["PATIENT_NAME"] = secret
            });

        string json = JsonSerializer.Serialize(row);

        Assert.False(json.Contains(secret, StringComparison.Ordinal));
        Assert.False(json.Contains("PATIENT_NAME", StringComparison.Ordinal));
    }
}
