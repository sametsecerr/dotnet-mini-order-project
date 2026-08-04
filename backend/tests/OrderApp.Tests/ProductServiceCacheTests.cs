using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using OrderApp.Api.Data;
using OrderApp.Api.Features.Orders;
using OrderApp.Api.Features.Products;

namespace OrderApp.Tests;

public class ProductServiceCacheTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly ProductCache _cache = new(new MemoryCache(new MemoryCacheOptions()), NullLogger<ProductCache>.Instance);

    public ProductServiceCacheTests()
    {
        using var db = _database.CreateContext();
        db.Products.AddRange(
            new Product { Id = 1, StockCode = "KB-1001", Name = "Mekanik Klavye", Price = 1000m, StockQuantity = 10, CreatedAtUtc = DateTime.UtcNow },
            new Product { Id = 2, StockCode = "MS-1002", Name = "Kablosuz Mouse", Price = 250m, StockQuantity = 5, CreatedAtUtc = DateTime.UtcNow });
        db.SaveChanges();
    }

    [Fact]
    public async Task Arama_IsimVeyaStokKoduna_Gore_Filtreler()
    {
        await using var db = _database.CreateContext();
        var sut = new ProductService(db, _cache);

        var byName = await sut.GetProductsAsync("klavye", CancellationToken.None);
        var byStockCode = await sut.GetProductsAsync("MS-1002", CancellationToken.None);

        byName.Should().ContainSingle().Which.StockCode.Should().Be("KB-1001");
        byStockCode.Should().ContainSingle().Which.Name.Should().Be("Kablosuz Mouse");
    }

    [Fact]
    public async Task SiparisSonrasi_CacheTemizlenir_VeGuncelStokDoner()
    {
        await using var readDb = _database.CreateContext();
        var productService = new ProductService(readDb, _cache);

        var before = await productService.GetProductsAsync(null, CancellationToken.None);
        before.Single(p => p.Id == 1).StockQuantity.Should().Be(10);

        // Sipariş oluşturulunca OrderService cache'i invalidate etmeli.
        await using var orderDb = _database.CreateContext();
        var orderService = new OrderService(orderDb, _cache, NullLogger<OrderService>.Instance);
        await orderService.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerName = "Ahmet Yilmaz",
            Items = [new CreateOrderItemRequest { ProductId = 1, Quantity = 4 }]
        }, CancellationToken.None);

        // Aynı cache instance'ı üzerinden okunsa bile taze veri gelmeli.
        await using var afterDb = _database.CreateContext();
        var after = await new ProductService(afterDb, _cache).GetProductsAsync(null, CancellationToken.None);
        after.Single(p => p.Id == 1).StockQuantity.Should().Be(6);
    }

    public void Dispose() => _database.Dispose();
}
