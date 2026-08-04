using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using OrderApp.Api.Common;
using OrderApp.Api.Data;
using OrderApp.Api.Features.Orders;
using OrderApp.Api.Features.Products;

namespace OrderApp.Tests;

public class OrderServiceTests : IDisposable
{
    private const int KeyboardId = 1;
    private const int MouseId = 2;

    private readonly TestDatabase _database = new();

    public OrderServiceTests()
    {
        using var db = _database.CreateContext();
        db.Products.AddRange(
            new Product { Id = KeyboardId, StockCode = "KB-1001", Name = "Mekanik Klavye", Price = 1000.00m, StockQuantity = 10, CreatedAtUtc = DateTime.UtcNow },
            new Product { Id = MouseId, StockCode = "MS-1002", Name = "Kablosuz Mouse", Price = 250.50m, StockQuantity = 3, CreatedAtUtc = DateTime.UtcNow });
        db.SaveChanges();
    }

    [Fact]
    public async Task CreateOrder_ReducesStockAndCalculatesTotal()
    {
        await using var db = _database.CreateContext();

        var order = await CreateSut(db).CreateOrderAsync(new CreateOrderRequest
        {
            CustomerName = "Ahmet Yilmaz",
            Items =
            [
                new CreateOrderItemRequest { ProductId = KeyboardId, Quantity = 2 },
                new CreateOrderItemRequest { ProductId = MouseId, Quantity = 3 }
            ]
        }, CancellationToken.None);

        // 2 * 1000.00 + 3 * 250.50
        order.TotalAmount.Should().Be(2751.50m);
        order.Items.Should().HaveCount(2);

        var products = await ReadProductsAsync();
        products[KeyboardId].StockQuantity.Should().Be(8);
        products[MouseId].StockQuantity.Should().Be(0);
    }

    [Fact]
    public async Task CreateOrder_WhenStockIsInsufficient_LeavesOrdersAndStockUntouched()
    {
        await using var db = _database.CreateContext();

        // Ilk urun icin stok yeterli, ikinci urun icin degil.
        var act = async () => await CreateSut(db).CreateOrderAsync(new CreateOrderRequest
        {
            CustomerName = "Ahmet Yilmaz",
            Items =
            [
                new CreateOrderItemRequest { ProductId = KeyboardId, Quantity = 1 },
                new CreateOrderItemRequest { ProductId = MouseId, Quantity = 99 }
            ]
        }, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<BusinessRuleException>();
        exception.Which.Reasons.Should().ContainSingle().Which.Should().Contain("MS-1002");

        await using var verifyDb = _database.CreateContext();
        (await verifyDb.Orders.CountAsync()).Should().Be(0);
        (await verifyDb.OrderItems.CountAsync()).Should().Be(0);

        var products = await ReadProductsAsync();
        products[KeyboardId].StockQuantity.Should().Be(10);
        products[MouseId].StockQuantity.Should().Be(3);
    }

    [Fact]
    public async Task CreateOrder_KeepsOrderTotal_WhenProductPriceChangesLater()
    {
        await using var db = _database.CreateContext();
        var created = await CreateSut(db).CreateOrderAsync(new CreateOrderRequest
        {
            CustomerName = "Ahmet Yilmaz",
            Items = [new CreateOrderItemRequest { ProductId = KeyboardId, Quantity = 2 }]
        }, CancellationToken.None);

        await using var updateDb = _database.CreateContext();
        (await updateDb.Products.SingleAsync(p => p.Id == KeyboardId)).Price = 5000.00m;
        await updateDb.SaveChangesAsync();

        await using var readDb = _database.CreateContext();
        var reloaded = await CreateSut(readDb).GetOrderByIdAsync(created.Id, CancellationToken.None);

        reloaded.TotalAmount.Should().Be(2000.00m);
        reloaded.Items.Single().UnitPrice.Should().Be(1000.00m);
    }

    [Fact]
    public async Task CreateOrder_WhenProductDoesNotExist_Throws()
    {
        await using var db = _database.CreateContext();

        var act = async () => await CreateSut(db).CreateOrderAsync(new CreateOrderRequest
        {
            CustomerName = "Ahmet Yilmaz",
            Items = [new CreateOrderItemRequest { ProductId = 9999, Quantity = 1 }]
        }, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Reasons.Any(r => r.Contains("9999")));

        await using var verifyDb = _database.CreateContext();
        (await verifyDb.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateOrder_WhenQuantityIsNotPositive_Throws()
    {
        await using var db = _database.CreateContext();

        var act = async () => await CreateSut(db).CreateOrderAsync(new CreateOrderRequest
        {
            CustomerName = "Ahmet Yilmaz",
            Items = [new CreateOrderItemRequest { ProductId = KeyboardId, Quantity = 0 }]
        }, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*sifirdan buyuk*");
    }

    [Fact]
    public async Task CreateOrder_MergesDuplicateLinesForSameProduct()
    {
        await using var db = _database.CreateContext();

        var order = await CreateSut(db).CreateOrderAsync(new CreateOrderRequest
        {
            CustomerName = "Ahmet Yilmaz",
            Items =
            [
                new CreateOrderItemRequest { ProductId = MouseId, Quantity = 2 },
                new CreateOrderItemRequest { ProductId = MouseId, Quantity = 1 }
            ]
        }, CancellationToken.None);

        order.Items.Should().ContainSingle().Which.Quantity.Should().Be(3);
        (await ReadProductsAsync())[MouseId].StockQuantity.Should().Be(0);
    }

    private OrderService CreateSut(AppDbContext db) => new(
        db,
        new ProductCache(new MemoryCache(new MemoryCacheOptions()), NullLogger<ProductCache>.Instance),
        NullLogger<OrderService>.Instance);

    private async Task<Dictionary<int, Product>> ReadProductsAsync()
    {
        await using var db = _database.CreateContext();
        return await db.Products.AsNoTracking().ToDictionaryAsync(p => p.Id);
    }

    public void Dispose() => _database.Dispose();
}
