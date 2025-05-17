using System.Data;
using pengdows.crud.enums;

namespace pengdows.crud.isolation;

public sealed class IsolationResolver : IIsolationResolver
{
    private readonly SupportedDatabase _product;
    private readonly bool _rcsi;
    private readonly HashSet<IsolationLevel> _supportedLevels;
    private readonly Dictionary<IsolationProfile, IsolationLevel> _profileMap;

    public IsolationResolver(SupportedDatabase product, bool readCommittedSnapshotEnabled)
    {
        _product = product;
        _rcsi = readCommittedSnapshotEnabled;
        _supportedLevels = BuildSupportedIsolationLevels(product, _rcsi);
        _profileMap = BuildProfileMapping(product);
    }

    public IsolationLevel Resolve(IsolationProfile profile)
    {
        if (!_profileMap.TryGetValue(profile, out var level))
        {
            throw new NotSupportedException($"Profile {profile} not supported for {_product}");
        }

        Validate(level);
        return level;
    }

    public void Validate(IsolationLevel level)
    {
        if (!_supportedLevels.Contains(level))
        {
            throw new InvalidOperationException($"Isolation level {level} not supported by {_product} (RCSI: {_rcsi})");
        }
    }

    public IReadOnlySet<IsolationLevel> GetSupportedLevels() => _supportedLevels;

    private static HashSet<IsolationLevel> BuildSupportedIsolationLevels(SupportedDatabase db, bool rcsi)
    {
        var map = new Dictionary<SupportedDatabase, HashSet<IsolationLevel>>
        {
            [SupportedDatabase.SqlServer] = new HashSet<IsolationLevel>()
            {
                IsolationLevel.ReadUncommitted,
                rcsi ? IsolationLevel.ReadCommitted : default, // only allow if RCSI is ON
                IsolationLevel.RepeatableRead,
                IsolationLevel.Serializable,
                IsolationLevel.Snapshot
            }.Where(l => l != default).ToHashSet(),

            // Add other DBs here as before...
        };

        return map.TryGetValue(db, out var set) ? set : throw new NotSupportedException($"Unsupported DB: {db}");
    }

    private static Dictionary<IsolationProfile, IsolationLevel> BuildProfileMapping(SupportedDatabase db)
    {
        return db switch
        {
            SupportedDatabase.SqlServer => new()
            {
                [IsolationProfile.SafeNonBlockingReads] = IsolationLevel.ReadCommitted, // assumes RCSI is validated
                [IsolationProfile.StrictConsistency] = IsolationLevel.Serializable,
                [IsolationProfile.FastWithRisks] = IsolationLevel.ReadUncommitted
            },

            SupportedDatabase.CockroachDb => new()
            {
                [IsolationProfile.SafeNonBlockingReads] = IsolationLevel.Serializable,
                [IsolationProfile.StrictConsistency] = IsolationLevel.Serializable
            },

            // ... other DBs here ...

            _ => throw new NotSupportedException($"Isolation profile mapping not defined for DB: {db}")
        };
    }
}