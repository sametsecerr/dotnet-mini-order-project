using Microsoft.EntityFrameworkCore;
using OrderApp.Api.Common;
using OrderApp.Api.Data;
using OrderApp.Api.Features.Products;

namespace OrderApp.Api.Features.Orders;

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

    public async Task<OrderDetailResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var items = ValidateAndMergeItems(request.Items);
        var pricingType = ParsePricingType(request.PricingType);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var productIds = items.Select(i => i.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var missingIds = productIds.Where(id => !products.ContainsKey(id)).ToList();
        if (missingIds.Count > 0)
        {
            throw new BusinessRuleException(
                "Siparisteki bazi urunler bulunamadi.",
                missingIds.Select(id => $"{id} numarali urun bulunamadi.").ToList());
        }

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
                UnitPrice = product.Price,
                Quantity = item.Quantity,
                LineTotal = product.Price * item.Quantity
            });
        }

        if (stockProblems.Count > 0)
        {
            throw new BusinessRuleException("Yetersiz stok nedeniyle siparis olusturulamadi.", stockProblems);
        }

        order.TotalAmount = order.Items.Sum(i => i.LineTotal);

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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

        return order is null
            ? throw new NotFoundException($"{id} numarali siparis bulunamadi.")
            : ToDetailResponse(order);
    }

    private static List<CreateOrderItemRequest> ValidateAndMergeItems(IReadOnlyCollection<CreateOrderItemRequest> requestItems)
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

        // TryParse tek basina yeterli degil: "7" gibi sayisal degerleri de kabul eder.
        if (!Enum.TryParse<PricingType>(value, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw new BusinessRuleException($"Gecersiz fiyatlandirma tipi: '{value}'. Gecerli degerler: Standard, Bulk.");
        }

        return parsed;
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
