using OrderApp.Api.Features.Products;

namespace OrderApp.Api.Features.Orders;

/// <summary>
/// Sipariş satırı. Ürüne FK ile bağlıdır fakat fiyat/isim/stok kodu sipariş
/// anındaki değerleriyle kopyalanır (snapshot). Ürün sonradan güncellense bile
/// geçmiş sipariş aynı kalır.
/// </summary>
public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string ProductStockCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;

    /// <summary>Sipariş anındaki birim fiyat.</summary>
    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    /// <summary>Sipariş anında hesaplanıp kaydedilen satır tutarı.</summary>
    public decimal LineTotal { get; set; }
}
