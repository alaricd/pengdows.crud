namespace pengdows.crud.tenant;

using pengdows.crud.configuration;

public class TenantConfiguration
{
    public string Name { get; set; } = string.Empty;
    public DatabaseContextConfiguration DatabaseContextConfiguration { get; set; } = new();
}
