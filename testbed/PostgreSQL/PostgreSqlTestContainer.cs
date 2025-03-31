using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using pengdows.crud;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Builders;

namespace testbed;

public class PostgreSqlTestContainer : TestContainer
{
    private readonly TestcontainersContainer _container;
    private string? _connectionString;
    private string _password = "mysecretpassword";
    private string _username = "postgres";
    private string _database = "postgres";
    private int _port = 5432;

    public PostgreSqlTestContainer()
    {
        _container = new TestcontainersBuilder<TestcontainersContainer>()
            .WithImage("postgres:latest")
            .WithEnvironment("POSTGRES_PASSWORD", _password)
            .WithEnvironment("POSTGRES_USER", _username)
            .WithEnvironment("POSTGRES_DB", _database)
            .WithPortBinding(_port, true)
            .Build();
        _container.StartAsync().Wait();
    }

    public override async Task StartAsync()
    {
        //await _container.StartAsync();
        var hostPort = _container.GetMappedPublicPort(_port);
        _connectionString =
            $@"Host=localhost;Port={hostPort};Username={_username};Password={_password};Database={_database};Pooling=true;Minimum Pool Size=1;Maximum Pool Size=20;Timeout=15;CommandTimeout=30;";
        await WaitForDbToStart(NpgsqlFactory.Instance, _connectionString,_container);
    }

    public override async Task<IDatabaseContext> GetDatabaseContextAsync(IServiceProvider services)
    {
        if (_connectionString is null)
            throw new InvalidOperationException("Container not started yet.");

        return new DatabaseContext(_connectionString, NpgsqlFactory.Instance,
            services.GetRequiredService<ITypeMapRegistry>());
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}