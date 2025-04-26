using pengdows.crud.enums;

namespace pengdows.crud.tenant;

public interface ITenantInformation
{
    SupportedDatabase DatabaseType { get; }
    string ConnectionString { get; }
}