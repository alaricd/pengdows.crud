using pengdows.crud.configuration;

namespace pengdows.crud.tenant;

public interface ITenantConnectionResolver
{
    IDatabaseContextConfiguration GetDatabaseContextConfiguration(string tenant);
}