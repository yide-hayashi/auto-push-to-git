namespace Project_auto_push_to_gitlab.SqlOptimizer.Core
{
    public sealed class SqlEvaluationRequest
    {
        public SqlEvaluationRequest(
            string connectionAlias,
            string sql,
            IReadOnlyList<SqlBindParameter> bindParameters,
            int timeoutSeconds,
            string? statementId = null)
        {
            ConnectionAlias = connectionAlias ?? throw new ArgumentNullException(nameof(connectionAlias));
            Sql = sql ?? throw new ArgumentNullException(nameof(sql));
            BindParameters = bindParameters ?? throw new ArgumentNullException(nameof(bindParameters));
            TimeoutSeconds = timeoutSeconds;
            StatementId = statementId;
        }

        public string ConnectionAlias { get; }

        public string Sql { get; }

        public IReadOnlyList<SqlBindParameter> BindParameters { get; }

        public int TimeoutSeconds { get; }

        public string? StatementId { get; }
    }

    public interface ISqlPlanEvaluator
    {
        Task<PlanEvaluationResult> ExplainAsync(
            SqlEvaluationRequest request,
            CancellationToken cancellationToken);

        Task<FinalExecutionResult> ExecuteFinalAsync(
            SqlEvaluationRequest request,
            int previewRowLimit,
            CancellationToken cancellationToken);
    }

    public interface IOptimizationSessionStore
    {
        Task<SqlOptimizationSession?> LoadAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SqlOptimizationSession>> LoadAllAsync(
            CancellationToken cancellationToken = default);

        Task SaveAsync(
            SqlOptimizationSession session,
            CancellationToken cancellationToken = default);
    }

    public interface IOptimizerClock
    {
        DateTimeOffset UtcNow { get; }
    }

    public sealed class SystemOptimizerClock : IOptimizerClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    public sealed class CreateOptimizationSessionRequest
    {
        public CreateOptimizationSessionRequest(
            string connectionAlias,
            string sql,
            SqlOptimizationObjective objective,
            SqlOptimizationLimits? limits = null)
        {
            ConnectionAlias = connectionAlias;
            Sql = sql;
            Objective = objective;
            Limits = limits ?? new SqlOptimizationLimits();
        }

        public string ConnectionAlias { get; }

        public string Sql { get; }

        public SqlOptimizationObjective Objective { get; }

        public SqlOptimizationLimits Limits { get; }
    }

    public sealed class UpdateOptimizationSessionRequest
    {
        public UpdateOptimizationSessionRequest(
            string connectionAlias,
            string sql,
            SqlOptimizationObjective objective,
            SqlOptimizationLimits limits)
        {
            ConnectionAlias = connectionAlias;
            Sql = sql;
            Objective = objective;
            Limits = limits;
        }

        public string ConnectionAlias { get; }

        public string Sql { get; }

        public SqlOptimizationObjective Objective { get; }

        public SqlOptimizationLimits Limits { get; }
    }

    public sealed class ProposeCandidateRequest
    {
        public ProposeCandidateRequest(
            string sql,
            string diagnosis,
            string changeReason,
            string semanticEvidence)
        {
            Sql = sql;
            Diagnosis = diagnosis;
            ChangeReason = changeReason;
            SemanticEvidence = semanticEvidence;
        }

        public string Sql { get; }

        public string Diagnosis { get; }

        public string ChangeReason { get; }

        public string SemanticEvidence { get; }
    }

    public sealed class CompleteOptimizationRequest
    {
        public CompleteOptimizationRequest(Guid iterationId, string finalAnalysis)
        {
            IterationId = iterationId;
            FinalAnalysis = finalAnalysis;
        }

        public Guid IterationId { get; }

        public string FinalAnalysis { get; }
    }

    public sealed class SessionClaim
    {
        public SessionClaim(
            Guid sessionId,
            Guid leaseId,
            DateTimeOffset leaseExpiresAt,
            SqlOptimizationSession session)
        {
            SessionId = sessionId;
            LeaseId = leaseId;
            LeaseExpiresAt = leaseExpiresAt;
            Session = session;
        }

        public Guid SessionId { get; }

        public Guid LeaseId { get; }

        public DateTimeOffset LeaseExpiresAt { get; }

        public SqlOptimizationSession Session { get; }
    }

    public sealed class OptimizationSessionSummary
    {
        public OptimizationSessionSummary(
            Guid sessionId,
            SqlOptimizationSessionStatus status,
            string connectionAlias,
            SqlOptimizationObjective objective,
            int roundNumber,
            DateTimeOffset updatedAt)
        {
            SessionId = sessionId;
            Status = status;
            ConnectionAlias = connectionAlias;
            Objective = objective;
            RoundNumber = roundNumber;
            UpdatedAt = updatedAt;
        }

        public Guid SessionId { get; }

        public SqlOptimizationSessionStatus Status { get; }

        public string ConnectionAlias { get; }

        public SqlOptimizationObjective Objective { get; }

        public int RoundNumber { get; }

        public DateTimeOffset UpdatedAt { get; }
    }

    public interface IOptimizationSessionService
    {
        Task<SqlOptimizationSession> CreateDraftAsync(
            CreateOptimizationSessionRequest request,
            CancellationToken cancellationToken = default);

        Task<SqlOptimizationSession> UpdateDraftAsync(
            Guid sessionId,
            UpdateOptimizationSessionRequest request,
            CancellationToken cancellationToken = default);

        Task<SqlOptimizationSession> ApproveAsync(
            Guid sessionId,
            IReadOnlyList<SqlBindParameter> bindParameters,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<OptimizationSessionSummary>> ListApprovedAsync(
            CancellationToken cancellationToken = default);

        Task<SqlOptimizationSession?> GetAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default);

        Task<SessionClaim> ClaimAsync(
            Guid sessionId,
            string agentId,
            CancellationToken cancellationToken = default);

        Task<SqlOptimizationIteration> EvaluateBaselineAsync(
            Guid sessionId,
            Guid leaseId,
            CancellationToken cancellationToken = default);

        Task<SqlOptimizationIteration> ProposeCandidateAsync(
            Guid sessionId,
            Guid leaseId,
            ProposeCandidateRequest request,
            CancellationToken cancellationToken = default);

        Task<FinalExecutionResult> CompleteAsync(
            Guid sessionId,
            Guid leaseId,
            CompleteOptimizationRequest request,
            CancellationToken cancellationToken = default);

        Task<SqlOptimizationSession> AcceptAsync(
            Guid sessionId,
            Guid iterationId,
            CancellationToken cancellationToken = default);

        Task<SqlOptimizationSession> ContinueAsync(
            Guid sessionId,
            Guid baseIterationId,
            string feedback,
            CancellationToken cancellationToken = default);

        Task<SqlOptimizationSession> StopAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default);
    }
}
