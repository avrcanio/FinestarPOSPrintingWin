using Microsoft.Extensions.Caching.Memory;

namespace MozzartPrintReceiver.Services;

public sealed class MemoryJobDeduplicator : IJobDeduplicator
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private readonly IMemoryCache _cache;

    public MemoryJobDeduplicator(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool IsDuplicate(string jobId)
    {
        return _cache.TryGetValue(GetKey(jobId), out _);
    }

    public void Remember(string jobId)
    {
        _cache.Set(GetKey(jobId), true, Ttl);
    }

    private static string GetKey(string jobId) => $"printed:{jobId}";
}
