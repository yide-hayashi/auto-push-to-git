using System.Text.Json;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

public sealed class SqlPlanTextRedactorTests
{
    [Fact]
    public void PlanSnapshot_RemovesPeekedBindValuesBeforeJsonOrMcpProjection()
    {
        const string secret = "PATIENT-SECRET-850321";
        string rawPlan = string.Join(
            "\r\n",
            "Plan hash value: 123456",
            string.Empty,
            "Peeked Binds (identified by position):",
            "--------------------------------------",
            $"   1 - :PATIENT_ID (VARCHAR2(30), CSID=873): '{secret}'",
            string.Empty,
            "Predicate Information (identified by operation id):",
            "---------------------------------------------------",
            "   1 - filter(\"PATIENT_ID\"=:PATIENT_ID)");

        PlanSnapshot snapshot = new(
            SqlPlanKind.Actual,
            rawPlan,
            Array.Empty<PlanNode>(),
            DateTimeOffset.UtcNow);
        string json = JsonSerializer.Serialize(snapshot);

        Assert.DoesNotContain(secret, snapshot.FormattedPlan, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
        Assert.Contains("peeked bind values are intentionally omitted", snapshot.FormattedPlan);
        Assert.Contains("Predicate Information", snapshot.FormattedPlan);
        Assert.Contains("PATIENT_ID\"=:PATIENT_ID", snapshot.FormattedPlan);
    }
}
