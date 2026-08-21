using Project_auto_push_to_gitlab.SqlOptimizer.Core;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

/// <summary>
/// 驗證SQL最佳化工作階段狀態、候選限制、安全性與最終實測選取規則。
/// </summary>
/// <remarks>
/// UI：無；針對frm_SqlOptimizer後端的SqlOptimizationService執行無Oracle副作用測試。
/// </remarks>
public sealed class SqlOptimizationServiceTests
{
    [Fact]
    public async Task StateMachine_RunsBaselineCandidateCompleteContinueAndStop()
    {
        OptimizerTestHarness harness = new();
        SqlOptimizationSession session =
            await harness.CreateApprovedSessionAsync();
        Assert.Equal(SqlOptimizationSessionStatus.Approved, session.Status);

        SessionClaim claim = await harness.Service.ClaimAsync(
            session.SessionId,
            "agent-one");
        Assert.Equal(SqlOptimizationSessionStatus.Running, session.Status);

        SqlOptimizationIteration baseline =
            await harness.Service.EvaluateBaselineAsync(
                session.SessionId,
                claim.LeaseId);
        Assert.True(baseline.IsBaseline);
        Assert.Equal(0, baseline.CandidateNumber);
        Assert.Equal(SqlIterationStatus.Explained, baseline.Status);

        const string candidateSql =
            "select /* candidate */ :id as VALUE from dual";
        SqlOptimizationIteration candidate =
            await harness.Service.ProposeCandidateAsync(
                session.SessionId,
                claim.LeaseId,
                new ProposeCandidateRequest(
                    candidateSql,
                    "Reduce work",
                    "Use a more selective access path",
                    "The bind set and output shape are unchanged"));
        Assert.Equal(1, candidate.CandidateNumber);
        Assert.False(candidate.IsBaseline);
        Assert.True(candidate.OutputShapeMatchesBaseline);
        Assert.Equal(SqlIterationStatus.Explained, candidate.Status);

        FinalExecutionResult finalResult =
            await harness.Service.CompleteAsync(
                session.SessionId,
                claim.LeaseId,
                new CompleteOptimizationRequest(
                    candidate.IterationId,
                    "Candidate has the best estimated and actual plan."));

        Assert.Equal(1, finalResult.Metrics.RowsRead);
        Assert.Equal(SqlOptimizationSessionStatus.AwaitingUser, session.Status);
        Assert.Null(session.Lease);
        Assert.Equal(SqlIterationStatus.Completed, candidate.Status);
        Assert.Single(harness.Evaluator.ExecuteRequests);
        OptimizationSessionSummary awaiting =
            Assert.Single(await harness.Service.ListApprovedAsync());
        Assert.Equal(SqlOptimizationSessionStatus.AwaitingUser, awaiting.Status);

        SqlOptimizationSession continued =
            await harness.Service.ContinueAsync(
                session.SessionId,
                candidate.IterationId,
                "Try one more round with lower I/O.");

        Assert.Same(session, continued);
        Assert.Equal(SqlOptimizationSessionStatus.Approved, session.Status);
        Assert.Equal(2, session.RoundNumber);
        Assert.Equal(candidateSql, session.RoundBaseSql);
        Assert.Equal("Try one more round with lower I/O.", session.UserFeedback);
        Assert.Equal(10, session.RemainingCandidatesInRound);

        await harness.Service.StopAsync(session.SessionId);

        Assert.Equal(SqlOptimizationSessionStatus.Stopped, session.Status);
        Assert.Null(session.Lease);
        Assert.Empty(await harness.Service.ListApprovedAsync());
    }

