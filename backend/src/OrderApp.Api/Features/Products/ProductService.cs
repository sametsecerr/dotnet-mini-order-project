using Microsoft.EntityFrameworkCore;
using OrderApp.Api.Common;
using OrderApp.Api.Data;

namespace OrderApp.Api.Features.Products;

public class ProductService
{
    private const string LikeEscapeCharacter = "\\";

    private readonly AppDbContext _db;
    private readonly ProductCache _cache;

    public ProductService(AppDbContext db, ProductCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public Task<IReadOnlyList<ProductResponse>> GetProductsAsync(string? search, CancellationToken cancellationToken)
    {
        // Terim hem cache key'i hem de sorgu icin ayni sekilde normalize edilir;
        // aksi halde ayni key altinda farkli sonuc kumeleri cache'lenebilir.
        var term = (search ?? string.Empty).Trim().ToLowerInvariant();
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
            var pattern = $"%{EscapeLikeWildcards(term)}%";
            query = query.Where(p =>
                EF.Functions.Like(p.Name.ToLower(), pattern, LikeEscapeCharacter) ||
                EF.Functions.Like(p.StockCode.ToLower(), pattern, LikeEscapeCharacter));
        }

        return await query
            .OrderBy(p => p.Name)
            .Select(p => new ProductResponse(p.Id, p.StockCode, p.Name, p.Price, p.StockQuantity))
            .ToListAsync(cancellationToken);
    }

    private static string EscapeLikeWildcards(string term) => term
        .Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter)
        .Replace("%", LikeEscapeCharacter + "%")
        .Replace("_", LikeEscapeCharacter + "_");
}
