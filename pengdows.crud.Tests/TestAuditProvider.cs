#region

using System;

#endregion

namespace pengdows.crud.Tests;

public class TestAuditProvider : IAuditContextProvider<string>
{
    public string GetCurrentUserIdentifier()
    {
        return "test-user";
    }

    public DateTime GetUtcNow()
    {
        return DateTime.UtcNow;
    }
}