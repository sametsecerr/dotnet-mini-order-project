using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace OrderApp.Api.Features.Products;

/// <summary>
/// Ürün okumalarının IMemoryCache üzerinden yönetildiği tek nokta.
///
/// Cache key yapısı:
///   products:all                -> tüm ürün listesi
///   products:search:{terim}     -> aramaya göre filtrelenmiş liste
///   products:id:{id}            -> tek ürün detayı
///
/// Invalidation: tüm ürün girdileri ortak bir <see cref="CancellationChangeToken"/>
/// ile işaretlenir. Stok/fiyat değiştiğinde token iptal edilir ve ürünle ilgili
/// bütün key'ler tek hamlede düşer. Böylece "hangi search key'i etkilendi?"
/// sorusunu çözmek zorunda kalmayız.
/// </summary>
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

    public static string SearchKey(string term) => $"products:search:{term.ToLowerInvariant()}";

    public static string DetailKey(int productId) => $"products:id:{productId}";

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory();

        // Bulunamayan kaydı cache'lemiyoruz; aksi halde 404 sonuçları da TTL boyunca sabitlenirdi.
        if (value is null)
        {
            return value;
        }

        // Girdiyi oluştururken güncel reset token'ını bağla.
        var entryOptions = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl };
        entryOptions.AddExpirationToken(new CancellationChangeToken(CurrentResetToken()));
        _cache.Set(key, value, entryOptions);

        return value;
    }

    /// <summary>Ürün verisi değiştiğinde (ör. sipariş sonrası stok düşümü) çağrılır.</summary>
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
