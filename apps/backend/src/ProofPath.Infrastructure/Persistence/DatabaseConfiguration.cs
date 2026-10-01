using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ProofPath.Infrastructure.Persistence;

public static class DatabaseConfiguration
{
    public static string Resolve(IConfiguration configuration)
    {
        var direct = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(direct)) return direct;

        var host = Required(configuration, "Database:Host");
        var database = Required(configuration, "Database:Name");
        var username = Required(configuration, "Database:Username");
        var password = Required(configuration, "Database:Password");
        var sslMode = Enum.TryParse<SslMode>(configuration["Database:SslMode"] ?? "Require", true, out var parsed)
            ? parsed : SslMode.Require;

        return new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = configuration.GetValue("Database:Port", 5432),
            Database = database,
            Username = username,
            Password = password,
            SslMode = sslMode,
            Pooling = true,
            Timeout = 15,
            CommandTimeout = 30
        }.ConnectionString;
    }

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Configure {key} securely.");
}
