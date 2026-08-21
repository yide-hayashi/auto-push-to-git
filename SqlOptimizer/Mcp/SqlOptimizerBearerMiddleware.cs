using System.Net;
using Microsoft.AspNetCore.Http;
using Project_auto_push_to_gitlab.SqlOptimizer.Codex;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Mcp;

/// <summary>
/// Enforces loopback-only access, rejects browser-originated requests, and
/// authenticates MCP requests with the per-installation bearer token.
/// </summary>
public sealed class SqlOptimizerBearerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SqlOptimizerTokenStore _tokenStore;

    public SqlOptimizerBearerMiddleware(
        RequestDelegate next,
        SqlOptimizerTokenStore tokenStore)
    {
        _next = next;
        _tokenStore = tokenStore;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsLoopback(context.Connection.RemoteIpAddress) ||
            !IsLoopbackHost(context.Request.Host.Host))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync(
                "Loopback access only.",
                context.RequestAborted);
            return;
        }

        // Codex CLI is not a browser and does not send Origin. Rejecting it
        // closes the DNS-rebinding/browser path without enabling CORS.
        if (context.Request.Headers.ContainsKey("Origin"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync(
                "Browser-originated requests are not allowed.",
                context.RequestAborted);
            return;
        }

        if (!_tokenStore.IsAuthorized(context.Request.Headers.Authorization))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsync(
                "Bearer authentication is required.",
                context.RequestAborted);
            return;
        }

        await _next(context);
    }

    private static bool IsLoopback(IPAddress? address)
    {
        return address is not null && IPAddress.IsLoopback(address);
    }

    private static bool IsLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out IPAddress? address) &&
               IPAddress.IsLoopback(address);
    }
}
