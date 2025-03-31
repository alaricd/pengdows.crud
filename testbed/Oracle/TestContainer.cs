using System.Data.Common;
using DotNet.Testcontainers.Containers;
using Oracle.ManagedDataAccess.Client;
using pengdows.crud;

namespace testbed;

public abstract class TestContainer : ITestContainer
{
    public async Task RunTestWithContainerAsync<TTestProvider>(
        IServiceProvider services,
        Func<IDatabaseContext, IServiceProvider, TTestProvider> testProviderFactory)
        where TTestProvider : TestProvider
    {
        await this.StartAsync();
        var dbContext = await this.GetDatabaseContextAsync(services);
        var testProvider = testProviderFactory(dbContext, services);


        Console.WriteLine($"Running test with container: {this.GetType().Name}");
        testProvider.RunTest().GetAwaiter().GetResult();
    }


    public abstract Task StartAsync();

    public abstract Task<IDatabaseContext> GetDatabaseContextAsync(IServiceProvider services);

    protected async Task WaitForDbToStart(DbProviderFactory instance, string connectionString,
        TestcontainersContainer container, 
        int numberOfSecondsToWait = 60)
    {
        var csb = instance.CreateConnectionStringBuilder();
        csb.ConnectionString = connectionString;
        var connection = instance.CreateConnection();
        connection.ConnectionString = csb.ConnectionString;
        var millisecondsToWait = numberOfSecondsToWait * 1000;
        for (
            var dt = DateTime.Now;
            (DateTime.Now - dt).TotalMilliseconds < millisecondsToWait;
        )
        {
            try
            {
                await connection.OpenAsync();

                await connection.CloseAsync();
                return;
            }
            catch (OracleException ex) 
            {
               await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                await Task.Delay(1000); // 1 second delay between retries
            }
        }

        throw new Exception("Could not connect to database.");
    }
}