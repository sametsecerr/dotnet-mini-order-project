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
    private readonly TestDatabase _database = new();

    private static readonly int KeyboardId = 1;
    private static readonly int MouseId = 2;

    public OrderServiceTests()
    {
        using var db = _database.CreateContext();
        db.Products.AddRange(
            new Product { Id = KeyboardId, StockCode = "KB-1001", Name = "Mekanik Klavye", Price = 1000.00m, StockQuantity = 10, CreatedAtUtc = DateTime.UtcNow },
            new Product { Id = MouseId, StockCode = "MS-1002", Name = "Kablosuz Mouse", Price = 250.50m, StockQuantity = 3, CreatedAtUtc = DateTime.UtcNow });
        db.SaveChanges();
    }

    private OrderService CreateSut(AppDbContext db) =>
        new(db, new ProductCache(new MemoryCache(new MemoryCacheOptions()), NullLogger<ProductCache>.Instance),
            NullLogger<OrderService>.Instance);

    [Fact]
    public async Task CreateOrder_StoklariDusurur_VeToplamiDogruHesaplar()
    {
        await using var db = _database.CreateContext();
        var sut = CreateSut(db);

        var order = await sut.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerName = "Ahmet Yilmaz",
            Items =
            [
                new CreateOrderItemRequest { ProductId = KeyboardId, Quantity = 2 },
                new CreateOrderItemRequest { ProductId = MouseId, Quantity = 3 }
            ]
        }, CancellationToken.None);

        // 2 * 1000.00 + 3 * 250.50 = 2751.50
        order.TotalAmount.Should().Be(2751.50m);
        order.Items.Should().HaveCount(2);

        await using var verifyDb = _database.CreateContext();
        var products = await verifyDb.Products.AsNoTracking().ToDictionaryAsync(p => p.Id);
        products[KeyboardId].StockQuantity.Should().Be(8);
        products[MouseId].StockQuantity.Should().Be(0);
    }

    [Fact]
    public async Task CreateOrder_YetersizStokta_SiparisOlusturmaz_VeHicbirStogu_Dusurmez()
    {
        await using var db = _database.CreateContext();
        var sut = CreateSut(db);

        // İlk ürün için stok yeterli, ikinci ürün için yetersiz.
        // Beklenti: hiçbir stok düşmemeli, sipariş kaydı oluşmamalı.
        var act = async () => await sut.CreateOrderAsync(new CreateOrderRequest
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

        var products = await verifyDb.Products.AsNoTracking().ToDictionaryAsync(p => p.Id);
        products[KeyboardId].StockQuantity.Should().Be(10);
        products[MouseId].StockQuantity.Should().Be(3);
    }

    [Fact]
    public async Task CreateOrder_UrunFiyatiSonradanDegisse_Bile_SiparisTutariDegismez()
    {
        await using var db = _database.CreateContext();
        var sut = CreateSut(db);

        var created = await sut.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerName = "Ahmet Yilmaz",
            Items = [new CreateOrderItemRequest { ProductId = KeyboardId, Quantity = 2 }]
        }, CancellationToken.None);

        await using var updateDb = _database.CreateContext();
        var keyboard = await updateDb.Products.SingleAsync(p => p.Id == KeyboardId);
        keyboard.Price = 5000.00m;
        await updateDb.SaveChangesAsync();

        await using var readDb = _database.CreateContext();
        var reloaded = await CreateSut(readDb).GetOrderByIdAsync(created.Id, CancellationToken.None);

        reloaded.TotalAmount.Should().Be(2000.00m);
        reloaded.Items.Single().UnitPrice.Should().Be(1000.00m);
    }

    [Fact]
    public async Task CreateOrder_OlmayanUrunIcin_BusinessRuleException_Firlatir()
    {
        await using var db = _database.CreateContext();
        var sut = CreateSut(db);

        var act = async () => await sut.CreateOrderAsync(new CreateOrderRequest
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
    public async Task CreateOrder_MiktarSifirVeyaNegatifse_Reddedilir()
    {
        await using var db = _database.CreateContext();
        var sut = CreateSut(db);

        var act = async () => await sut.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerName = "Ahmet Yilmaz",
            Items = [new CreateOrderItemRequest { ProductId = KeyboardId, Quantity = 0 }]
        }, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*sifirdan buyuk*");
    }

    [Fact]
    public async Task CreateOrder_AyniUrunBirdenFazlaSatirdaGelirse_MiktarlarBirlestirilir()
    {
        await using var db = _database.CreateContext();
        var sut = CreateSut(db);

        var order = await sut.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerName = "Ahmet Yilmaz",
            Items =
            [
                new CreateOrderItemRequest { ProductId = MouseId, Quantity = 2 },
                new CreateOrderItemRequest { ProductId = MouseId, Quantity = 1 }
            ]
        }, CancellationToken.None);

        order.Items.Should().ContainSingle().Which.Quantity.Should().Be(3);

        await using var verifyDb = _database.CreateContext();
        (await verifyDb.Products.SingleAsync(p => p.Id == MouseId)).StockQuantity.Should().Be(0);
    }

    public void Dispose() => _database.Dispose();
}
