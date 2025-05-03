using pengdows.crud.enums;

namespace pengdows.crud;

public class DatabaseContextConfiguration
{
    public string connectionString { get; set; }
    public string factory { get; set; }
    public ITypeMapRegistry? typeMapRegistry { get; set; }
    public DbMode mode { get; set; } = DbMode.Standard;
    public ReadWriteMode readWriteMode { get; set; } = ReadWriteMode.ReadWrite;
}