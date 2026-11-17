using System.Globalization;
using System.Security.Claims;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Api.Endpoints;

public static class HistoryEndpoints
{
    public static IEndpointRouteBuilder MapHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/profile/history").RequireAuthorization();
        group.MapGet("/evaluations", (int? limit, string? cursor, ClaimsPrincipal user, CandidateHistory history, CancellationToken ct) =>
            ReadPageAsync(limit, cursor,
                (before, count) => history.EvaluationsAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, before, count, ct),
                item => new HistoryPosition(item.CreatedAt, item.Id)));
        group.MapGet("/matches", (int? limit, string? cursor, ClaimsPrincipal user, CandidateHistory history, CancellationToken ct) =>
            ReadPageAsync(limit, cursor,
                (before, count) => history.MatchesAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, before, count, ct),
                item => new HistoryPosition(item.CreatedAt, item.Id)));
        return endpoints;
    }

    private static async Task<IResult> ReadPageAsync<T>(int? limit, string? cursor,
        Func<HistoryPosition?, int, Task<T[]>> read, Func<T, HistoryPosition> getPosition)
    {
        var pageSize = limit ?? 20;
        if (pageSize is < 1 or > 100)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["limit"] = ["Use a page size from 1 to 100."] });
        HistoryPosition? before = null;
        if (cursor is not null)
        {
            if (cursor.Length > 53 ||
                cursor.Split('.') is not [var ticks, var id] ||
                !long.TryParse(ticks, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp) ||
                timestamp < 1 || timestamp >= DateTime.MaxValue.Ticks ||
                !Guid.TryParseExact(id, "N", out var recordId))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["cursor"] = ["Use the cursor returned by the history endpoint."] });
            before = new HistoryPosition(new DateTime(timestamp, DateTimeKind.Utc), recordId);
        }

        var records = await read(before, pageSize + 1);
        if (records.Length <= pageSize)
            return Results.Ok(new { items = records, nextCursor = (string?)null });
        var last = getPosition(records[pageSize - 1]);
        var nextCursor = FormattableString.Invariant($"{last.CreatedAt.Ticks}.{last.Id:N}");
        return Results.Ok(new { items = records[..pageSize], nextCursor });
    }
}
