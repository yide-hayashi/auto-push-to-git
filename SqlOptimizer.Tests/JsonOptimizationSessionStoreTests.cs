using Project_auto_push_to_gitlab.SqlOptimizer.Core;
using Project_auto_push_to_gitlab.SqlOptimizer.Persistence;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

public sealed class JsonOptimizationSessionStoreTests
{
    [Fact]
    public async Task SaveAsync_OmitsBindValuesAndResultPreview_AndRestoresAsDraft()
    {
        using TestDirectory directory = new("json-store");
        const string bindSecret = "TOP-SECRET-BIND-271828";
        const string previewSecret = "TOP-SECRET-ROW-314159";
        JsonOptimizationSessionStore store = new(directory.Path);
        FakeSqlPlanEvaluator evaluator = new()
        {
            PreviewSecret = previewSecret
        };
        OptimizerTestHarness harness = new(store, evaluator);
        SqlOptimizationSession session =
            await harness.CreateApprovedSessionAsync(bindValue: bindSecret);
        SessionClaim claim =
            await harness.Service.ClaimAsync(session.SessionId, "persist-agent");
        await harness.Service.EvaluateBaselineAsync(
            session.SessionId,
            claim.LeaseId);
        SqlOptimizationIteration candidate =
            await harness.Service.ProposeCandidateAsync(
                session.SessionId,
                claim.LeaseId,
                new ProposeCandidateRequest(
                    "select /* persisted candidate */ :id as VALUE from dual",
                    "diagnosis",
                    "change reason",
                    "same output"));
        FinalExecutionResult execution =
            await harness.Service.CompleteAsync(
                session.SessionId,
                claim.LeaseId,
                new CompleteOptimizationRequest(
                    candidate.IterationId,
                    "final analysis"));
        Assert.Equal(previewSecret, execution.PreviewRows[0].Values["VALUE"]);

        string sessionPath = Directory
            .EnumerateFiles(directory.Path, "session-*.json")
            .Single();
        string json = await File.ReadAllTextAsync(sessionPath);

        Assert.False(json.Contains(bindSecret, StringComparison.Ordinal));
        Assert.False(json.Contains(previewSecret, StringComparison.Ordinal));
        Assert.False(json.Contains("runtimeBindParameters", StringComparison.OrdinalIgnoreCase));
        Assert.False(json.Contains("previewRows", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("\"expectedBindNames\"", json);
        Assert.Contains("\"actualPlan\"", json);

        JsonOptimizationSessionStore reloadedStore = new(directory.Path);
        SqlOptimizationSession? reloaded =
            await reloadedStore.LoadAsync(session.SessionId);

        Assert.NotNull(reloaded);
        Assert.Equal(SqlOptimizationSessionStatus.Draft, reloaded.Status);
        Assert.False(reloaded.HasRuntimeBindValues);
        Assert.Null(reloaded.Approval);
        SqlBindDefinition bindDefinition =
            Assert.Single(reloaded.LastApprovedBindDefinitions);
        Assert.Equal("ID", bindDefinition.Name);
        Assert.Equal(OracleBindDataType.Varchar2, bindDefinition.DataType);
        Assert.Equal(128, bindDefinition.Size);
        Assert.Null(reloaded.Lease);
        Assert.Contains(
            reloaded.Iterations,
            iteration => iteration.Status == SqlIterationStatus.Completed);
    }
}
