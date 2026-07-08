using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace HabitTracker.IntegrationTests;

/// <summary>One Postgres container per test collection; each factory points at it.</summary>
public class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public Task InitializeAsync() => Container.StartAsync();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<PostgresFixture>;

public class TestWebAppFactory(PostgresFixture postgres) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", postgres.Container.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-0123456789abcdef");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        // Generous default so unrelated tests never trip the limiter; the rate-limit
        // test builds its own factory with a small limit.
        builder.UseSetting("RateLimiting:AuthPermitLimit", "10000");
    }
}
