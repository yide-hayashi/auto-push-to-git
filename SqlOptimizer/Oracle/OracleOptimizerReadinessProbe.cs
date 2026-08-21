using Oracle.ManagedDataAccess.Client;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Oracle
{
    public sealed record OracleOptimizerReadinessResult(
        bool IsReady,
        string Message);

    public interface IOracleOptimizerReadinessProbe
    {
        Task<OracleOptimizerReadinessResult> CheckAsync(
            string connectionAlias,
            int timeoutSeconds,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Verifies the Oracle capabilities needed by the optimizer before a
    /// Codex child process is launched. It never runs user SQL: only a
    /// one-row SELECT from DUAL is explained and drained.
    /// </summary>
    public sealed class OracleOptimizerReadinessProbe :
        IOracleOptimizerReadinessProbe
    {
        private const string DiagnosticSql =
            "SELECT /* AUTOCALLGIT_SQL_OPTIMIZER_PREFLIGHT */ 1 AS READY_VALUE FROM DUAL";

        private readonly ISqlPlanEvaluator _planEvaluator;

        public OracleOptimizerReadinessProbe(ISqlPlanEvaluator planEvaluator)
        {
            _planEvaluator = planEvaluator
                ?? throw new ArgumentNullException(nameof(planEvaluator));
        }

        public async Task<OracleOptimizerReadinessResult> CheckAsync(
            string connectionAlias,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            SqlEvaluationRequest request = new(
                connectionAlias,
                DiagnosticSql,
                Array.Empty<SqlBindParameter>(),
                Math.Clamp(timeoutSeconds, 1, 15),
                "AISO_DIAG_" + Guid.NewGuid().ToString("N")[..20]);

            try
            {
                PlanEvaluationResult explain =
                    await _planEvaluator.ExplainAsync(request, cancellationToken)
                        .ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(explain.Plan.FormattedPlan))
                {
                    return new OracleOptimizerReadinessResult(
                        false,
                        "PLAN_TABLEまたはDBMS_XPLAN.DISPLAYから計画を取得できませんでした。");
                }

                FinalExecutionResult actual =
                    await _planEvaluator.ExecuteFinalAsync(
                            request,
                            previewRowLimit: 0,
                            cancellationToken)
                        .ConfigureAwait(false);
                bool displayCursorUnavailable = actual.ActualPlan.Warnings.Any(
                    warning => warning.Contains(
                        "Actual plan unavailable",
                        StringComparison.OrdinalIgnoreCase));
                if (displayCursorUnavailable ||
                    string.IsNullOrWhiteSpace(actual.ActualPlan.FormattedPlan))
                {
                    return new OracleOptimizerReadinessResult(
                        false,
                        "DBMS_XPLAN.DISPLAY_CURSORまたは必要なV$参照権限を確認してください。");
                }

                return new OracleOptimizerReadinessResult(
                    true,
                    "Oracle接続、PLAN_TABLE、DBMS_XPLAN.DISPLAY_CURSORを確認しました。");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (OracleException exception)
            {
                return new OracleOptimizerReadinessResult(
                    false,
                    $"Oracle事前診断に失敗しました（ORA-{Math.Abs(exception.Number):00000}）。PLAN_TABLEとV$参照権限を確認してください。");
            }
            catch (Exception)
            {
                return new OracleOptimizerReadinessResult(
                    false,
                    "Oracle事前診断に失敗しました。接続先、PLAN_TABLE、DBMS_XPLAN権限を確認してください。");
            }
        }
    }
}
