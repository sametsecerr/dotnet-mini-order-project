namespace OrderApp.Api.Features.Orders;

/// <summary>
/// Oluşturulmuş bir sipariş. Toplam tutar hesaplandığı anda kaydedilir ve
/// ürün fiyatları sonradan değişse bile bir daha hesaplanmaz.
/// </summary>
public class Order
{
    public int Id { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// İstekte gelen opsiyonel fiyatlandırma tipi. Case'de bu alan için bir iş
    /// kuralı tanımlanmadığından sadece siparişle birlikte kayıt altına alınır.
    /// </summary>
    public PricingType PricingType { get; set; } = PricingType.Standard;

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Sipariş anındaki satır toplamlarının toplamı (dondurulmuş tutar).</summary>
    public decimal TotalAmount { get; set; }

    public List<OrderItem> Items { get; set; } = new();
}
