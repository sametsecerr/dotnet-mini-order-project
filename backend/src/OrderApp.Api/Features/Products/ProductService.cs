using Microsoft.EntityFrameworkCore;
using OrderApp.Api.Common;
using OrderApp.Api.Data;

namespace OrderApp.Api.Features.Products;

/// <summary>
/// Ürün okuma işlemleri. Tüm okumalar cache üzerinden (read-through) yapılır.
/// </summary>
public class ProductService
{
    private readonly AppDbContext _db;
    private readonly ProductCache _cache;

    public ProductService(AppDbContext db, ProductCache cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <summary>Ürünleri listeler; <paramref name="search"/> verilirse isim veya stok koduna göre filtreler.</summary>
    public Task<IReadOnlyList<ProductResponse>> GetProductsAsync(string? search, CancellationToken cancellationToken)
    {
        var term = search?.Trim() ?? string.Empty;
        var cacheKey = term.Length == 0 ? ProductCache.AllProductsKey : ProductCache.SearchKey(term);

        return _cache.GetOrCreateAsync(cacheKey, () => QueryProductsAsync(term, cancellationToken));
    }

    public async Task<ProductResponse> GetProductByIdAsync(int id, CancellationToken cancellationToken)
    {
        var product = await _cache.GetOrCreateAsync(
            ProductCache.DetailKey(id),
            async () => await _db.Products
                .AsNoTracking()
                .Where(p => p.Id == id)
                .Select(p => new ProductResponse(p.Id, p.StockCode, p.Name, p.Price, p.StockQuantity))
                .FirstOrDefaultAsync(cancellationToken));

        return product ?? throw new NotFoundException($"{id} numarali urun bulunamadi.");
    }

    private async Task<IReadOnlyList<ProductResponse>> QueryProductsAsync(string term, CancellationToken cancellationToken)
    {
        var query = _db.Products.AsNoTracking();

        if (term.Length > 0)
        {
            // SQLite'ta LIKE varsayılan olarak case-insensitive çalışır (ASCII).
            var pattern = $"%{term}%";
            query = query.Where(p => EF.Functions.Like(p.Name, pattern) || EF.Functions.Like(p.StockCode, pattern));
        }

        return await query
            .OrderBy(p => p.Name)
            .Select(p => new ProductResponse(p.Id, p.StockCode, p.Name, p.Price, p.StockQuantity))
            .ToListAsync(cancellationToken);
    }
}
