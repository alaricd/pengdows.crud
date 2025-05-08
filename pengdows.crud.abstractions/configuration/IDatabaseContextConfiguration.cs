using pengdows.crud.enums;

namespace pengdows.crud.configuration;

public interface IDatabaseContextConfiguration
{
    string ConnectionString { get; set; }
    string ProviderName { get; set; }
    DbMode DbMode { get; set; }
    ReadWriteMode ReadWriteMode { get; set; }
}