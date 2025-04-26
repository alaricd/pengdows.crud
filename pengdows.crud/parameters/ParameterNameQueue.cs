using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace pengdows.crud.parameters;

using System.Collections.Concurrent;

internal sealed class ParameterNameQueue : IParameterNameQueue
{
    private const int NameLength = 5;
    private const int RefillThreshold = 50;
    private const int RefillAmount = 100;

    private static readonly char[] _charset =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_".ToCharArray();

    private readonly ConcurrentQueue<string> _queue = new();
    private readonly HashSet<string> _usedNames = new();
    private readonly object _refillLock = new();
    private readonly ILogger<IParameterNameQueue> _logger;

    public ParameterNameQueue(ILogger<IParameterNameQueue> logger = null)
    {
        _logger = logger ?? NullLogger<IParameterNameQueue>.Instance;

        RefillQueue();
    }

    public string GetNext()
    {
        EnsureQueueFilled();

        while (_queue.TryDequeue(out var name))
        {
            if (_usedNames.Add(name))
                return name;
        }

        // Shouldn't ever get here, but just in case
        return GenerateName();
    }

    private void EnsureQueueFilled()
    {
        if (_queue.Count < RefillThreshold)
        {
            lock (_refillLock)
            {
                if (_queue.Count < RefillThreshold)
                    RefillQueue();
            }
        }
    }

    private void RefillQueue()
    {
        _logger.LogInformation("Refilling queue. Queue count before: {QueueCount}, Used names: {UsedCount}",
            _queue.Count, _usedNames.Count);
        for (var i = 0;
             i < RefillAmount;
             i++)
        {
            var name = GenerateName();
            if (!_usedNames.Contains(name))
                _queue.Enqueue(name);
        }

        PurgeUsedNames();
    }

    private string GenerateName()
    {
        Span<char> buffer = stackalloc char[NameLength];
        const int firstCharMax = 52; //26*2, make sure that the first char is a character
        var anyOtherMax = _charset.Length;
        var max = firstCharMax;
        for (var i = 0; i < NameLength; i++)
        {
            var index = Random.Shared.Next(max);
            buffer[i] = _charset[index];
            max = anyOtherMax;
        }


        return new string(buffer);
    }

    private void PurgeUsedNames()
    {
        const int hardLimit = 999;

        if (_usedNames.Count > hardLimit)
        {
            _logger.LogWarning("Used names exceeded {hardLimit}. Clearing entire set.", hardLimit);
            _usedNames.Clear();
            return;
        }

        var purgeCount = Math.Min(RefillAmount, _usedNames.Count);

        // Don't purge if we are under the refill threshold
        if (purgeCount < RefillThreshold)
        {
            return;
        }

        _logger.LogInformation("Purging {PurgeCount} used names from used set.", purgeCount);

        var enumerator = _usedNames.GetEnumerator();

        var removed = 0;
        for (; purgeCount-- > 0 && enumerator.MoveNext(); removed++)
        {
            _usedNames.Remove(enumerator.Current);
        }

        _logger.LogInformation("Purge complete. Removed {Removed} names. Remaining: {Remaining}.",
            removed,
            _usedNames.Count);
    }
}