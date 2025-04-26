using pengdows.crud.enums;

namespace pengdows.crud.tenant;

public class TenantInformation : ITenantInformation
{
    public TenantInformation(SupportedDatabase databaseType, string connectionString)
    {
        DatabaseType = databaseType;
        ConnectionString = connectionString;
    }

    public SupportedDatabase DatabaseType { get; }
    public string ConnectionString { get; }
}