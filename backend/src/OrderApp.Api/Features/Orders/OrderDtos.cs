using System.ComponentModel.DataAnnotations;

namespace OrderApp.Api.Features.Orders;

public class CreateOrderRequest
{
    [Required(ErrorMessage = "Musteri adi zorunludur.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Musteri adi 2-200 karakter olmalidir.")]
    public string CustomerName { get; set; } = string.Empty;

    public string? PricingType { get; set; }

    [Required(ErrorMessage = "Siparis en az bir urun icermelidir.")]
    [MinLength(1, ErrorMessage = "Siparis en az bir urun icermelidir.")]
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}

public class CreateOrderItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Gecerli bir urun secilmelidir.")]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Siparis miktari sifirdan buyuk olmalidir.")]
    public int Quantity { get; set; }
}

public record OrderItemResponse(
    int ProductId,
    string StockCode,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public record OrderSummaryResponse(
    int Id,
    string CustomerName,
    string PricingType,
    DateTime CreatedAtUtc,
    decimal TotalAmount,
    int ItemCount);

public record OrderDetailResponse(
    int Id,
    string CustomerName,
    string PricingType,
    DateTime CreatedAtUtc,
    decimal TotalAmount,
    IReadOnlyList<OrderItemResponse> Items);
