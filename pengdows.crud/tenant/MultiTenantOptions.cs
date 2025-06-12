namespace pengdows.crud.tenant;

public class MultiTenantOptions
{
    public List<TenantConfiguration> Tenants { get; set; } = new();
}