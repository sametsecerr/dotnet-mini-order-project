namespace OrderApp.Api.Features.Orders;

public enum PricingType
{
    Standard = 0,
    Bulk = 1
}

public class Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public PricingType PricingType { get; set; } = PricingType.Standard;
    public DateTime CreatedAtUtc { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}
