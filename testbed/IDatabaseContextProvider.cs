using pengdows.crud;

namespace testbed;

public interface IDatabaseContextProvider
{
    IDatabaseContext Get(string key);
}