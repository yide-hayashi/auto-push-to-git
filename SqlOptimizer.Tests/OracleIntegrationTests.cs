using Project_auto_push_to_gitlab.SqlOptimizer.Core;
using Project_auto_push_to_gitlab.SqlOptimizer.Oracle;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

public sealed class OracleIntegrationTests
{
    [Fact(Skip = "検証用Oracle、PLAN_TABLE、V$SQL/DBMS_XPLAN権限がある環境でのみ手動実行します。")]
    [Trait("Category", "OracleIntegration")]
    public async Task ExplainAndExecuteFinal_AgainstConfiguredOracle()
    {
        OracleSqlPlanEvaluator evaluator = new(
            new KtccOracleConnectionFactory(),
            new OracleSelectStatementValidator());
        SqlEvaluationRequest request = new(
            "KTGH00",
            "select :value as VALUE from dual",
            [
                new SqlBindParameter(
                    "value",
                    OracleBindDataType.Int32,
                    1)
            ],
            timeoutSeconds: 60,
            statementId: "SO_MANUAL_TEST");

        PlanEvaluationResult estimated =
            await evaluator.ExplainAsync(request, CancellationToken.None);
        FinalExecutionResult actual =
            await evaluator.ExecuteFinalAsync(
                request,
                previewRowLimit: 1,
                CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(estimated.Plan.FormattedPlan));
        Assert.False(string.IsNullOrWhiteSpace(actual.ActualPlan.FormattedPlan));
        Assert.Equal(1, actual.Metrics.RowsRead);
    }
}
