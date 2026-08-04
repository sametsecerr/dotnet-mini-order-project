using Microsoft.EntityFrameworkCore;
using OrderApp.Api.Features.Products;

namespace OrderApp.Api.Data;

public static class DatabaseSeeder
{
    public static async Task MigrateAndSeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);

        if (await db.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        var createdAt = DateTime.UtcNow;
        db.Products.AddRange(
            new Product { StockCode = "KB-1001", Name = "Mekanik Klavye", Price = 1899.90m, StockQuantity = 25, CreatedAtUtc = createdAt },
            new Product { StockCode = "MS-1002", Name = "Kablosuz Mouse", Price = 749.50m, StockQuantity = 40, CreatedAtUtc = createdAt },
            new Product { StockCode = "MN-2001", Name = "27\" 4K Monitor", Price = 12499.00m, StockQuantity = 8, CreatedAtUtc = createdAt },
            new Product { StockCode = "HS-3001", Name = "Kulakustu Kulaklik", Price = 2299.00m, StockQuantity = 15, CreatedAtUtc = createdAt },
            new Product { StockCode = "DK-4001", Name = "USB-C Dock Station", Price = 3150.75m, StockQuantity = 12, CreatedAtUtc = createdAt },
            new Product { StockCode = "WC-5001", Name = "1080p Webcam", Price = 1120.00m, StockQuantity = 0, CreatedAtUtc = createdAt });

        await db.SaveChangesAsync(cancellationToken);
    }
}
