namespace pengdows.crud.tenant;

public interface ITenantConnectionResolver
{
    string GetConnectionString(string name);
    ITenantInformation GetTenantInfo(string tenant);
}