    [Fact]
    public async Task Lease_IsIdempotentForOwner_AndExclusiveForAnotherAgent()
    {
        OptimizerTestHarness harness = new();
        SqlOptimizationSession session =
            await harness.CreateApprovedSessionAsync();

        SessionClaim first =
            await harness.Service.ClaimAsync(session.SessionId, "agent-a");
        SessionClaim repeated =
            await harness.Service.ClaimAsync(session.SessionId, "agent-a");

        Assert.Equal(first.LeaseId, repeated.LeaseId);
        await Assert.ThrowsAsync<SessionLeaseException>(
            () => harness.Service.ClaimAsync(session.SessionId, "agent-b"));
        Assert.Equal(SqlOptimizationSessionStatus.Running, session.Status);
        Assert.Equal("agent-a", session.Lease?.AgentId);
    }

    [Fact]
    public async Task CandidateLimit_AllowsTenCandidatesAndRejectsEleventh()
    {
        OptimizerTestHarness harness = new();
        SqlOptimizationSession session =
            await harness.CreateApprovedSessionAsync(
                limits: new SqlOptimizationLimits(
                    commandTimeoutSeconds: 60,
                    previewRowLimit: 200,
                    maxCandidatesPerRound: 10));
        SessionClaim claim =
            await harness.Service.ClaimAsync(session.SessionId, "limit-agent");
        await harness.Service.EvaluateBaselineAsync(
            session.SessionId,
            claim.LeaseId);

        for (int candidateNumber = 1; candidateNumber <= 10; candidateNumber++)
        {
            SqlOptimizationIteration candidate =
                await harness.Service.ProposeCandidateAsync(
                    session.SessionId,
                    claim.LeaseId,
                    new ProposeCandidateRequest(
                        $"select /* candidate {candidateNumber} */ :id as VALUE from dual",
                        "diagnosis",
                        "change",
                        "same output"));

            Assert.Equal(candidateNumber, candidate.CandidateNumber);
        }

        Assert.Equal(10, session.CurrentRoundCandidateCount);
        Assert.Equal(0, session.RemainingCandidatesInRound);
        await Assert.ThrowsAsync<CandidateLimitExceededException>(
            () => harness.Service.ProposeCandidateAsync(
                session.SessionId,
                claim.LeaseId,
                new ProposeCandidateRequest(
                    "select /* candidate 11 */ :id as VALUE from dual",
                    "diagnosis",
                    "change",
                    "same output")));
        Assert.Equal(10, harness.Evaluator.ExplainRequests.Count - 1);
    }

    /// <summary>
    /// 驗證服務拒絕高Cost項目，並只允許本回合預估根節點Cost最低的SQL進入最終實測。
    /// </summary>
    /// <returns>測試完成時結束的非同步工作；失敗時由xUnit回報例外。</returns>
    /// <remarks>
    /// UI：無；對應frm_SqlOptimizer的gridHistory Cost欄位與Codex自動完成流程。
    /// 使用FakeSqlPlanEvaluator，不連線Oracle且不會寫入資料庫。
    /// </remarks>
    [Fact]
    public async Task CompleteAsync_RejectsHigherCostAndExecutesLowestCostIteration()
    {
        // 1150820 codex しずくちゃん_GPT-5：建立基準Cost 5、高Cost 20與最低Cost 2，驗證最終執行保護。
        FakeSqlPlanEvaluator evaluator = new()
        {
            EstimatedCostSelector = request =>
                request.Sql.Contains("low cost", StringComparison.Ordinal)
                    ? 2m
                    : request.Sql.Contains("high cost", StringComparison.Ordinal)
                        ? 20m
                        : 5m
        };
        OptimizerTestHarness harness = new(evaluator: evaluator);
        SqlOptimizationSession session =
            await harness.CreateApprovedSessionAsync();
        SessionClaim claim =
            await harness.Service.ClaimAsync(session.SessionId, "cost-agent");
        await harness.Service.EvaluateBaselineAsync(
            session.SessionId,
            claim.LeaseId);

        SqlOptimizationIteration highCost =
            await harness.Service.ProposeCandidateAsync(
                session.SessionId,
                claim.LeaseId,
                new ProposeCandidateRequest(
                    "select /* high cost */ :id as VALUE from dual",
                    "diagnosis",
                    "change",
                    "same output"));
        SqlOptimizationIteration lowCost =
            await harness.Service.ProposeCandidateAsync(
                session.SessionId,
                claim.LeaseId,
                new ProposeCandidateRequest(
                    "select /* low cost */ :id as VALUE from dual",
                    "diagnosis",
                    "change",
                    "same output"));

        await Assert.ThrowsAsync<InvalidSessionStateException>(
            () => harness.Service.CompleteAsync(
                session.SessionId,
                claim.LeaseId,
                new CompleteOptimizationRequest(
                    highCost.IterationId,
                    "must reject higher cost")));
        Assert.Empty(evaluator.ExecuteRequests);

        await harness.Service.CompleteAsync(
            session.SessionId,
            claim.LeaseId,
            new CompleteOptimizationRequest(
                lowCost.IterationId,
                "execute the lowest estimated cost"));

        SqlEvaluationRequest executed = Assert.Single(evaluator.ExecuteRequests);
        Assert.Equal(lowCost.Sql, executed.Sql);
    }

