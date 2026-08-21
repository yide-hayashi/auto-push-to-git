using Project_auto_push_to_gitlab.SqlOptimizer.Core;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Persistence
{
    internal sealed class PersistedOptimizationSession
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public Guid SessionId { get; set; }

        public string ConnectionAlias { get; set; } = string.Empty;

        public string OriginalSql { get; set; } = string.Empty;

        public string RoundBaseSql { get; set; } = string.Empty;

        public List<string> ExpectedBindNames { get; set; } = new List<string>();

        public SqlOptimizationObjective Objective { get; set; }

        public PersistedLimits Limits { get; set; } = new PersistedLimits();

        public SqlOptimizationSessionStatus Status { get; set; }

        public int RoundNumber { get; set; }

        public PersistedApproval? Approval { get; set; }

        public List<PersistedBindDefinition> LastApprovedBindDefinitions { get; set; } =
            new List<PersistedBindDefinition>();

        public PersistedLease? Lease { get; set; }

        public string? UserFeedback { get; set; }

        public Guid? AcceptedIterationId { get; set; }

        public string? ErrorMessage { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public List<PersistedIteration> Iterations { get; set; } =
            new List<PersistedIteration>();

        public static PersistedOptimizationSession FromDomain(SqlOptimizationSession session)
        {
            return new PersistedOptimizationSession
            {
                SessionId = session.SessionId,
                ConnectionAlias = session.ConnectionAlias,
                OriginalSql = session.OriginalSql,
                RoundBaseSql = session.RoundBaseSql,
                ExpectedBindNames = session.ExpectedBindNames.ToList(),
                Objective = session.Objective,
                Limits = PersistedLimits.FromDomain(session.Limits),
                Status = session.Status,
                RoundNumber = session.RoundNumber,
                Approval = session.Approval is null
                    ? null
                    : PersistedApproval.FromDomain(session.Approval),
                LastApprovedBindDefinitions = session.LastApprovedBindDefinitions
                    .Select(PersistedBindDefinition.FromDomain)
                    .ToList(),
                Lease = session.Lease is null
                    ? null
                    : PersistedLease.FromDomain(session.Lease),
                UserFeedback = session.UserFeedback,
                AcceptedIterationId = session.AcceptedIterationId,
                ErrorMessage = session.ErrorMessage,
                CreatedAt = session.CreatedAt,
                UpdatedAt = session.UpdatedAt,
                Iterations = session.Iterations
                    .Select(PersistedIteration.FromDomain)
                    .ToList()
            };
        }

        public SqlOptimizationSession ToDomain()
        {
            if (SchemaVersion != CurrentSchemaVersion)
            {
                throw new SqlOptimizerException(
                    $"Unsupported optimization session schema version {SchemaVersion}.");
            }

            return SqlOptimizationSession.Restore(
                SessionId,
                ConnectionAlias,
                OriginalSql,
                string.IsNullOrEmpty(RoundBaseSql) ? OriginalSql : RoundBaseSql,
                ExpectedBindNames,
                Objective,
                Limits.ToDomain(),
                Status,
                Math.Max(1, RoundNumber),
                Approval?.ToDomain(),
                LastApprovedBindDefinitions
                    .Select(definition => definition.ToDomain())
                    .ToArray(),
                Lease?.ToDomain(),
                UserFeedback,
                AcceptedIterationId,
                ErrorMessage,
                CreatedAt,
                UpdatedAt,
                Iterations.Select(iteration => iteration.ToDomain()).ToArray());
        }
    }

    internal sealed class PersistedLimits
    {
        public int CommandTimeoutSeconds { get; set; } = 60;

        public int PreviewRowLimit { get; set; } = 200;

        public int MaxCandidatesPerRound { get; set; } = 10;

        public static PersistedLimits FromDomain(SqlOptimizationLimits limits)
        {
            return new PersistedLimits
            {
                CommandTimeoutSeconds = limits.CommandTimeoutSeconds,
                PreviewRowLimit = limits.PreviewRowLimit,
                MaxCandidatesPerRound = limits.MaxCandidatesPerRound
            };
        }

        public SqlOptimizationLimits ToDomain()
        {
            return new SqlOptimizationLimits(
                CommandTimeoutSeconds,
                PreviewRowLimit,
                MaxCandidatesPerRound);
        }
    }

    internal sealed class PersistedBindDefinition
    {
        public string Name { get; set; } = string.Empty;

        public OracleBindDataType DataType { get; set; }

        public int? Size { get; set; }

        public static PersistedBindDefinition FromDomain(SqlBindDefinition definition)
        {
            return new PersistedBindDefinition
            {
                Name = definition.Name,
                DataType = definition.DataType,
                Size = definition.Size
            };
        }

        public SqlBindDefinition ToDomain()
        {
            return new SqlBindDefinition(Name, DataType, Size);
        }
    }

    internal sealed class PersistedApproval
    {
        public string ConnectionAlias { get; set; } = string.Empty;

        public string SqlHash { get; set; } = string.Empty;

        public string BindSchemaHash { get; set; } = string.Empty;

        public List<PersistedBindDefinition> BindDefinitions { get; set; } =
            new List<PersistedBindDefinition>();

        public PersistedLimits Limits { get; set; } = new PersistedLimits();

        public DateTimeOffset ApprovedAt { get; set; }

        public static PersistedApproval FromDomain(SqlApprovalSnapshot approval)
        {
            return new PersistedApproval
            {
                ConnectionAlias = approval.ConnectionAlias,
                SqlHash = approval.SqlHash,
                BindSchemaHash = approval.BindSchemaHash,
                BindDefinitions = approval.BindDefinitions
                    .Select(PersistedBindDefinition.FromDomain)
                    .ToList(),
                Limits = PersistedLimits.FromDomain(approval.Limits),
                ApprovedAt = approval.ApprovedAt
            };
        }

        public SqlApprovalSnapshot ToDomain()
        {
            return new SqlApprovalSnapshot(
                ConnectionAlias,
                SqlHash,
                BindSchemaHash,
                BindDefinitions.Select(definition => definition.ToDomain()).ToArray(),
                Limits.ToDomain(),
                ApprovedAt);
        }
    }

    internal sealed class PersistedLease
    {
        public Guid LeaseId { get; set; }

        public string AgentId { get; set; } = string.Empty;

        public DateTimeOffset AcquiredAt { get; set; }

        public DateTimeOffset ExpiresAt { get; set; }

        public static PersistedLease FromDomain(SqlOptimizationLease lease)
        {
            return new PersistedLease
            {
                LeaseId = lease.LeaseId,
                AgentId = lease.AgentId,
                AcquiredAt = lease.AcquiredAt,
                ExpiresAt = lease.ExpiresAt
            };
        }

        public SqlOptimizationLease ToDomain()
        {
            return new SqlOptimizationLease(LeaseId, AgentId, AcquiredAt, ExpiresAt);
        }
    }

    internal sealed class PersistedIteration
    {
        public Guid IterationId { get; set; }

        public int RoundNumber { get; set; }

        public int CandidateNumber { get; set; }

        public bool IsBaseline { get; set; }

        public string Sql { get; set; } = string.Empty;

        public string Diagnosis { get; set; } = string.Empty;

        public string ChangeReason { get; set; } = string.Empty;

        public string SemanticEvidence { get; set; } = string.Empty;

        public SqlIterationStatus Status { get; set; }

        public PlanSnapshot? EstimatedPlan { get; set; }

        public PlanSnapshot? ActualPlan { get; set; }

        public List<SqlOutputColumn> OutputColumns { get; set; } =
            new List<SqlOutputColumn>();

        public bool OutputShapeMatchesBaseline { get; set; }

        public SqlExecutionMetrics? ExecutionMetrics { get; set; }

        public string? FinalAnalysis { get; set; }

        public string? ErrorMessage { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset? CompletedAt { get; set; }

        public static PersistedIteration FromDomain(SqlOptimizationIteration iteration)
        {
            return new PersistedIteration
            {
                IterationId = iteration.IterationId,
                RoundNumber = iteration.RoundNumber,
                CandidateNumber = iteration.CandidateNumber,
                IsBaseline = iteration.IsBaseline,
                Sql = iteration.Sql,
                Diagnosis = iteration.Diagnosis,
                ChangeReason = iteration.ChangeReason,
                SemanticEvidence = iteration.SemanticEvidence,
                Status = iteration.Status,
                EstimatedPlan = iteration.EstimatedPlan,
                ActualPlan = iteration.ActualPlan,
                OutputColumns = iteration.OutputColumns.ToList(),
                OutputShapeMatchesBaseline = iteration.OutputShapeMatchesBaseline,
                ExecutionMetrics = iteration.ExecutionMetrics,
                FinalAnalysis = iteration.FinalAnalysis,
                ErrorMessage = iteration.ErrorMessage,
                CreatedAt = iteration.CreatedAt,
                CompletedAt = iteration.CompletedAt
            };
        }

        public SqlOptimizationIteration ToDomain()
        {
            return SqlOptimizationIteration.Restore(
                IterationId,
                RoundNumber,
                CandidateNumber,
                IsBaseline,
                Sql,
                Diagnosis,
                ChangeReason,
                SemanticEvidence,
                Status,
                EstimatedPlan,
                ActualPlan,
                OutputColumns,
                OutputShapeMatchesBaseline,
                ExecutionMetrics,
                FinalAnalysis,
                ErrorMessage,
                CreatedAt,
                CompletedAt);
        }
    }
}
