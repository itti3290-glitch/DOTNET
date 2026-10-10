using Npgsql;

namespace Library.Data;

/// <summary>
/// Managed hosts - Render among them - hand out a database URL in URI form
/// (<c>postgres://user:password@host:port/database</c>). Npgsql only understands keyword
/// connection strings, so a URI has to be translated before it reaches the provider. Anything
/// that is not a URI is already in keyword form and is passed through untouched, which keeps the
/// local appsettings value working.
/// </summary>
public static class PostgresConnectionString
{
    public static string Normalize(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return connectionString;

        var uri = new Uri(connectionString);
        var credentials = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/'),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : null,

            // Managed Postgres is reached over the network and expects TLS. Require encrypts
            // without demanding a verifiable certificate chain, which is what these providers
            // issue; a local server stays unencrypted so plain development keeps working.
            SslMode = IsLoopback(uri.Host) ? SslMode.Disable : SslMode.Require
        };

        // An explicit query parameter is the caller's intent, so it overrides the defaults above
        // (e.g. "?sslmode=disable" against a server without TLS).
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
                builder[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
        }

        return builder.ConnectionString;
    }

    private static bool IsLoopback(string host) =>
        host is "localhost" or "127.0.0.1" or "::1";
}
