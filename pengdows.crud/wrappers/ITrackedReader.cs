using System.Data;

namespace pengdows.crud.wrappers;

public interface ITrackedReader:IDataReader, IAsyncDisposable
{
}