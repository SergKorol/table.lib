using System.Data.Common;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using JetBrains.Annotations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace table.lib.tests;

[UsedImplicitly]
public sealed class DbFixture : IAsyncLifetime
{
    private readonly INetwork _network = new NetworkBuilder().Build();

    private readonly PostgreSqlContainer _postgreSqlContainer;

    private readonly IContainer _flywayContainer;

    public DbFixture()
    {
        _postgreSqlContainer = new PostgreSqlBuilder()
            .WithImage("postgres:latest")
            .WithNetwork(_network)
            .WithNetworkAliases(nameof(_postgreSqlContainer))
            .Build();

        _flywayContainer = new ContainerBuilder()
            .WithImage("flyway/flyway:latest")
            .WithResourceMapping("migrate/", "/flyway/sql/")
            .WithCommand("-url=jdbc:postgresql://" + nameof(_postgreSqlContainer) + "/")
            .WithCommand("-user=" + PostgreSqlBuilder.DefaultUsername)
            .WithCommand("-password=" + PostgreSqlBuilder.DefaultPassword)
            .WithCommand("-connectRetries=3")
            .WithCommand("migrate")
            .WithNetwork(_network)
            .DependsOn(_postgreSqlContainer)
            .WithWaitStrategy(Wait.ForUnixContainer().AddCustomWaitStrategy(new MigrationCompleted()))
            .Build();
    }

    public DbConnection DbConnection => new NpgsqlConnection(_postgreSqlContainer.GetConnectionString());

    public Task InitializeAsync()
    {
        return _flywayContainer.StartAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private sealed class MigrationCompleted : IWaitUntil
    {
        public Task<bool> UntilAsync(IContainer container)
        {
            return Task.FromResult(TestcontainersStates.Exited.Equals(container.State));
        }
    }
}