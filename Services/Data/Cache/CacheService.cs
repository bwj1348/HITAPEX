using System.Collections.Concurrent;

namespace HITAPEX.Services.Data.Cache;

public class CacheService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly TimeSpan _defaultTtl;
    private readonly Timer _cleanupTimer;

    public CacheService(TimeSpan? defaultTtl = null, int cleanupIntervalMs = 60000)
    {
        _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(5);
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, cleanupIntervalMs, cleanupIntervalMs);
    }

    public void Set<T>(string key, T value, TimeSpan? ttl = null)
    {
        _cache[key] = new CacheEntry(value, DateTime.UtcNow.Add(ttl ?? _defaultTtl));
    }

    public bool TryGet<T>(string key, out T? value)
    {
        if (_cache.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTime.UtcNow)
        {
            value = (T)entry.Value;
            return true;
        }

        _cache.TryRemove(key, out _);
        value = default;
        return false;
    }

    public bool Contains(string key) => TryGet<object>(key, out _);

    public void Remove(string key) => _cache.TryRemove(key, out _);

    public void Clear() => _cache.Clear();

    private void CleanupExpiredEntries(object? state)
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _cache)
        {
            if (kvp.Value.ExpiresAt <= now)
                _cache.TryRemove(kvp.Key, out _);
        }
    }

    private class CacheEntry
    {
        public object Value { get; }
        public DateTime ExpiresAt { get; }

        public CacheEntry(object value, DateTime expiresAt)
        {
            Value = value;
            ExpiresAt = expiresAt;
        }
    }
}
