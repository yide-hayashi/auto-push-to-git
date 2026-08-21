using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Core
{
    public sealed class SqlOptimizationLease
    {
        internal SqlOptimizationLease(
            Guid leaseId,
            string agentId,
            DateTimeOffset acquiredAt,
            DateTimeOffset expiresAt)
        {
            LeaseId = leaseId;
            AgentId = agentId;
            AcquiredAt = acquiredAt;
            ExpiresAt = expiresAt;
        }

        public Guid LeaseId { get; }

        public string AgentId { get; }

        public DateTimeOffset AcquiredAt { get; }

        public DateTimeOffset ExpiresAt { get; }

        public bool IsActive(DateTimeOffset now)
        {
            return now < ExpiresAt;
        }
    }

    public sealed class SqlOptimizationIteration
    {
        internal SqlOptimizationIteration(
            Guid iterationId,
            int roundNumber,
            int candidateNumber,
            bool isBaseline,
            string sql,
            string diagnosis,
            string changeReason,
            string semanticEvidence,
            DateTimeOffset createdAt)
        {
            IterationId = iterationId;
            RoundNumber = roundNumber;
            CandidateNumber = candidateNumber;
            IsBaseline = isBaseline;
            Sql = sql;
            Diagnosis = diagnosis;
            ChangeReason = changeReason;
            SemanticEvidence = semanticEvidence;
            CreatedAt = createdAt;
            Status = SqlIterationStatus.Pending;
            OutputColumns = Array.Empty<SqlOutputColumn>();
            PreviewRows = Array.Empty<SqlResultRow>();
            OutputShapeMatchesBaseline = isBaseline;
        }

        public Guid IterationId { get; }

        public int RoundNumber { get; }

        /// <summary>
        /// Zero is reserved for the baseline. Proposed candidates start at one.
        /// </summary>
        public int CandidateNumber { get; }

        public bool IsBaseline { get; }

        public string Sql { get; }

        public string Diagnosis { get; private set; }

        public string ChangeReason { get; private set; }

        public string SemanticEvidence { get; private set; }

        public SqlIterationStatus Status { get; private set; }

        public PlanSnapshot? EstimatedPlan { get; private set; }

        public PlanSnapshot? ActualPlan { get; private set; }

        public IReadOnlyList<SqlOutputColumn> OutputColumns { get; private set; }

        public bool OutputShapeMatchesBaseline { get; private set; }

        public SqlExecutionMetrics? ExecutionMetrics { get; private set; }

        [JsonIgnore]
        public IReadOnlyList<SqlResultRow> PreviewRows { get; private set; }

        public string? FinalAnalysis { get; private set; }

        public string? ErrorMessage { get; private set; }

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset? CompletedAt { get; private set; }

        internal void CompleteExplanation(
            PlanEvaluationResult result,
            bool outputShapeMatchesBaseline,
            DateTimeOffset completedAt)
        {
            EstimatedPlan = result.Plan;
            OutputColumns = new ReadOnlyCollection<SqlOutputColumn>(
                result.OutputColumns.ToArray());
            OutputShapeMatchesBaseline = outputShapeMatchesBaseline;
            Status = SqlIterationStatus.Explained;
            CompletedAt = completedAt;
            ErrorMessage = null;
        }

        internal void CompleteExecution(
            FinalExecutionResult result,
            string finalAnalysis,
            DateTimeOffset completedAt)
        {
            ActualPlan = result.ActualPlan;
            ExecutionMetrics = result.Metrics;
            PreviewRows = new ReadOnlyCollection<SqlResultRow>(
                result.PreviewRows.ToArray());
            OutputColumns = new ReadOnlyCollection<SqlOutputColumn>(
                result.OutputColumns.ToArray());
            OutputShapeMatchesBaseline = true;
            FinalAnalysis = finalAnalysis;
            Status = SqlIterationStatus.Completed;
            CompletedAt = completedAt;
            ErrorMessage = null;
        }

        internal void Fail(string message, DateTimeOffset completedAt)
        {
            Status = SqlIterationStatus.Failed;
            ErrorMessage = message;
            CompletedAt = completedAt;
        }

        internal void SetExecuting()
        {
            Status = SqlIterationStatus.Executing;
            ErrorMessage = null;
            CompletedAt = null;
        }

        internal static SqlOptimizationIteration Restore(
            Guid iterationId,
            int roundNumber,
            int candidateNumber,
            bool isBaseline,
            string sql,
            string diagnosis,
            string changeReason,
            string semanticEvidence,
            SqlIterationStatus status,
            PlanSnapshot? estimatedPlan,
            PlanSnapshot? actualPlan,
            IReadOnlyList<SqlOutputColumn> outputColumns,
            bool outputShapeMatchesBaseline,
            SqlExecutionMetrics? executionMetrics,
            string? finalAnalysis,
            string? errorMessage,
            DateTimeOffset createdAt,
            DateTimeOffset? completedAt)
        {
            SqlOptimizationIteration iteration = new SqlOptimizationIteration(
                iterationId,
                roundNumber,
                candidateNumber,
                isBaseline,
                sql,
                diagnosis,
                changeReason,
                semanticEvidence,
                createdAt)
            {
                Status = status,
                EstimatedPlan = estimatedPlan,
                ActualPlan = actualPlan,
                OutputColumns = new ReadOnlyCollection<SqlOutputColumn>(outputColumns.ToArray()),
                OutputShapeMatchesBaseline = outputShapeMatchesBaseline,
                ExecutionMetrics = executionMetrics,
                PreviewRows = Array.Empty<SqlResultRow>(),
                FinalAnalysis = finalAnalysis,
                ErrorMessage = errorMessage,
                CompletedAt = completedAt
            };

            return iteration;
        }
    }

    public sealed class SqlOptimizationSession
    {
        private readonly List<SqlOptimizationIteration> _iterations;

        [JsonIgnore]
        private IReadOnlyList<SqlBindParameter> _runtimeBindParameters;
        private IReadOnlyList<SqlBindDefinition> _lastApprovedBindDefinitions;

        private SqlOptimizationSession(
            Guid sessionId,
            string connectionAlias,
            string originalSql,
            IReadOnlyList<string> expectedBindNames,
            SqlOptimizationObjective objective,
            SqlOptimizationLimits limits,
            DateTimeOffset createdAt)
        {
            SessionId = sessionId;
            ConnectionAlias = NormalizeConnectionAlias(connectionAlias);
            OriginalSql = originalSql;
            RoundBaseSql = originalSql;
            ExpectedBindNames = new ReadOnlyCollection<string>(expectedBindNames.ToArray());
            Objective = objective;
            Limits = limits;
            CreatedAt = createdAt;
            UpdatedAt = createdAt;
            Status = SqlOptimizationSessionStatus.Draft;
            RoundNumber = 1;
            _iterations = new List<SqlOptimizationIteration>();
            _runtimeBindParameters = Array.Empty<SqlBindParameter>();
            _lastApprovedBindDefinitions = Array.Empty<SqlBindDefinition>();
        }

        public Guid SessionId { get; }

        public string ConnectionAlias { get; private set; }

        public string OriginalSql { get; private set; }

        public string RoundBaseSql { get; private set; }

        public IReadOnlyList<string> ExpectedBindNames { get; private set; }

        public SqlOptimizationObjective Objective { get; private set; }

        public SqlOptimizationLimits Limits { get; private set; }

        public SqlOptimizationSessionStatus Status { get; private set; }

        public int RoundNumber { get; private set; }

        public SqlApprovalSnapshot? Approval { get; private set; }

        public IReadOnlyList<SqlBindDefinition> LastApprovedBindDefinitions =>
            new ReadOnlyCollection<SqlBindDefinition>(
                _lastApprovedBindDefinitions.ToArray());

        public SqlOptimizationLease? Lease { get; private set; }

        public string? UserFeedback { get; private set; }

        public Guid? AcceptedIterationId { get; private set; }

        public string? ErrorMessage { get; private set; }

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public IReadOnlyList<SqlOptimizationIteration> Iterations =>
            new ReadOnlyCollection<SqlOptimizationIteration>(_iterations.ToArray());

        [JsonIgnore]
        public bool HasRuntimeBindValues => _runtimeBindParameters.Count == ExpectedBindNames.Count;

        public SqlOptimizationIteration? CurrentRoundBaseline =>
            _iterations.LastOrDefault(
                iteration => iteration.RoundNumber == RoundNumber && iteration.IsBaseline);

        public int CurrentRoundCandidateCount =>
            _iterations.Count(
                iteration => iteration.RoundNumber == RoundNumber && !iteration.IsBaseline);

        public int RemainingCandidatesInRound =>
            Math.Max(0, Limits.MaxCandidatesPerRound - CurrentRoundCandidateCount);

        internal static SqlOptimizationSession Create(
            Guid sessionId,
            string connectionAlias,
            SqlValidationResult validation,
            SqlOptimizationObjective objective,
            SqlOptimizationLimits limits,
            DateTimeOffset createdAt)
        {
            return new SqlOptimizationSession(
                sessionId,
                connectionAlias,
                validation.ExecutableSql,
                validation.BindNames,
                objective,
                limits,
                createdAt);
        }

        internal void UpdateDraft(
            string connectionAlias,
            SqlValidationResult validation,
            SqlOptimizationObjective objective,
            SqlOptimizationLimits limits,
            DateTimeOffset now)
        {
            if (Status == SqlOptimizationSessionStatus.Running)
            {
                throw new InvalidSessionStateException(
                    "A running session cannot be edited. Stop it before changing the approved input.");
            }

            if (Status == SqlOptimizationSessionStatus.Accepted ||
                Status == SqlOptimizationSessionStatus.Stopped ||
                Status == SqlOptimizationSessionStatus.Error)
            {
                throw new InvalidSessionStateException(
                    $"A {Status} session cannot be edited.");
            }

            ConnectionAlias = NormalizeConnectionAlias(connectionAlias);
            OriginalSql = validation.ExecutableSql;
            RoundBaseSql = validation.ExecutableSql;
            ExpectedBindNames = new ReadOnlyCollection<string>(validation.BindNames.ToArray());
            Objective = objective;
            Limits = limits;
            Status = SqlOptimizationSessionStatus.Draft;
            RoundNumber = 1;
            Approval = null;
            Lease = null;
            UserFeedback = null;
            AcceptedIterationId = null;
            ErrorMessage = null;
            _iterations.Clear();
            _runtimeBindParameters = Array.Empty<SqlBindParameter>();
            _lastApprovedBindDefinitions = Array.Empty<SqlBindDefinition>();
            UpdatedAt = now;
        }

        internal void Approve(
            IReadOnlyList<SqlBindParameter> bindParameters,
            DateTimeOffset now)
        {
            if (Status != SqlOptimizationSessionStatus.Draft)
            {
                throw new InvalidSessionStateException(
                    $"Only a Draft session can be approved; current status is {Status}.");
            }

            SqlBindDefinition[] definitions = bindParameters
                .Select(parameter => parameter.Definition)
                .ToArray();

            Approval = new SqlApprovalSnapshot(
                ConnectionAlias,
                SqlApprovalFingerprint.HashSql(OriginalSql),
                SqlApprovalFingerprint.HashBindSchema(definitions),
                definitions,
                Limits,
                now);
            _runtimeBindParameters = bindParameters.ToArray();
            _lastApprovedBindDefinitions = definitions;
            Status = SqlOptimizationSessionStatus.Approved;
            ErrorMessage = null;
            UpdatedAt = now;
        }

        internal SqlOptimizationLease Claim(
            string agentId,
            DateTimeOffset now,
            TimeSpan leaseDuration)
        {
            if (string.IsNullOrWhiteSpace(agentId))
            {
                throw new ArgumentException("Agent id is required.", nameof(agentId));
            }

            EnsureApprovalIsCurrent();
            if (!HasRuntimeBindValues)
            {
                throw new InvalidSessionStateException(
                    "Runtime bind values are unavailable. Re-enter them and approve the session.");
            }

            if (Status == SqlOptimizationSessionStatus.Running &&
                Lease is not null &&
                Lease.IsActive(now))
            {
                if (string.Equals(Lease.AgentId, agentId, StringComparison.Ordinal))
                {
                    return Lease;
                }

                throw new SessionLeaseException(
                    $"The session is leased by '{Lease.AgentId}' until {Lease.ExpiresAt:O}.");
            }

            if (Status != SqlOptimizationSessionStatus.Approved &&
                Status != SqlOptimizationSessionStatus.Running)
            {
                throw new InvalidSessionStateException(
                    $"A session in status {Status} cannot be claimed.");
            }

            Lease = new SqlOptimizationLease(
                Guid.NewGuid(),
                agentId.Trim(),
                now,
                now.Add(leaseDuration));
            Status = SqlOptimizationSessionStatus.Running;
            UpdatedAt = now;
            return Lease;
        }

        internal SqlOptimizationIteration BeginBaseline(Guid leaseId, DateTimeOffset now)
        {
            EnsureLease(leaseId, now);
            if (CurrentRoundBaseline is not null)
            {
                throw new InvalidSessionStateException(
                    "The current round already contains a baseline.");
            }

            SqlOptimizationIteration iteration = new SqlOptimizationIteration(
                Guid.NewGuid(),
                RoundNumber,
                0,
                true,
                RoundBaseSql,
                "Baseline plan",
                "No change",
                "Approved SQL baseline",
                now);
            _iterations.Add(iteration);
            UpdatedAt = now;
            return iteration;
        }

        internal SqlOptimizationIteration BeginCandidate(
            Guid leaseId,
            string sql,
            string diagnosis,
            string changeReason,
            string semanticEvidence,
            DateTimeOffset now)
        {
            EnsureLease(leaseId, now);
            if (CurrentRoundBaseline?.Status != SqlIterationStatus.Explained &&
                CurrentRoundBaseline?.Status != SqlIterationStatus.Completed)
            {
                throw new InvalidSessionStateException(
                    "Evaluate the current round baseline before proposing a candidate.");
            }

            int candidateNumber = CurrentRoundCandidateCount + 1;
            if (candidateNumber > Limits.MaxCandidatesPerRound)
            {
                throw new CandidateLimitExceededException(Limits.MaxCandidatesPerRound);
            }

            SqlOptimizationIteration iteration = new SqlOptimizationIteration(
                Guid.NewGuid(),
                RoundNumber,
                candidateNumber,
                false,
                sql,
                diagnosis ?? string.Empty,
                changeReason ?? string.Empty,
                semanticEvidence ?? string.Empty,
                now);
            _iterations.Add(iteration);
            UpdatedAt = now;
            return iteration;
        }

        internal void CompleteExplanation(
            Guid iterationId,
            PlanEvaluationResult result,
            bool outputShapeMatchesBaseline,
            DateTimeOffset now)
        {
            SqlOptimizationIteration iteration = GetIteration(iterationId);
            iteration.CompleteExplanation(result, outputShapeMatchesBaseline, now);
            UpdatedAt = now;
        }

        internal void FailIteration(Guid iterationId, string message, DateTimeOffset now)
        {
            GetIteration(iterationId).Fail(message, now);
            UpdatedAt = now;
        }

        internal void CompleteExecution(
            Guid iterationId,
            FinalExecutionResult result,
            string finalAnalysis,
            DateTimeOffset now)
        {
            SqlOptimizationIteration iteration = GetIteration(iterationId);
            iteration.CompleteExecution(result, finalAnalysis, now);
            Status = SqlOptimizationSessionStatus.AwaitingUser;
            Lease = null;
            UpdatedAt = now;
        }

        internal void BeginFinalExecution(Guid iterationId, DateTimeOffset now)
        {
            SqlOptimizationIteration iteration = GetIteration(iterationId);
            if (iteration.Status != SqlIterationStatus.Explained)
            {
                throw new InvalidSessionStateException(
                    "Final execution requires an explained candidate.");
            }

            iteration.SetExecuting();
            UpdatedAt = now;
        }

        internal void Accept(Guid iterationId, DateTimeOffset now)
        {
            if (Status != SqlOptimizationSessionStatus.AwaitingUser)
            {
                throw new InvalidSessionStateException(
                    $"Only an AwaitingUser session can be accepted; current status is {Status}.");
            }

            SqlOptimizationIteration iteration = GetIteration(iterationId);
            if (iteration.Status != SqlIterationStatus.Completed)
            {
                throw new InvalidSessionStateException(
                    "Only the candidate that completed final execution can be accepted.");
            }

            AcceptedIterationId = iterationId;
            Status = SqlOptimizationSessionStatus.Accepted;
            UpdatedAt = now;
        }

        internal void Continue(Guid baseIterationId, string feedback, DateTimeOffset now)
        {
            if (Status != SqlOptimizationSessionStatus.AwaitingUser)
            {
                throw new InvalidSessionStateException(
                    $"Only an AwaitingUser session can continue; current status is {Status}.");
            }

            SqlOptimizationIteration baseIteration = GetIteration(baseIterationId);
            if (baseIteration.Status != SqlIterationStatus.Explained &&
                baseIteration.Status != SqlIterationStatus.Completed)
            {
                throw new InvalidSessionStateException(
                    "The next round must start from an explained candidate.");
            }

            UserFeedback = feedback ?? string.Empty;
            RoundNumber++;
            RoundBaseSql = baseIteration.Sql;
            Status = SqlOptimizationSessionStatus.Approved;
            Lease = null;
            AcceptedIterationId = null;
            ErrorMessage = null;
            UpdatedAt = now;
        }

        internal void Stop(DateTimeOffset now)
        {
            if (Status == SqlOptimizationSessionStatus.Accepted ||
                Status == SqlOptimizationSessionStatus.Stopped)
            {
                return;
            }

            Status = SqlOptimizationSessionStatus.Stopped;
            Lease = null;
            UpdatedAt = now;
        }

        internal void MarkError(string message, DateTimeOffset now)
        {
            Status = SqlOptimizationSessionStatus.Error;
            ErrorMessage = message;
            Lease = null;
            UpdatedAt = now;
        }

        internal void EnsureLease(Guid leaseId, DateTimeOffset now)
        {
            if (Status != SqlOptimizationSessionStatus.Running ||
                Lease is null ||
                Lease.LeaseId != leaseId)
            {
                throw new SessionLeaseException("The session lease is missing or does not match.");
            }

            if (!Lease.IsActive(now))
            {
                throw new SessionLeaseException("The session lease has expired.");
            }
        }

        internal IReadOnlyList<SqlBindParameter> GetRuntimeBindParameters()
        {
            return _runtimeBindParameters.ToArray();
        }

        internal SqlOptimizationIteration GetIteration(Guid iterationId)
        {
            return _iterations.FirstOrDefault(iteration => iteration.IterationId == iterationId)
                ?? throw new InvalidSessionStateException(
                    $"Iteration '{iterationId}' does not belong to session '{SessionId}'.");
        }

        internal static SqlOptimizationSession Restore(
            Guid sessionId,
            string connectionAlias,
            string originalSql,
            string roundBaseSql,
            IReadOnlyList<string> expectedBindNames,
            SqlOptimizationObjective objective,
            SqlOptimizationLimits limits,
            SqlOptimizationSessionStatus persistedStatus,
            int roundNumber,
            SqlApprovalSnapshot? approval,
            IReadOnlyList<SqlBindDefinition> lastApprovedBindDefinitions,
            SqlOptimizationLease? lease,
            string? userFeedback,
            Guid? acceptedIterationId,
            string? errorMessage,
            DateTimeOffset createdAt,
            DateTimeOffset updatedAt,
            IReadOnlyList<SqlOptimizationIteration> iterations)
        {
            bool requiresReapproval =
                persistedStatus == SqlOptimizationSessionStatus.Approved ||
                persistedStatus == SqlOptimizationSessionStatus.Running ||
                persistedStatus == SqlOptimizationSessionStatus.AwaitingUser;

            SqlOptimizationSession session = new SqlOptimizationSession(
                sessionId,
                connectionAlias,
                originalSql,
                expectedBindNames,
                objective,
                limits,
                createdAt)
            {
                RoundBaseSql = requiresReapproval ? originalSql : roundBaseSql,
                Status = requiresReapproval
                    ? SqlOptimizationSessionStatus.Draft
                    : persistedStatus,
                RoundNumber = requiresReapproval ? Math.Max(1, roundNumber + 1) : roundNumber,
                Approval = requiresReapproval ? null : approval,
                Lease = requiresReapproval ? null : lease,
                UserFeedback = userFeedback,
                AcceptedIterationId = acceptedIterationId,
                ErrorMessage = errorMessage,
                UpdatedAt = updatedAt
            };

            session._iterations.AddRange(iterations);
            session._runtimeBindParameters = Array.Empty<SqlBindParameter>();
            session._lastApprovedBindDefinitions =
                lastApprovedBindDefinitions.Count > 0
                    ? lastApprovedBindDefinitions.ToArray()
                    : approval?.BindDefinitions.ToArray() ??
                      Array.Empty<SqlBindDefinition>();
            return session;
        }

        private void EnsureApprovalIsCurrent()
        {
            if (Approval is null)
            {
                throw new InvalidSessionStateException("The session has not been approved.");
            }

            string runtimeSchemaHash = SqlApprovalFingerprint.HashBindSchema(
                _runtimeBindParameters.Select(parameter => parameter.Definition).ToArray());

            bool matches =
                string.Equals(Approval.ConnectionAlias, ConnectionAlias, StringComparison.Ordinal) &&
                string.Equals(
                    Approval.SqlHash,
                    SqlApprovalFingerprint.HashSql(OriginalSql),
                    StringComparison.Ordinal) &&
                string.Equals(Approval.BindSchemaHash, runtimeSchemaHash, StringComparison.Ordinal) &&
                Approval.Limits.CommandTimeoutSeconds == Limits.CommandTimeoutSeconds &&
                Approval.Limits.PreviewRowLimit == Limits.PreviewRowLimit &&
                Approval.Limits.MaxCandidatesPerRound == Limits.MaxCandidatesPerRound;

            if (!matches)
            {
                throw new InvalidSessionStateException(
                    "The approved SQL, bind schema, connection, or limits have changed.");
            }
        }

        private static string NormalizeConnectionAlias(string connectionAlias)
        {
            if (string.IsNullOrWhiteSpace(connectionAlias))
            {
                throw new ArgumentException("Connection alias is required.", nameof(connectionAlias));
            }

            string normalized = connectionAlias.Trim().ToUpperInvariant();
            if (normalized != "KTGH00" && normalized != "HPK210" && normalized != "TSHIS")
            {
                throw new ArgumentException(
                    "Connection alias must be KTGH00, HPK210, or TSHIS.",
                    nameof(connectionAlias));
            }

            return normalized;
        }
    }
}
