namespace pengdows.crud.tenant;

public class TenantConnectionResolver : ITenantConnectionResolver
{
    public string GetConnectionString(string name)
    {
        throw new NotImplementedException();
    }

    public ITenantInformation GetTenantInfo(string tenant)
    {
        throw new NotImplementedException();
    }
}