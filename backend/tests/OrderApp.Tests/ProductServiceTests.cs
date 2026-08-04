using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using OrderApp.Api.Features.Orders;
using OrderApp.Api.Features.Products;

namespace OrderApp.Tests;

public class ProductServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly ProductCache _cache = new(new MemoryCache(new MemoryCacheOptions()), NullLogger<ProductCache>.Instance);

    public ProductServiceTests()
    {
        using var db = _database.CreateContext();
        db.Products.AddRange(
            new Product { Id = 1, StockCode = "KB-1001", Name = "Mekanik Klavye", Price = 1000m, StockQuantity = 10, CreatedAtUtc = DateTime.UtcNow },
            new Product { Id = 2, StockCode = "MS-1002", Name = "Kablosuz Mouse", Price = 250m, StockQuantity = 5, CreatedAtUtc = DateTime.UtcNow });
        db.SaveChanges();
    }

    [Fact]
    public async Task GetProducts_SearchesByNameOrStockCode()
    {
        await using var db = _database.CreateContext();
        var sut = new ProductService(db, _cache);

        var byName = await sut.GetProductsAsync("klavye", CancellationToken.None);
        var byStockCode = await sut.GetProductsAsync("MS-1002", CancellationToken.None);

        byName.Should().ContainSingle().Which.StockCode.Should().Be("KB-1001");
        byStockCode.Should().ContainSingle().Which.Name.Should().Be("Kablosuz Mouse");
    }

    [Fact]
    public async Task GetProducts_TreatsWildcardCharactersAsLiterals()
    {
        await using var db = _database.CreateContext();
        var sut = new ProductService(db, _cache);

        (await sut.GetProductsAsync("%", CancellationToken.None)).Should().BeEmpty();
        (await sut.GetProductsAsync("KB_1001", CancellationToken.None)).Should().BeEmpty();
        (await sut.GetProductsAsync("KB-1001", CancellationToken.None)).Should().ContainSingle();
    }

    [Fact]
    public async Task GetProducts_IsCaseInsensitive()
    {
        await using var db = _database.CreateContext();
        var sut = new ProductService(db, _cache);

        var lowercase = await sut.GetProductsAsync("klavye", CancellationToken.None);
        var uppercase = await sut.GetProductsAsync("KLAVYE", CancellationToken.None);

        lowercase.Should().ContainSingle().Which.StockCode.Should().Be("KB-1001");
        uppercase.Should().BeEquivalentTo(lowercase);
    }

    [Fact]
    public async Task GetProducts_ReturnsFreshStock_AfterOrderInvalidatesCache()
    {
        await using var readDb = _database.CreateContext();
        var before = await new ProductService(readDb, _cache).GetProductsAsync(null, CancellationToken.None);
        before.Single(p => p.Id == 1).StockQuantity.Should().Be(10);

        await using var orderDb = _database.CreateContext();
        await new OrderService(orderDb, _cache, NullLogger<OrderService>.Instance).CreateOrderAsync(new CreateOrderRequest
        {
            CustomerName = "Ahmet Yilmaz",
            Items = [new CreateOrderItemRequest { ProductId = 1, Quantity = 4 }]
        }, CancellationToken.None);

        await using var afterDb = _database.CreateContext();
        var after = await new ProductService(afterDb, _cache).GetProductsAsync(null, CancellationToken.None);
        after.Single(p => p.Id == 1).StockQuantity.Should().Be(6);
    }

    public void Dispose() => _database.Dispose();
}
