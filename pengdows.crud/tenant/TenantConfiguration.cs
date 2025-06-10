using pengdows.crud.configuration;

namespace pengdows.crud.tenant;

public class TenantConfiguration:ITenantConfiguration
{
    public string Name { get; set; } = string.Empty;
    public DatabaseContextConfiguration DatabaseContextConfiguration { get; set; } = new();
}