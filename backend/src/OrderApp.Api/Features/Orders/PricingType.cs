namespace OrderApp.Api.Features.Orders;

/// <summary>
/// Sipariş isteğinde gelen fiyatlandırma tipi. Case dokümanında bu alan için bir
/// hesaplama kuralı verilmediğinden fiyatı etkilemez, siparişle birlikte saklanır.
/// </summary>
public enum PricingType
{
    Standard = 0,
    Bulk = 1
}
