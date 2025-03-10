namespace pengdows.crud;

public interface ITypeMapRegistry
{
    TableInfo GetTableInfo<T>();
}