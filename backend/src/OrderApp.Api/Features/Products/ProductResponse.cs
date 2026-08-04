namespace OrderApp.Api.Features.Products;

public record ProductResponse(
    int Id,
    string StockCode,
    string Name,
    decimal Price,
    int StockQuantity);
