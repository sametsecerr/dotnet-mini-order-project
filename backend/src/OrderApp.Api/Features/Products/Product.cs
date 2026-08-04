namespace OrderApp.Api.Features.Products;

/// <summary>
/// Satışa açık ürün. Fiyat ve stok bilgisinin güncel hali burada tutulur;
/// geçmiş siparişler bu kaydın anlık kopyasını (snapshot) kendi içinde saklar.
/// </summary>
public class Product
{
    public int Id { get; set; }

    /// <summary>İşletmenin kullandığı benzersiz stok kodu (SKU).</summary>
    public string StockCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Güncel birim fiyat. Para değeri olduğu için decimal(18,2).</summary>
    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    /// <summary>
    /// Optimistic concurrency token. Stok her düştüğünde artırılır; böylece iki
    /// eşzamanlı sipariş aynı stoğu okuyup ikisi birden düşemez.
    /// </summary>
    public int Version { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Stoğu, yeterli olduğunu doğrulayarak düşürür.</summary>
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
