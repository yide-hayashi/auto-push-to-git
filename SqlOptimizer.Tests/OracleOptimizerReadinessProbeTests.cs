using Project_auto_push_to_gitlab.SqlOptimizer.Oracle;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

public sealed class OracleOptimizerReadinessProbeTests
{
    [Fact]
    public async Task CheckAsync_ExplainsAndExecutesOnlyTheDualDiagnostic()
    {
        FakeSqlPlanEvaluator evaluator = new();
        OracleOptimizerReadinessProbe probe = new(evaluator);

        OracleOptimizerReadinessResult result =
            await probe.CheckAsync("KTGH00", 60, CancellationToken.None);

        Assert.True(result.IsReady);
        Assert.Single(evaluator.ExplainRequests);
        Assert.Single(evaluator.ExecuteRequests);
        Assert.Equal(15, evaluator.ExplainRequests[0].TimeoutSeconds);
        Assert.Contains("FROM DUAL", evaluator.ExplainRequests[0].Sql);
        Assert.Empty(evaluator.ExplainRequests[0].BindParameters);
    }

    [Fact]
    public async Task CheckAsync_ReportsDisplayCursorPermissionFailure()
    {
        FakeSqlPlanEvaluator evaluator = new()
        {
            ActualPlanWarnings =
            [
                "Actual plan unavailable. Verify catalog/V$ privileges."
            ]
        };
        OracleOptimizerReadinessProbe probe = new(evaluator);

        OracleOptimizerReadinessResult result =
            await probe.CheckAsync("KTGH00", 10, CancellationToken.None);

        Assert.False(result.IsReady);
        Assert.Contains("DISPLAY_CURSOR", result.Message);
    }
}
