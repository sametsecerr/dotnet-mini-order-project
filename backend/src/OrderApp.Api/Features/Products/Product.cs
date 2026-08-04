namespace OrderApp.Api.Features.Products;

public class Product
{
    public int Id { get; set; }
    public string StockCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public int Version { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public bool TryReduceStock(int quantity)
    {
        if (quantity <= 0 || quantity > StockQuantity)
        {
            return false;
        }

        StockQuantity -= quantity;
        Version++;
        return true;
    }
}
