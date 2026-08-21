using System.Collections.Concurrent;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Core
{
    /// <summary>
    /// 管理SQL最佳化工作階段、Oracle計畫評估、候選限制與最終實測流程。
    /// </summary>
    /// <remarks>
    /// UI：由frm_SqlOptimizer經SqlOptimizerUiController及MCP工具間接使用。
    /// 本類別不直接操作UI；Oracle評估在應用程式內採單一序列執行。
    /// </remarks>
    public sealed class SqlOptimizationService : IOptimizationSessionService
    {
        public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(30);

        private static readonly SemaphoreSlim OracleEvaluationGate = new SemaphoreSlim(1, 1);

        private readonly IOptimizationSessionStore _store;
        private readonly ISqlPlanEvaluator _planEvaluator;
        private readonly ISqlTextValidator _validator;
        private readonly IOptimizerClock _clock;
        private readonly ConcurrentDictionary<Guid, SqlOptimizationSession> _sessions =
            new ConcurrentDictionary<Guid, SqlOptimizationSession>();
        private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _sessionGates =
            new ConcurrentDictionary<Guid, SemaphoreSlim>();
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runCancellationSources =
            new ConcurrentDictionary<Guid, CancellationTokenSource>();

        public SqlOptimizationService(
            IOptimizationSessionStore store,
            ISqlPlanEvaluator planEvaluator,
            ISqlTextValidator? validator = null,
            IOptimizerClock? clock = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _planEvaluator = planEvaluator ?? throw new ArgumentNullException(nameof(planEvaluator));
            _validator = validator ?? new OracleSelectStatementValidator();
            _clock = clock ?? new SystemOptimizerClock();
        }

        public async Task<SqlOptimizationSession> CreateDraftAsync(
            CreateOptimizationSessionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            SqlValidationResult validation = _validator.Validate(request.Sql);
            SqlOptimizationSession session = SqlOptimizationSession.Create(
                Guid.NewGuid(),
                request.ConnectionAlias,
                validation,
                request.Objective,
                request.Limits,
                _clock.UtcNow);

            if (!_sessions.TryAdd(session.SessionId, session))
            {
                throw new SqlOptimizerException("Unable to allocate a unique optimization session id.");
            }

            await _store.SaveAsync(session, cancellationToken).ConfigureAwait(false);
            return session;
        }

        public async Task<SqlOptimizationSession> UpdateDraftAsync(
            Guid sessionId,
            UpdateOptimizationSessionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            SqlValidationResult validation = _validator.Validate(request.Sql);
            return await WithSessionLockAsync(
                    sessionId,
                    async session =>
                    {
                        session.UpdateDraft(
                            request.ConnectionAlias,
                            validation,
                            request.Objective,
                            request.Limits,
                            _clock.UtcNow);
                        await _store.SaveAsync(session, cancellationToken).ConfigureAwait(false);
                        return session;
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<SqlOptimizationSession> ApproveAsync(
            Guid sessionId,
            IReadOnlyList<SqlBindParameter> bindParameters,
            CancellationToken cancellationToken = default)
        {
            if (bindParameters is null)
            {
                throw new ArgumentNullException(nameof(bindParameters));
            }

            return await WithSessionLockAsync(
                    sessionId,
                    async session =>
                    {
                        SqlValidationResult validation = _validator.Validate(session.OriginalSql);
                        _validator.EnsureBindingsMatch(validation, bindParameters);
                        session.Approve(bindParameters, _clock.UtcNow);
                        await _store.SaveAsync(session, cancellationToken).ConfigureAwait(false);
                        return session;
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<OptimizationSessionSummary>> ListApprovedAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<SqlOptimizationSession> persisted =
                await _store.LoadAllAsync(cancellationToken).ConfigureAwait(false);

            foreach (SqlOptimizationSession session in persisted)
            {
                _sessions.TryAdd(session.SessionId, session);
            }

            return _sessions.Values
                .Where(session =>
                    session.Status == SqlOptimizationSessionStatus.Approved ||
                    session.Status == SqlOptimizationSessionStatus.AwaitingUser)
                .OrderBy(session => session.UpdatedAt)
                .Select(session => new OptimizationSessionSummary(
                    session.SessionId,
                    session.Status,
                    session.ConnectionAlias,
                    session.Objective,
                    session.RoundNumber,
                    session.UpdatedAt))
                .ToArray();
        }

        public Task<SqlOptimizationSession?> GetAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            return GetOrLoadAsync(sessionId, cancellationToken);
        }

        public async Task<SessionClaim> ClaimAsync(
            Guid sessionId,
            string agentId,
            CancellationToken cancellationToken = default)
        {
            return await WithSessionLockAsync(
                    sessionId,
                    async session =>
                    {
                        Guid? previousLeaseId = session.Lease?.LeaseId;
                        SqlOptimizationLease lease = session.Claim(
                            agentId,
                            _clock.UtcNow,
                            LeaseDuration);
                        if (previousLeaseId != lease.LeaseId ||
                            !_runCancellationSources.ContainsKey(sessionId))
                        {
                            ReplaceRunCancellationSource(sessionId);
                        }

                        await _store.SaveAsync(session, cancellationToken).ConfigureAwait(false);
                        return new SessionClaim(
                            session.SessionId,
                            lease.LeaseId,
                            lease.ExpiresAt,
                            session);
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<SqlOptimizationIteration> EvaluateBaselineAsync(
            Guid sessionId,
            Guid leaseId,
            CancellationToken cancellationToken = default)
        {
            return await WithSessionLockAsync(
                    sessionId,
                    async session =>
                    {
                        using CancellationTokenSource linkedCancellation =
                            CreateRunLinkedCancellationSource(sessionId, cancellationToken);
                        CancellationToken operationToken = linkedCancellation.Token;

                        session.EnsureLease(leaseId, _clock.UtcNow);
                        SqlOptimizationIteration? existing = session.CurrentRoundBaseline;
                        if (existing is not null)
                        {
                            if (existing.Status == SqlIterationStatus.Explained ||
                                existing.Status == SqlIterationStatus.Completed)
                            {
                                return existing;
                            }

                            throw new InvalidSessionStateException(
                                $"The current baseline is in status {existing.Status}.");
                        }

                        SqlOptimizationIteration baseline = session.BeginBaseline(
                            leaseId,
                            _clock.UtcNow);
                        await _store.SaveAsync(session, operationToken).ConfigureAwait(false);

                        try
                        {
                            PlanEvaluationResult result = await ExplainWithGlobalLockAsync(
                                    session,
                                    baseline,
                                    operationToken)
                                .ConfigureAwait(false);
                            session.CompleteExplanation(
                                baseline.IterationId,
                                result,
                                true,
                                _clock.UtcNow);
                            await _store.SaveAsync(session, operationToken).ConfigureAwait(false);
                            return baseline;
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            string safeError = SqlOptimizerErrorSanitizer.ForOperation(
                                exception,
                                "Baseline EXPLAIN");
                            session.FailIteration(
                                baseline.IterationId,
                                safeError,
                                _clock.UtcNow);
                            session.MarkError(safeError, _clock.UtcNow);
                            await _store.SaveAsync(session, CancellationToken.None).ConfigureAwait(false);
                            RemoveRunCancellationSource(sessionId);
                            throw;
                        }
                        catch (OperationCanceledException)
                        {
                            session.FailIteration(
                                baseline.IterationId,
                                "Baseline evaluation was canceled.",
                                _clock.UtcNow);
                            session.MarkError("Baseline evaluation was canceled.", _clock.UtcNow);
                            await _store.SaveAsync(session, CancellationToken.None).ConfigureAwait(false);
                            RemoveRunCancellationSource(sessionId);
                            throw;
                        }
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<SqlOptimizationIteration> ProposeCandidateAsync(
            Guid sessionId,
            Guid leaseId,
            ProposeCandidateRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            SqlValidationResult validation = _validator.Validate(request.Sql);
            return await WithSessionLockAsync(
                    sessionId,
                    async session =>
                    {
                        using CancellationTokenSource linkedCancellation =
                            CreateRunLinkedCancellationSource(sessionId, cancellationToken);
                        CancellationToken operationToken = linkedCancellation.Token;

                        _validator.EnsureBindingsMatch(
                            validation,
                            session.GetRuntimeBindParameters());

                        SqlOptimizationIteration iteration = session.BeginCandidate(
                            leaseId,
                            validation.ExecutableSql,
                            request.Diagnosis,
                            request.ChangeReason,
                            request.SemanticEvidence,
                            _clock.UtcNow);
                        await _store.SaveAsync(session, operationToken).ConfigureAwait(false);

                        try
                        {
                            PlanEvaluationResult result = await ExplainWithGlobalLockAsync(
                                    session,
                                    iteration,
                                    operationToken)
                                .ConfigureAwait(false);
                            IReadOnlyList<SqlOutputColumn> baselineColumns =
                                session.CurrentRoundBaseline?.OutputColumns ??
                                Array.Empty<SqlOutputColumn>();
                            bool shapeMatches = OutputShapesMatch(
                                baselineColumns,
                                result.OutputColumns);
                            session.CompleteExplanation(
                                iteration.IterationId,
                                result,
                                shapeMatches,
                                _clock.UtcNow);
                            await _store.SaveAsync(session, operationToken).ConfigureAwait(false);
                            return iteration;
                        }
                        catch (Exception exception) when (exception is not OperationCanceledException)
                        {
                            string safeError = SqlOptimizerErrorSanitizer.ForOperation(
                                exception,
                                "Candidate EXPLAIN");
                            session.FailIteration(
                                iteration.IterationId,
                                safeError,
                                _clock.UtcNow);
                            await _store.SaveAsync(session, CancellationToken.None).ConfigureAwait(false);
                            throw;
                        }
                        catch (OperationCanceledException)
                        {
                            session.FailIteration(
                                iteration.IterationId,
                                "Candidate evaluation was canceled.",
                                _clock.UtcNow);
                            await _store.SaveAsync(session, CancellationToken.None).ConfigureAwait(false);
                            throw;
                        }
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 驗證指定反覆項目是本回合可安全使用且預估Cost最低的SQL，之後僅執行一次最終實測。
        /// </summary>
        /// <param name="sessionId">準備完成的最佳化工作階段識別碼，必須對應現行工作階段。</param>
        /// <param name="leaseId">Agent目前持有的有效租約識別碼，不可使用其他Agent或過期租約。</param>
        /// <param name="request">包含欲實測反覆項目與最終分析的要求，不可為null；方法不會修改此物件。</param>
        /// <param name="cancellationToken">取消等待或Oracle實測的權杖；取消時不會將工作階段標示為成功。</param>
        /// <returns>最低預估Cost SQL的實測計畫、執行指標、輸出欄位與有限預覽資料。</returns>
        /// <exception cref="ArgumentNullException">request為null時拋出。</exception>
        /// <exception cref="InvalidSessionStateException">工作階段、反覆狀態或最低Cost條件不符合時拋出。</exception>
        /// <exception cref="SqlOutputShapeMismatchException">候選輸出欄位與基準SQL不相容時拋出。</exception>
        /// <remarks>
        /// UI：由frm_SqlOptimizer的btnStartCodex啟動Codex後，經sql_optimizer_complete間接使用。
        /// 結果顯示於gridHistory、txtActualPlan及gridResultPreview。本方法不直接操作UI。
        /// 最低Cost僅指本回合已完成EXPLAIN且輸出相容的項目，包含原始基準SQL；不代表所有可能改寫的全域最小值。
        /// </remarks>
        public async Task<FinalExecutionResult> CompleteAsync(
            Guid sessionId,
            Guid leaseId,
            CompleteOptimizationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            bool finalExecutionStarted = false;
            try
            {
                return await WithSessionLockAsync(
                    sessionId,
                    async session =>
                    {
                        using CancellationTokenSource linkedCancellation =
                            CreateRunLinkedCancellationSource(sessionId, cancellationToken);
                        CancellationToken operationToken = linkedCancellation.Token;

                        session.EnsureLease(leaseId, _clock.UtcNow);
                        SqlOptimizationIteration iteration =
                            session.GetIteration(request.IterationId);

                        if (iteration.RoundNumber != session.RoundNumber)
                        {
                            throw new InvalidSessionStateException(
                                "Final execution must use an iteration from the current round.");
                        }

                        if (iteration.Status != SqlIterationStatus.Explained)
                        {
                            throw new InvalidSessionStateException(
                                "Final execution requires an explained candidate.");
                        }

                        if (!iteration.OutputShapeMatchesBaseline)
                        {
                            throw new SqlOutputShapeMismatchException(
                                "The candidate output columns do not match the current baseline.");
                        }

                        // 1150820 codex しずくちゃん_GPT-5：阻止Agent執行高於本回合最低預估Cost的SQL。
                        EnsureLowestEstimatedCost(session, iteration);

                        bool evaluationGateAcquired = false;
                        try
                        {
                            session.BeginFinalExecution(
                                iteration.IterationId,
                                _clock.UtcNow);
                            finalExecutionStarted = true;
                            await _store.SaveAsync(
                                    session,
                                    CancellationToken.None)
                                .ConfigureAwait(false);

                            SqlEvaluationRequest evaluationRequest =
                                BuildEvaluationRequest(
                                    session,
                                    iteration.Sql,
                                    null);

                            await OracleEvaluationGate
                                .WaitAsync(operationToken)
                                .ConfigureAwait(false);
                            evaluationGateAcquired = true;

                            FinalExecutionResult result = await _planEvaluator.ExecuteFinalAsync(
                                    evaluationRequest,
                                    session.Limits.PreviewRowLimit,
                                    operationToken)
                                .ConfigureAwait(false);

                            if (!OutputShapesMatch(iteration.OutputColumns, result.OutputColumns))
                            {
                                session.FailIteration(
                                    iteration.IterationId,
                                    "Final execution output columns differ from the explained candidate.",
                                    _clock.UtcNow);
                                session.MarkError(
                                    "Final execution output columns differ from the explained candidate.",
                                    _clock.UtcNow);
                                await _store.SaveAsync(session, CancellationToken.None).ConfigureAwait(false);
                                throw new SqlOutputShapeMismatchException(
                                    "Final execution output columns differ from the explained candidate.");
                            }

                            session.CompleteExecution(
                                iteration.IterationId,
                                result,
                                request.FinalAnalysis ?? string.Empty,
                                _clock.UtcNow);
                            await _store.SaveAsync(session, CancellationToken.None).ConfigureAwait(false);
                            return result;
                        }
                        catch (Exception exception) when (
                            finalExecutionStarted &&
                            exception is not OperationCanceledException &&
                            iteration.Status != SqlIterationStatus.Failed)
                        {
                            string safeError = SqlOptimizerErrorSanitizer.ForOperation(
                                exception,
                                "Final SELECT execution");
                            session.FailIteration(
                                iteration.IterationId,
                                safeError,
                                _clock.UtcNow);
                            session.MarkError(safeError, _clock.UtcNow);
                            await _store.SaveAsync(session, CancellationToken.None).ConfigureAwait(false);
                            throw;
                        }
                        catch (OperationCanceledException) when (finalExecutionStarted)
                        {
                            session.FailIteration(
                                iteration.IterationId,
                                "Final execution was canceled.",
                                _clock.UtcNow);
                            session.MarkError(
                                "Final execution was canceled.",
                                _clock.UtcNow);
                            await _store.SaveAsync(session, CancellationToken.None).ConfigureAwait(false);
                            throw;
                        }
                        finally
                        {
                            if (evaluationGateAcquired)
                            {
                                OracleEvaluationGate.Release();
                            }
                        }
                    },
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (finalExecutionStarted)
                {
                    RemoveRunCancellationSource(sessionId);
                }
            }
        }

        public async Task<SqlOptimizationSession> AcceptAsync(
            Guid sessionId,
            Guid iterationId,
            CancellationToken cancellationToken = default)
        {
            SqlOptimizationSession accepted = await WithSessionLockAsync(
                    sessionId,
                    async session =>
                    {
                        session.Accept(iterationId, _clock.UtcNow);
                        await _store.SaveAsync(session, cancellationToken).ConfigureAwait(false);
                        return session;
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            RemoveRunCancellationSource(sessionId);
            return accepted;
        }

        public async Task<SqlOptimizationSession> ContinueAsync(
            Guid sessionId,
            Guid baseIterationId,
            string feedback,
            CancellationToken cancellationToken = default)
        {
            return await WithSessionLockAsync(
                    sessionId,
                    async session =>
                    {
                        session.Continue(baseIterationId, feedback, _clock.UtcNow);
                        await _store.SaveAsync(session, cancellationToken).ConfigureAwait(false);
                        return session;
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<SqlOptimizationSession> StopAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            CancelRun(sessionId);
            try
            {
                return await WithSessionLockAsync(
                        sessionId,
                        async session =>
                        {
                            session.Stop(_clock.UtcNow);
                            await _store.SaveAsync(session, CancellationToken.None).ConfigureAwait(false);
                            return session;
                        },
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            finally
            {
                RemoveRunCancellationSource(sessionId);
            }
        }

        private async Task<PlanEvaluationResult> ExplainWithGlobalLockAsync(
            SqlOptimizationSession session,
            SqlOptimizationIteration iteration,
            CancellationToken cancellationToken)
        {
            await OracleEvaluationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await _planEvaluator.ExplainAsync(
                        BuildEvaluationRequest(
                            session,
                            iteration.Sql,
                            CreateStatementId(iteration.IterationId)),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                OracleEvaluationGate.Release();
            }
        }

        private static SqlEvaluationRequest BuildEvaluationRequest(
            SqlOptimizationSession session,
            string sql,
            string? statementId)
        {
            return new SqlEvaluationRequest(
                session.ConnectionAlias,
                sql,
                session.GetRuntimeBindParameters(),
                session.Limits.CommandTimeoutSeconds,
                statementId);
        }

        private async Task<TResult> WithSessionLockAsync<TResult>(
            Guid sessionId,
            Func<SqlOptimizationSession, Task<TResult>> action,
            CancellationToken cancellationToken)
        {
            SemaphoreSlim gate = _sessionGates.GetOrAdd(
                sessionId,
                _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                SqlOptimizationSession session =
                    await GetOrLoadRequiredAsync(sessionId, cancellationToken).ConfigureAwait(false);
                return await action(session).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<SqlOptimizationSession?> GetOrLoadAsync(
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            if (_sessions.TryGetValue(sessionId, out SqlOptimizationSession? cached))
            {
                return cached;
            }

            SqlOptimizationSession? loaded =
                await _store.LoadAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (loaded is null)
            {
                return null;
            }

            return _sessions.GetOrAdd(sessionId, loaded);
        }

        private async Task<SqlOptimizationSession> GetOrLoadRequiredAsync(
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            return await GetOrLoadAsync(sessionId, cancellationToken).ConfigureAwait(false)
                ?? throw new OptimizationSessionNotFoundException(sessionId);
        }

        /// <summary>
        /// 確認選取項目的根節點預估Cost等於本回合所有可執行項目的最低值。
        /// </summary>
        /// <param name="session">目前持有鎖定的最佳化工作階段，不可為null。</param>
        /// <param name="selectedIteration">Agent要求進行最終實測的項目，不可為null。</param>
        /// <exception cref="InvalidSessionStateException">缺少可比較Cost，或選取項目Cost高於本回合最低值時拋出。</exception>
        /// <remarks>
        /// UI：由frm_SqlOptimizer經Codex、MCP與CompleteAsync間接使用，結果反映於gridHistory。
        /// 比較範圍包含本回合基準SQL及輸出欄位相容的候選SQL；Cost相同時允許任一並列最低項目。
        /// </remarks>
        private static void EnsureLowestEstimatedCost(
            SqlOptimizationSession session,
            SqlOptimizationIteration selectedIteration)
        {
            // 1150820 codex しずくちゃん_GPT-5：以執行計畫根節點Cost做決定性比較，並將原始基準SQL納入保護。
            decimal? selectedCost = GetEstimatedRootCost(selectedIteration.EstimatedPlan);
            decimal[] eligibleCosts = session.Iterations
                .Where(iteration =>
                    iteration.RoundNumber == session.RoundNumber &&
                    iteration.Status == SqlIterationStatus.Explained &&
                    iteration.OutputShapeMatchesBaseline)
                .Select(iteration => GetEstimatedRootCost(iteration.EstimatedPlan))
                .Where(cost => cost.HasValue)
                .Select(cost => cost!.Value)
                .ToArray();

            if (!selectedCost.HasValue || eligibleCosts.Length == 0)
            {
                throw new InvalidSessionStateException(
                    "Final execution requires a comparable estimated root cost.");
            }

            decimal lowestCost = eligibleCosts.Min();
            if (selectedCost.Value > lowestCost)
            {
                throw new InvalidSessionStateException(
                    $"The selected estimated cost {selectedCost.Value} is higher than the current-round minimum {lowestCost}.");
            }
        }

        /// <summary>
        /// 從預估執行計畫取得最接近根節點且具有數值的Cost。
        /// </summary>
        /// <param name="plan">準備比較的預估計畫；為null或沒有Cost時允許傳入。</param>
        /// <returns>依節點ID排序後第一個有效Cost；找不到時回傳null。</returns>
        /// <remarks>
        /// UI：由frm_SqlOptimizer的最低Cost驗證與Cost欄位概念間接使用，不直接操作UI。
        /// </remarks>
        private static decimal? GetEstimatedRootCost(PlanSnapshot? plan)
        {
            // 1150820 codex しずくちゃん_GPT-5：與畫面Cost欄位採用相同的根節點取值規則。
            return plan?.Nodes
                .OrderBy(node => node.Id)
                .Select(node => node.Cost)
                .FirstOrDefault(cost => cost.HasValue);
        }

        private static string CreateStatementId(Guid iterationId)
        {
            return "SO" + iterationId.ToString("N").Substring(0, 28);
        }

        private static bool OutputShapesMatch(
            IReadOnlyList<SqlOutputColumn> baseline,
            IReadOnlyList<SqlOutputColumn> candidate)
        {
            if (baseline.Count == 0 || candidate.Count == 0 || baseline.Count != candidate.Count)
            {
                return false;
            }

            for (int index = 0; index < baseline.Count; index++)
            {
                SqlOutputColumn left = baseline[index];
                SqlOutputColumn right = candidate[index];
                if (left.Ordinal != right.Ordinal ||
                    !string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        left.ProviderTypeName,
                        right.ProviderTypeName,
                        StringComparison.OrdinalIgnoreCase) ||
                    left.IsNullable != right.IsNullable ||
                    left.ColumnSize != right.ColumnSize ||
                    left.NumericPrecision != right.NumericPrecision ||
                    left.NumericScale != right.NumericScale)
                {
                    return false;
                }
            }

            return true;
        }

        private CancellationTokenSource CreateRunLinkedCancellationSource(
            Guid sessionId,
            CancellationToken callerToken)
        {
            if (!_runCancellationSources.TryGetValue(
                    sessionId,
                    out CancellationTokenSource? runCancellation))
            {
                throw new InvalidSessionStateException(
                    "The session has no active run cancellation scope. Claim it again.");
            }

            try
            {
                return CancellationTokenSource.CreateLinkedTokenSource(
                    callerToken,
                    runCancellation.Token);
            }
            catch (ObjectDisposedException)
            {
                throw new InvalidSessionStateException(
                    "The session run has already ended.");
            }
        }

        private void ReplaceRunCancellationSource(Guid sessionId)
        {
            CancellationTokenSource replacement = new CancellationTokenSource();
            _runCancellationSources.AddOrUpdate(
                sessionId,
                replacement,
                (_, previous) =>
                {
                    TryCancel(previous);
                    previous.Dispose();
                    return replacement;
                });
        }

        private void CancelRun(Guid sessionId)
        {
            if (_runCancellationSources.TryGetValue(
                    sessionId,
                    out CancellationTokenSource? cancellation))
            {
                TryCancel(cancellation);
            }
        }

        private void RemoveRunCancellationSource(Guid sessionId)
        {
            if (_runCancellationSources.TryRemove(
                    sessionId,
                    out CancellationTokenSource? cancellation))
            {
                cancellation.Dispose();
            }
        }

        private static void TryCancel(CancellationTokenSource cancellation)
        {
            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // A concurrent terminal transition already disposed the scope.
            }
        }
    }
}
