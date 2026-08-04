namespace OrderApp.Api.Features.Products;

/// <summary>API'nin döndüğü ürün gösterimi. Entity doğrudan dışarı verilmez.</summary>
public record ProductResponse(
    int Id,
    string StockCode,
    string Name,
    decimal Price,
    int StockQuantity);
