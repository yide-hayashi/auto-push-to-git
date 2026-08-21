using Project_auto_push_to_gitlab.SqlOptimizer.Core;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Mcp;

public sealed record McpOptimizationLimits(
    int CommandTimeoutSeconds,
    int PreviewRowLimit,
    int MaxCandidatesPerRound);

public sealed record McpBindDefinition(
    string Name,
    string DataType,
    int? Size);

public sealed record McpOutputColumn(
    int Ordinal,
    string Name,
    string ProviderTypeName,
    bool? IsNullable,
    int? ColumnSize,
    int? NumericPrecision,
    int? NumericScale);

public sealed record McpExecutionMetrics(
    double ElapsedMilliseconds,
    long RowsRead,
    long? BufferGets,
    double? CpuMilliseconds);

public sealed record McpPlanSummary(
    string Kind,
    string? PlanHashValue,
    int NodeCount,
    decimal? RootCost,
    IReadOnlyList<string> Warnings);

public sealed record McpIterationSummary(
    string IterationId,
    int RoundNumber,
    int CandidateNumber,
    bool IsBaseline,
    string Status,
    string Sql,
    string Diagnosis,
    string ChangeReason,
    string SemanticEvidence,
    bool OutputShapeMatchesBaseline,
    McpPlanSummary? EstimatedPlan,
    McpPlanSummary? ActualPlan,
    IReadOnlyList<McpOutputColumn> OutputColumns,
    McpExecutionMetrics? ExecutionMetrics,
    string? FinalAnalysis,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record McpSessionSummary(
    string SessionId,
    string Status,
    string ConnectionAlias,
    string Objective,
    int RoundNumber,
    DateTimeOffset UpdatedAt);

public sealed record McpSessionDetail(
    string SessionId,
    string Status,
    string ConnectionAlias,
    string Objective,
    int RoundNumber,
    string OriginalSql,
    string RoundBaseSql,
    IReadOnlyList<string> ExpectedBindNames,
    IReadOnlyList<McpBindDefinition> BindDefinitions,
    McpOptimizationLimits Limits,
    string? UserFeedback,
    string? AcceptedIterationId,
    string? ErrorMessage,
    int CurrentRoundCandidateCount,
    int RemainingCandidatesInRound,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<McpIterationSummary> Iterations);

public sealed record McpListApprovedResponse(
    int Count,
    IReadOnlyList<McpSessionSummary> Sessions);

public sealed record McpClaimResponse(
    string SessionId,
    string LeaseId,
    DateTimeOffset LeaseExpiresAt,
    McpIterationSummary Baseline,
    McpSessionDetail Session,
    string PlanNotice);

public sealed record McpGetSessionResponse(McpSessionDetail Session);

public sealed record McpPlanChunkResponse(
    string SessionId,
    string IterationId,
    string PlanKind,
    int StartLine,
    int ReturnedLines,
    int TotalLines,
    int? NextStartLine,
    bool HasMore,
    string Content,
    IReadOnlyList<string> Warnings);

public sealed record McpProposeResponse(
    string SessionId,
    string LeaseId,
    McpIterationSummary Iteration,
    int RemainingCandidatesInRound,
    string PlanNotice);

public sealed record McpCompleteResponse(
    string SessionId,
    string Status,
    McpIterationSummary Iteration,
    McpExecutionMetrics Metrics,
    McpPlanSummary ActualPlan,
    IReadOnlyList<McpOutputColumn> OutputColumns,
    string NextAction);

internal static class SqlOptimizerMcpProjection
{
    public static McpSessionSummary ToSummary(
        OptimizationSessionSummary summary)
    {
        return new McpSessionSummary(
            summary.SessionId.ToString("D"),
            summary.Status.ToString(),
            summary.ConnectionAlias,
            summary.Objective.ToString(),
            summary.RoundNumber,
            summary.UpdatedAt);
    }

    public static McpSessionDetail ToDetail(SqlOptimizationSession session)
    {
        return new McpSessionDetail(
            session.SessionId.ToString("D"),
            session.Status.ToString(),
            session.ConnectionAlias,
            session.Objective.ToString(),
            session.RoundNumber,
            session.OriginalSql,
            session.RoundBaseSql,
            session.ExpectedBindNames.ToArray(),
            (session.Approval?.BindDefinitions ??
             Array.Empty<SqlBindDefinition>())
                .Select(ToBindDefinition)
                .ToArray(),
            new McpOptimizationLimits(
                session.Limits.CommandTimeoutSeconds,
                session.Limits.PreviewRowLimit,
                session.Limits.MaxCandidatesPerRound),
            session.UserFeedback,
            session.AcceptedIterationId?.ToString("D"),
            session.ErrorMessage,
            session.CurrentRoundCandidateCount,
            session.RemainingCandidatesInRound,
            session.CreatedAt,
            session.UpdatedAt,
            session.Iterations.Select(ToIteration).ToArray());
    }

    public static McpIterationSummary ToIteration(
        SqlOptimizationIteration iteration)
    {
        return new McpIterationSummary(
            iteration.IterationId.ToString("D"),
            iteration.RoundNumber,
            iteration.CandidateNumber,
            iteration.IsBaseline,
            iteration.Status.ToString(),
            iteration.Sql,
            iteration.Diagnosis,
            iteration.ChangeReason,
            iteration.SemanticEvidence,
            iteration.OutputShapeMatchesBaseline,
            ToPlanSummary(iteration.EstimatedPlan),
            ToPlanSummary(iteration.ActualPlan),
            iteration.OutputColumns.Select(ToOutputColumn).ToArray(),
            ToMetrics(iteration.ExecutionMetrics),
            iteration.FinalAnalysis,
            iteration.ErrorMessage,
            iteration.CreatedAt,
            iteration.CompletedAt);
    }

    public static McpPlanSummary? ToPlanSummary(PlanSnapshot? plan)
    {
        if (plan is null)
        {
            return null;
        }

        decimal? rootCost = plan.Nodes
            .FirstOrDefault(node => node.ParentId is null)
            ?.Cost;

        return new McpPlanSummary(
            plan.Kind.ToString(),
            plan.PlanHashValue,
            plan.Nodes.Count,
            rootCost,
            plan.Warnings.ToArray());
    }

    public static McpExecutionMetrics? ToMetrics(
        SqlExecutionMetrics? metrics)
    {
        return metrics is null
            ? null
            : new McpExecutionMetrics(
                metrics.Elapsed.TotalMilliseconds,
                metrics.RowsRead,
                metrics.BufferGets,
                metrics.CpuTime?.TotalMilliseconds);
    }

    public static McpOutputColumn ToOutputColumn(SqlOutputColumn column)
    {
        return new McpOutputColumn(
            column.Ordinal,
            column.Name,
            column.ProviderTypeName,
            column.IsNullable,
            column.ColumnSize,
            column.NumericPrecision,
            column.NumericScale);
    }

    private static McpBindDefinition ToBindDefinition(
        SqlBindDefinition definition)
    {
        return new McpBindDefinition(
            definition.Name,
            definition.DataType.ToString(),
            definition.Size);
    }
}
