using System;
using System.Data;
using System.Linq;
using pengdows.crud.enums;
using pengdows.crud.isolation;
using Xunit;

namespace pengdows.crud.Tests.isolation;

public class IsolationResolverTests
{
    [Fact]
    public void Constructor_UnsupportedDatabase_Throws()
    {
        Assert.Throws<NotSupportedException>(() => new IsolationResolver(SupportedDatabase.MySql, true));
    }

    [Fact]
    public void GetSupportedLevels_SqlServer_RcsiTrue()
    {
        var resolver = new IsolationResolver(SupportedDatabase.SqlServer, true);

        var levels = resolver.GetSupportedLevels().OrderBy(l => l).ToArray();
        var expected = new[]
        {
            IsolationLevel.ReadUncommitted,
            IsolationLevel.ReadCommitted,
            IsolationLevel.RepeatableRead,
            IsolationLevel.Serializable,
            IsolationLevel.Snapshot
        };

        Assert.Equal(expected, levels);
    }

    [Fact]
    public void Resolve_SqlServer_RcsiFalse()
    {
        var resolver = new IsolationResolver(SupportedDatabase.SqlServer, false);

        var level = resolver.Resolve(IsolationProfile.SafeNonBlockingReads);
        Assert.Equal(IsolationLevel.Snapshot, level);
        Assert.Throws<InvalidOperationException>(() => resolver.Validate(IsolationLevel.ReadCommitted));
    }

    [Fact]
    public void Resolve_PostgreSql_NoRcsi_ThrowsForSafeNonBlockingReads()
    {
        var resolver = new IsolationResolver(SupportedDatabase.PostgreSql, false);

        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(IsolationProfile.SafeNonBlockingReads));
        Assert.Equal(IsolationLevel.Serializable, resolver.Resolve(IsolationProfile.StrictConsistency));
    }

    [Fact]
    public void Resolve_CockroachDb_UnsupportedProfile()
    {
        var resolver = new IsolationResolver(SupportedDatabase.CockroachDb, true);

        Assert.Equal(IsolationLevel.Serializable, resolver.Resolve(IsolationProfile.SafeNonBlockingReads));
        Assert.Throws<NotSupportedException>(() => resolver.Resolve(IsolationProfile.FastWithRisks));
        Assert.Throws<InvalidOperationException>(() => resolver.Validate(IsolationLevel.ReadCommitted));
    }
}