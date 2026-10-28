using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;

namespace Diwakar.Cms.Api.Authentication;

public sealed class AuthThrottle(TimeProvider clock) : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 10000 });
    private readonly object sync = new();
    private sealed record Counter(int Count, DateTimeOffset Until);

    public static string Key(string category, string identity) => category + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));

    public bool Take(string key, int limit)
    {
        lock (sync)
        {
            if (!cache.TryGetValue<Counter>(key, out var value) || value is null || value.Until <= clock.GetUtcNow())
                value = new Counter(0, clock.GetUtcNow().AddMinutes(1));
            if (value.Count >= limit) return false;
            cache.Set(key, value with { Count = value.Count + 1 }, new MemoryCacheEntryOptions { AbsoluteExpiration = value.Until, Size = 1 });
            return true;
        }
    }

    public void Clear(string key) { lock (sync) cache.Remove(key); }
    public void Dispose() => cache.Dispose();
}