    [Fact]
    public async Task InvalidTransitions_AreRejected()
    {
        OptimizerTestHarness harness = new();
        SqlOptimizationSession session =
            await harness.CreateApprovedSessionAsync();

        await Assert.ThrowsAsync<InvalidSessionStateException>(
            () => harness.Service.AcceptAsync(session.SessionId, Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidSessionStateException>(
            () => harness.Service.ContinueAsync(
                session.SessionId,
                Guid.NewGuid(),
                "not ready"));

        SessionClaim claim =
            await harness.Service.ClaimAsync(session.SessionId, "agent");
        await Assert.ThrowsAsync<InvalidSessionStateException>(
            () => harness.Service.ProposeCandidateAsync(
                session.SessionId,
                claim.LeaseId,
                new ProposeCandidateRequest(
                    "select :id as VALUE from dual",
                    "diagnosis",
                    "change",
                    "same output")));
    }

    [Fact]
    public async Task FailedFinalExecution_MovesSessionToErrorAndCannotRunAgain()
    {
        FakeSqlPlanEvaluator evaluator = new();
        OptimizerTestHarness harness = new(evaluator: evaluator);
        SqlOptimizationSession session =
            await harness.CreateApprovedSessionAsync();
        SessionClaim claim =
            await harness.Service.ClaimAsync(session.SessionId, "final-agent");
        await harness.Service.EvaluateBaselineAsync(
            session.SessionId,
            claim.LeaseId);
        SqlOptimizationIteration candidate =
            await harness.Service.ProposeCandidateAsync(
                session.SessionId,
                claim.LeaseId,
                new ProposeCandidateRequest(
                    "select /* final failure */ :id as VALUE from dual",
                    "diagnosis",
                    "change",
                    "same output"));
        evaluator.FinalExecutionException =
            new InvalidOperationException("simulated final failure");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Service.CompleteAsync(
                session.SessionId,
                claim.LeaseId,
                new CompleteOptimizationRequest(
                    candidate.IterationId,
                    "final analysis")));

        Assert.Equal(SqlOptimizationSessionStatus.Error, session.Status);
        Assert.Null(session.Lease);
        Assert.Equal(SqlIterationStatus.Failed, candidate.Status);
        Assert.DoesNotContain(
            "simulated final failure",
            session.ErrorMessage,
            StringComparison.Ordinal);
        Assert.Contains("Error details were omitted", session.ErrorMessage);
        Assert.Single(evaluator.ExecuteRequests);
        await Assert.ThrowsAsync<InvalidSessionStateException>(
            () => harness.Service.CompleteAsync(
                session.SessionId,
                claim.LeaseId,
                new CompleteOptimizationRequest(
                    session.CurrentRoundBaseline!.IterationId,
                    "retry")));
        Assert.Single(evaluator.ExecuteRequests);
    }

    [Fact]
    public async Task RejectedComplete_DoesNotRemoveTheActiveRunCancellationScope()
    {
        OptimizerTestHarness harness = new();
        SqlOptimizationSession session =
            await harness.CreateApprovedSessionAsync();
        SessionClaim claim =
            await harness.Service.ClaimAsync(session.SessionId, "scope-agent");
        await harness.Service.EvaluateBaselineAsync(
            session.SessionId,
            claim.LeaseId);

        await Assert.ThrowsAsync<InvalidSessionStateException>(
            () => harness.Service.CompleteAsync(
                session.SessionId,
                claim.LeaseId,
                new CompleteOptimizationRequest(
                    Guid.NewGuid(),
                    "invalid iteration")));

        SqlOptimizationIteration candidate =
            await harness.Service.ProposeCandidateAsync(
                session.SessionId,
                claim.LeaseId,
                new ProposeCandidateRequest(
                    "select /* still active */ :id as VALUE from dual",
                    "diagnosis",
                    "change",
                    "same output"));

        Assert.Equal(SqlIterationStatus.Explained, candidate.Status);
        Assert.Equal(SqlOptimizationSessionStatus.Running, session.Status);
    }

    [Fact]
    public async Task CandidateWithDifferentTypeMetadata_IsNeverExecuted()
    {
        FakeSqlPlanEvaluator evaluator = new();
        OptimizerTestHarness harness = new(evaluator: evaluator);
        SqlOptimizationSession session =
            await harness.CreateApprovedSessionAsync();
        SessionClaim claim =
            await harness.Service.ClaimAsync(session.SessionId, "shape-agent");
        await harness.Service.EvaluateBaselineAsync(
            session.SessionId,
            claim.LeaseId);
        evaluator.OutputColumns =
        [
            new SqlOutputColumn(
                0,
                "VALUE",
                "NUMBER",
                isNullable: false,
                columnSize: 22,
                numericPrecision: 18,
                numericScale: 2)
        ];

        SqlOptimizationIteration candidate =
            await harness.Service.ProposeCandidateAsync(
                session.SessionId,
                claim.LeaseId,
                new ProposeCandidateRequest(
                    "select /* changed type */ :id as VALUE from dual",
                    "diagnosis",
                    "change",
                    "same output"));

        Assert.False(candidate.OutputShapeMatchesBaseline);
        await Assert.ThrowsAsync<SqlOutputShapeMismatchException>(
            () => harness.Service.CompleteAsync(
                session.SessionId,
                claim.LeaseId,
                new CompleteOptimizationRequest(
                    candidate.IterationId,
                    "must not execute")));
        Assert.Empty(evaluator.ExecuteRequests);
        Assert.Equal(SqlOptimizationSessionStatus.Running, session.Status);
    }

    [Fact]
    public async Task StopAsync_CancelsAnInFlightOracleEvaluation()
    {
        InMemoryOptimizationSessionStore store = new();
        BlockingSqlPlanEvaluator evaluator = new();
        SqlOptimizationService service = new(
            store,
            evaluator,
            new OracleSelectStatementValidator(),
            new FakeOptimizerClock());
        SqlOptimizationSession draft = await service.CreateDraftAsync(
            new CreateOptimizationSessionRequest(
                "KTGH00",
                "select :id as VALUE from dual",
                SqlOptimizationObjective.Balanced));
        SqlOptimizationSession session = await service.ApproveAsync(
            draft.SessionId,
            [
                new SqlBindParameter(
                    "id",
                    OracleBindDataType.Varchar2,
                    "runtime-only")
            ]);
        SessionClaim claim = await service.ClaimAsync(
            session.SessionId,
            "blocking-agent");

        Task<SqlOptimizationIteration> evaluation =
            service.EvaluateBaselineAsync(
                session.SessionId,
                claim.LeaseId);
        await evaluator.Started.WaitAsync(TimeSpan.FromSeconds(5));

        SqlOptimizationSession stopped =
            await service.StopAsync(session.SessionId);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await evaluation);
        Assert.Equal(SqlOptimizationSessionStatus.Stopped, stopped.Status);
        Assert.Null(stopped.Lease);
    }
}
