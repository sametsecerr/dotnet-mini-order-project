using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace OrderApp.Api.Features.Products;

public sealed class ProductCache
{
    public const string AllProductsKey = "products:all";

    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(1);

    private readonly IMemoryCache _cache;
    private readonly ILogger<ProductCache> _logger;
    private readonly object _resetLock = new();
    private CancellationTokenSource _resetTokenSource = new();

    public ProductCache(IMemoryCache cache, ILogger<ProductCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public static string SearchKey(string normalizedTerm) => $"products:search:{normalizedTerm}";

    public static string DetailKey(int productId) => $"products:id:{productId}";

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return cached;
        }

        // Token, factory beklenmeden once alinmali: veri okunurken gelen bir
        // InvalidateAll'i kacirmayalim, yoksa eski veri TTL boyunca cache'te kalir.
        var resetToken = CurrentResetToken();

        var value = await factory();
        if (value is null)
        {
            return value;
        }

        var options = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl };
        options.AddExpirationToken(new CancellationChangeToken(resetToken));
        _cache.Set(key, value, options);

        return value;
    }

    public void InvalidateAll()
    {
        CancellationTokenSource previous;
        lock (_resetLock)
        {
            previous = _resetTokenSource;
            _resetTokenSource = new CancellationTokenSource();
        }

        previous.Cancel();
        previous.Dispose();
        _logger.LogInformation("Urun cache'i temizlendi.");
    }

    private CancellationToken CurrentResetToken()
    {
        lock (_resetLock)
        {
            return _resetTokenSource.Token;
        }
    }
}
