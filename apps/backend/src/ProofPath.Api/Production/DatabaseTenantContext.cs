using System.Data.Common;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProofPath.Infrastructure.Persistence;

namespace ProofPath.Api.Production;

public sealed class DatabaseTenantContext(IHttpContextAccessor httpContext) : DbConnectionInterceptor
{
    private const string UserSetting = "proofpath.user_id";

    private string UserId =>
        httpContext.HttpContext?.User is { Identity.IsAuthenticated: true } user
            ? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? ""
            : "";

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"select set_config('{UserSetting}', @user_id, false)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "user_id";
        parameter.Value = UserId;
        command.Parameters.Add(parameter);
        return command;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Registration creates the identity and its private profile in the same transaction.
    public static Task UseNewIdentityAsync(ProofPathDbContext database, string userId, CancellationToken ct) =>
        database.Database.ExecuteSqlInterpolatedAsync($"select set_config({UserSetting}, {userId}, true)", ct);
}
