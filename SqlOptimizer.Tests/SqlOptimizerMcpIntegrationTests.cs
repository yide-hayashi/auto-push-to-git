using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Project_auto_push_to_gitlab.SqlOptimizer.Codex;
using Project_auto_push_to_gitlab.SqlOptimizer.Core;
using Project_auto_push_to_gitlab.SqlOptimizer.Mcp;
using Xunit;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqlOptimizerMcpIntegrationCollection
{
    public const string Name = "SQL optimizer MCP integration";
}

[Collection(SqlOptimizerMcpIntegrationCollection.Name)]
public sealed class SqlOptimizerMcpIntegrationTests
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task OfficialClient_RunsApprovedOptimizationWorkflow_WithoutLeakingSecrets()
    {
        using CancellationTokenSource timeout =
            new(TimeSpan.FromSeconds(30));
        using TestDirectory tokenDirectory =
            new("McpIntegrationToken");

        const string bindSecret = "BIND-SECRET-MUST-NOT-LEAK";
        const string resultSecret = "RESULT-SECRET-MUST-NOT-LEAK";
        const string baselineSql = "select :id as VALUE from dual";
        const string candidateSql =
            "select /* optimized candidate */ :id as VALUE from dual";

        InMemoryOptimizationSessionStore store = new();
        FakeSqlPlanEvaluator evaluator = new()
        {
            PreviewSecret = resultSecret
        };
        SqlOptimizationService service = new(
            store,
            evaluator,
            new OracleSelectStatementValidator(),
            new FakeOptimizerClock());

        SqlOptimizationSession draft = await service.CreateDraftAsync(
            new CreateOptimizationSessionRequest(
                "KTGH00",
                baselineSql,
                SqlOptimizationObjective.Balanced,
                new SqlOptimizationLimits(
                    commandTimeoutSeconds: 60,
                    previewRowLimit: 200,
                    maxCandidatesPerRound: 10)),
            timeout.Token);
        SqlOptimizationSession approved = await service.ApproveAsync(
            draft.SessionId,
            [
                new SqlBindParameter(
                    "id",
                    OracleBindDataType.Varchar2,
                    bindSecret,
                    size: 128)
            ],
            timeout.Token);

        SqlOptimizerTokenStore tokenStore =
            new(tokenDirectory.Path);
        string bearerToken = tokenStore.GetOrCreateToken();
        int port = GetAvailableLoopbackPort();

        await using SqlOptimizerMcpHost host = new(
            service,
            tokenStore,
            new SqlOptimizerMcpOptions
            {
                Port = port
            });
        await host.StartAsync(timeout.Token);

        HttpClientTransport transport = new(
            new HttpClientTransportOptions
            {
                Endpoint = host.Endpoint,
                TransportMode = HttpTransportMode.StreamableHttp,
                ConnectionTimeout = TimeSpan.FromSeconds(10),
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {bearerToken}"
                }
            });
        await using McpClient client = await McpClient.CreateAsync(
            transport,
            cancellationToken: timeout.Token);

        IList<McpClientTool> tools =
            await client.ListToolsAsync(cancellationToken: timeout.Token);
        Assert.Equal(
            [
                "sql_optimizer_claim",
                "sql_optimizer_complete",
                "sql_optimizer_get_plan",
                "sql_optimizer_get_session",
                "sql_optimizer_list_approved",
                "sql_optimizer_propose"
            ],
            tools.Select(tool => tool.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());

        List<CallToolResult> responses = new();

        CallToolResult listResult = await client.CallToolAsync(
            "sql_optimizer_list_approved",
            cancellationToken: timeout.Token);
        responses.Add(listResult);
        McpListApprovedResponse list =
            ReadStructured<McpListApprovedResponse>(listResult);
        Assert.Equal(
            approved.SessionId.ToString("D"),
            Assert.Single(list.Sessions).SessionId);

        CallToolResult claimResult = await client.CallToolAsync(
            "sql_optimizer_claim",
            new Dictionary<string, object?>
            {
                ["session_id"] = approved.SessionId.ToString("D"),
                ["agent_id"] = "integration-test-agent"
            },
            cancellationToken: timeout.Token);
        responses.Add(claimResult);
        McpClaimResponse claim =
            ReadStructured<McpClaimResponse>(claimResult);

        Assert.True(claim.Baseline.IsBaseline);
        Assert.Equal("Explained", claim.Baseline.Status);
        Assert.Single(evaluator.ExplainRequests);
        Assert.Empty(evaluator.ExecuteRequests);

        CallToolResult sessionResult = await client.CallToolAsync(
            "sql_optimizer_get_session",
            new Dictionary<string, object?>
            {
                ["session_id"] = approved.SessionId.ToString("D")
            },
            cancellationToken: timeout.Token);
        responses.Add(sessionResult);
        McpGetSessionResponse session =
            ReadStructured<McpGetSessionResponse>(sessionResult);

        Assert.Equal("Running", session.Session.Status);
        Assert.Equal(["ID"], session.Session.ExpectedBindNames);
        Assert.Equal("Varchar2", Assert.Single(session.Session.BindDefinitions).DataType);
        Assert.Single(session.Session.Iterations);

        CallToolResult planResult = await client.CallToolAsync(
            "sql_optimizer_get_plan",
            new Dictionary<string, object?>
            {
                ["session_id"] = approved.SessionId.ToString("D"),
                ["iteration_id"] = claim.Baseline.IterationId,
                ["plan_kind"] = "estimated",
                ["start_line"] = 1,
                ["max_lines"] = 50
            },
            cancellationToken: timeout.Token);
        responses.Add(planResult);
        McpPlanChunkResponse baselinePlan =
            ReadStructured<McpPlanChunkResponse>(planResult);

        Assert.Equal("estimated", baselinePlan.PlanKind);
        Assert.Contains(
            $"Estimated PLAN FOR {baselineSql}",
            baselinePlan.Content,
            StringComparison.Ordinal);

        CallToolResult proposeResult = await client.CallToolAsync(
            "sql_optimizer_propose",
            new Dictionary<string, object?>
            {
                ["session_id"] = approved.SessionId.ToString("D"),
                ["lease_id"] = claim.LeaseId,
                ["sql"] = candidateSql,
                ["diagnosis"] = "Reduce the baseline work.",
                ["change_reason"] = "Exercise the candidate EXPLAIN path.",
                ["semantic_evidence"] =
                    "The bind set, result meaning, and output shape are unchanged."
            },
            cancellationToken: timeout.Token);
        responses.Add(proposeResult);
        McpProposeResponse proposal =
            ReadStructured<McpProposeResponse>(proposeResult);

        Assert.False(proposal.Iteration.IsBaseline);
        Assert.Equal(1, proposal.Iteration.CandidateNumber);
        Assert.Equal("Explained", proposal.Iteration.Status);
        Assert.Equal(2, evaluator.ExplainRequests.Count);
        Assert.Empty(evaluator.ExecuteRequests);

        CallToolResult completeResult = await client.CallToolAsync(
            "sql_optimizer_complete",
            new Dictionary<string, object?>
            {
                ["session_id"] = approved.SessionId.ToString("D"),
                ["lease_id"] = claim.LeaseId,
                ["iteration_id"] = proposal.Iteration.IterationId,
                ["final_analysis"] =
                    "The candidate preserves the approved shape and is selected for final measurement."
            },
            cancellationToken: timeout.Token);
        responses.Add(completeResult);
        McpCompleteResponse completion =
            ReadStructured<McpCompleteResponse>(completeResult);

        Assert.Equal("AwaitingUser", completion.Status);
        Assert.Equal("Completed", completion.Iteration.Status);
        Assert.NotNull(completion.Iteration.ActualPlan);
        Assert.Equal(2, evaluator.ExplainRequests.Count);
        Assert.Single(evaluator.ExecuteRequests);

        SqlOptimizationSession finalSession =
            await service.GetAsync(approved.SessionId, timeout.Token)
            ?? throw new InvalidOperationException(
                "The optimization session disappeared during the MCP workflow.");
        Assert.Equal(
            SqlOptimizationSessionStatus.AwaitingUser,
            finalSession.Status);
        Assert.Null(finalSession.Lease);

        string responseText = string.Join(
            "\n",
            responses.Select(GetAllResponseText));
        Assert.DoesNotContain(
            bindSecret,
            responseText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            resultSecret,
            responseText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            bearerToken,
            responseText,
            StringComparison.Ordinal);
    }

    private static T ReadStructured<T>(CallToolResult result)
        where T : class
    {
        Assert.NotEqual(true, result.IsError);
        Assert.True(
            result.StructuredContent.HasValue,
            "The MCP tool response did not contain structured content.");

        return result.StructuredContent.Value.Deserialize<T>(
                   ResponseJsonOptions)
               ?? throw new InvalidDataException(
                   $"The MCP response could not be deserialized as {typeof(T).Name}.");
    }

    private static string GetAllResponseText(CallToolResult result)
    {
        StringBuilder text = new();
        if (result.StructuredContent.HasValue)
        {
            text.Append(result.StructuredContent.Value.GetRawText());
        }

        foreach (ContentBlock block in result.Content)
        {
            text.Append(
                JsonSerializer.Serialize(
                    block,
                    block.GetType(),
                    ResponseJsonOptions));
        }

        return text.ToString();
    }

    private static int GetAvailableLoopbackPort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
