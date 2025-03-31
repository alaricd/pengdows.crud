using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using pengdows.crud;

namespace testbed;

public class OracleTestContainer : TestContainer
{
    private readonly TestcontainersContainer _container;
    private string? _connectionString;
    private string _password = "mysecurepassword";
    private string _username = "system";
    private string _database = "XEPDB1";
    private int _port = 1521;

    public OracleTestContainer()
    {
        _container = new TestcontainersBuilder<TestcontainersContainer>()
            .WithImage("oracle/database:18.4.0-xe")
            .WithEnvironment("ORACLE_PWD", _password)
            .WithPortBinding(_port, true)
            .WithExposedPort(_port)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilPortIsAvailable(_port)) // Oracle listens on 1521
            .Build();
    }

    public override async Task StartAsync()
    {
        await _container.StartAsync();
        var hostPort = _container.GetMappedPublicPort(_port);
        _connectionString = // $@"User Id={_username};Password={_password};Data Source=localhost:{hostPort}/{_database};";
        $@"User Id={_username};Password={_password};Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT={hostPort}))(CONNECT_DATA=(SERVICE_NAME={_database})));";

        await base.WaitForDbToStart(OracleClientFactory.Instance, _connectionString,_container,  120);
    }

    public override async Task<IDatabaseContext> GetDatabaseContextAsync(IServiceProvider services)
    {
        if (_connectionString is null)
            throw new InvalidOperationException("Container not started yet.");

        return new DatabaseContext(_connectionString, OracleClientFactory.Instance,
            services.GetRequiredService<ITypeMapRegistry>());
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}