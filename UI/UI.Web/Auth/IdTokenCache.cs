using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace UI.Web.Auth;

public sealed record CachedIdToken(string IdToken, DateTimeOffset ExpiresAt);

/// <summary>
/// Process-wide cache of Firebase ID tokens per user, so the hourly refresh happens once per user instead of
/// once per request. ID tokens never go into the cookie.
/// </summary>
public interface IIdTokenCache
{
    /// <summary>The cached token, or null when it is missing or about to expire.</summary>
    CachedIdToken? Get(string uid);

    void Set(string uid, CachedIdToken token);

    void Remove(string uid);

    /// <summary>Returns the cached token or runs <paramref name="refresh"/> exactly once, even under concurrent callers.</summary>
    Task<CachedIdToken> GetOrRefreshAsync(string uid, Func<CancellationToken, Task<CachedIdToken>> refresh, CancellationToken cancellationToken);
}

public sealed class IdTokenCache(IMemoryCache cache, TimeProvider clock) : IIdTokenCache
{
    private static readonly TimeSpan Skew = TimeSpan.FromSeconds(60);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    private static string Key(string uid) => $"firebase:idtoken:{uid}";

    public CachedIdToken? Get(string uid) =>
        cache.TryGetValue(Key(uid), out CachedIdToken? token) && token is not null && token.ExpiresAt - clock.GetUtcNow() > Skew
            ? token
            : null;

    public void Set(string uid, CachedIdToken token) =>
        cache.Set(Key(uid), token, new MemoryCacheEntryOptions { AbsoluteExpiration = token.ExpiresAt - Skew });

    public void Remove(string uid) => cache.Remove(Key(uid));

    public async Task<CachedIdToken> GetOrRefreshAsync(string uid, Func<CancellationToken, Task<CachedIdToken>> refresh, CancellationToken cancellationToken)
    {
        if (Get(uid) is { } cached)
        {
            return cached;
        }

        var gate = _locks.GetOrAdd(uid, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (Get(uid) is { } refreshedMeanwhile)
            {
                return refreshedMeanwhile;
            }

            var fresh = await refresh(cancellationToken);
            Set(uid, fresh);
            return fresh;
        }
        finally
        {
            gate.Release();
        }
    }
}
