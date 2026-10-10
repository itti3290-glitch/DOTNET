using Library.Data;
using Npgsql;

namespace Library.Tests.Data;

/// <summary>
/// The deployed API gets its database URL from the platform in URI form. A mistake here surfaces
/// only as a failure to connect after a deploy, so the translation is pinned down by tests.
/// </summary>
public class PostgresConnectionStringTests
{
    [Fact]
    public void KeywordConnectionString_IsPassedThroughUnchanged()
    {
        const string keyword = "Host=localhost;Port=5432;Database=LibraryApiDb;Username=postgres;Password=postgres";

        Assert.Equal(keyword, PostgresConnectionString.Normalize(keyword));
    }

    [Theory]
    [InlineData("postgres://")]
    [InlineData("postgresql://")]
    public void DatabaseUri_IsTranslatedIntoNpgsqlKeywords(string scheme)
    {
        var result = PostgresConnectionString.Normalize($"{scheme}libuser:s3cret@dpg-abc123.oregon-postgres.render.com:5432/library_db");

        var parsed = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("dpg-abc123.oregon-postgres.render.com", parsed.Host);
        Assert.Equal(5432, parsed.Port);
        Assert.Equal("library_db", parsed.Database);
        Assert.Equal("libuser", parsed.Username);
        Assert.Equal("s3cret", parsed.Password);
    }

    [Fact]
    public void DatabaseUri_WithoutAPort_FallsBackToThePostgresDefault()
    {
        var result = PostgresConnectionString.Normalize("postgres://u:p@db.example.com/library");

        Assert.Equal(5432, new NpgsqlConnectionStringBuilder(result).Port);
    }

    [Fact]
    public void RemoteHost_RequiresTls()
    {
        var result = PostgresConnectionString.Normalize("postgres://u:p@db.example.com/library");

        Assert.Equal(SslMode.Require, new NpgsqlConnectionStringBuilder(result).SslMode);
    }

    [Fact]
    public void LoopbackHost_DoesNotRequireTls_SoLocalDevelopmentKeepsWorking()
    {
        var result = PostgresConnectionString.Normalize("postgres://u:p@localhost:5432/library");

        Assert.Equal(SslMode.Disable, new NpgsqlConnectionStringBuilder(result).SslMode);
    }

    [Fact]
    public void ExplicitQueryParameter_OverridesTheDefault()
    {
        var result = PostgresConnectionString.Normalize("postgres://u:p@db.example.com/library?sslmode=disable");

        Assert.Equal(SslMode.Disable, new NpgsqlConnectionStringBuilder(result).SslMode);
    }

    [Fact]
    public void PercentEncodedCredentials_AreDecoded()
    {
        // Generated passwords routinely contain characters that have to be escaped in a URI.
        var result = PostgresConnectionString.Normalize("postgres://my%40user:p%40ss%3Aword@db.example.com/library");

        var parsed = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("my@user", parsed.Username);
        Assert.Equal("p@ss:word", parsed.Password);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankConnectionString_IsRejected(string value)
    {
        Assert.Throws<ArgumentException>(() => PostgresConnectionString.Normalize(value));
    }
}
