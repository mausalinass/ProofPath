using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace ProofPath.Api.Production;

public static class HealthResponse
{
    public static readonly HealthCheckOptions Live = new()
    {
        Predicate = _ => false,
        ResponseWriter = Write
    };

    public static readonly HealthCheckOptions Ready = new() { ResponseWriter = Write };

    private static Task Write(HttpContext context, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            checks = report.Entries.Select(entry => new { name = entry.Key, status = entry.Value.Status.ToString().ToLowerInvariant() })
        }));
    }
}
