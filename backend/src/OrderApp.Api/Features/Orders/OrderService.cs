using Microsoft.EntityFrameworkCore;
using OrderApp.Api.Common;
using OrderApp.Api.Data;
using OrderApp.Api.Features.Products;

namespace OrderApp.Api.Features.Orders;

/// <summary>
/// Sipariş iş kuralları. Controller sadece isteği buraya iletir.
/// </summary>
public class OrderService
{
    private readonly AppDbContext _db;
    private readonly ProductCache _productCache;
    private readonly ILogger<OrderService> _logger;

    public OrderService(AppDbContext db, ProductCache productCache, ILogger<OrderService> logger)
    {
        _db = db;
        _productCache = productCache;
        _logger = logger;
    }

    /// <summary>
    /// Siparişi doğrular, stokları düşer ve siparişi kaydeder.
    /// Sipariş kaydı ile stok düşümü tek transaction içinde yapılır: herhangi bir
    /// üründe stok yetmiyorsa hiçbir stok değişmez ve sipariş oluşmaz.
    /// </summary>
    public async Task<OrderDetailResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var items = NormalizeItems(request.Items);
        var pricingType = ParsePricingType(request.PricingType);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var productIds = items.Select(i => i.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        EnsureAllProductsExist(productIds, products);

        var order = new Order
        {
            CustomerName = request.CustomerName.Trim(),
            PricingType = pricingType,
            CreatedAtUtc = DateTime.UtcNow
        };

        var stockProblems = new List<string>();

        foreach (var item in items)
        {
            var product = products[item.ProductId];

            if (!product.TryReduceStock(item.Quantity))
            {
                stockProblems.Add(
                    $"{product.Name} ({product.StockCode}) icin yeterli stok yok. Talep: {item.Quantity}, mevcut: {product.StockQuantity}.");
                continue;
            }

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductStockCode = product.StockCode,
                ProductName = product.Name,
                // Fiyat sipariş anında kopyalanır; ürün fiyatı sonradan değişse
                // bile bu siparişin tutarı değişmez.
                UnitPrice = product.Price,
                Quantity = item.Quantity,
                LineTotal = product.Price * item.Quantity
            });
        }

        if (stockProblems.Count > 0)
        {
            // Transaction commit edilmez; takip edilen stok değişiklikleri de
            // SaveChanges çağrılmadığı için veritabanına yansımaz.
            throw new BusinessRuleException("Yetersiz stok nedeniyle siparis olusturulamadi.", stockProblems);
        }

        order.TotalAmount = order.Items.Sum(i => i.LineTotal);

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Stoklar değişti -> ürün cache'i geçersiz.
        _productCache.InvalidateAll();
        _logger.LogInformation("Siparis {OrderId} olusturuldu. Tutar: {Total}", order.Id, order.TotalAmount);

        return ToDetailResponse(order);
    }

    public async Task<IReadOnlyList<OrderSummaryResponse>> GetOrdersAsync(CancellationToken cancellationToken)
    {
        return await _db.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAtUtc)
            .ThenByDescending(o => o.Id)
            .Select(o => new OrderSummaryResponse(
                o.Id,
                o.CustomerName,
                o.PricingType.ToString(),
                o.CreatedAtUtc,
                o.TotalAmount,
                o.Items.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<OrderDetailResponse> GetOrderByIdAsync(int id, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
        {
            throw new NotFoundException($"{id} numarali siparis bulunamadi.");
        }

        return ToDetailResponse(order);
    }

    /// <summary>
    /// İstek satırlarını doğrular ve aynı ürünün birden fazla satırda gelmesi
    /// durumunda miktarları tek satırda toplar.
    /// </summary>
    private static List<CreateOrderItemRequest> NormalizeItems(IReadOnlyCollection<CreateOrderItemRequest> requestItems)
    {
        if (requestItems.Count == 0)
        {
            throw new BusinessRuleException("Siparis en az bir urun icermelidir.");
        }

        if (requestItems.Any(i => i.Quantity <= 0))
        {
            throw new BusinessRuleException("Her urun icin siparis miktari sifirdan buyuk olmalidir.");
        }

        return requestItems
            .GroupBy(i => i.ProductId)
            .Select(g => new CreateOrderItemRequest { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToList();
    }

    private static PricingType ParsePricingType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PricingType.Standard;
        }

        if (!Enum.TryParse<PricingType>(value, ignoreCase: true, out var parsed))
        {
            throw new BusinessRuleException(
                $"Gecersiz fiyatlandirma tipi: '{value}'. Gecerli degerler: Standard, Bulk.");
        }

        return parsed;
    }

    private static void EnsureAllProductsExist(IEnumerable<int> requestedIds, IReadOnlyDictionary<int, Product> found)
    {
        var missing = requestedIds.Where(id => !found.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            throw new BusinessRuleException(
                "Siparisteki bazi urunler bulunamadi.",
                missing.Select(id => $"{id} numarali urun bulunamadi.").ToList());
        }
    }

    private static OrderDetailResponse ToDetailResponse(Order order) => new(
        order.Id,
        order.CustomerName,
        order.PricingType.ToString(),
        order.CreatedAtUtc,
        order.TotalAmount,
        order.Items
            .Select(i => new OrderItemResponse(i.ProductId, i.ProductStockCode, i.ProductName, i.UnitPrice, i.Quantity, i.LineTotal))
            .ToList());
}
