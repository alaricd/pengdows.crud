using pengdows.crud;

namespace testbed;

public class StringAuditContextProvider
    : AuditContextProvider<string>
{
    public override string GetCurrentUserIdentifier()
    {
        return "testuser";
    }
}