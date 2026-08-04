using Microsoft.EntityFrameworkCore;
using OrderApp.Api.Features.Orders;
using OrderApp.Api.Features.Products;

namespace OrderApp.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(p => p.StockCode).IsRequired().HasMaxLength(32);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Price).HasPrecision(18, 2);
            entity.Property(p => p.Version).IsConcurrencyToken();

            entity.HasIndex(p => p.StockCode).IsUnique();
            entity.HasIndex(p => p.Name);

            entity.ToTable(t =>
            {
                // CAST sart: SQLite decimal'i TEXT sakliyor, "Price" >= 0 her zaman true doner.
                t.HasCheckConstraint("CK_Products_Price_NonNegative", "CAST(\"Price\" AS REAL) >= 0");
                t.HasCheckConstraint("CK_Products_Stock_NonNegative", "\"StockQuantity\" >= 0");
            });
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(o => o.CustomerName).IsRequired().HasMaxLength(200);
            entity.Property(o => o.TotalAmount).HasPrecision(18, 2);
            entity.Property(o => o.PricingType).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(o => o.CreatedAtUtc);

            entity.HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(i => i.ProductStockCode).IsRequired().HasMaxLength(32);
            entity.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
            entity.Property(i => i.UnitPrice).HasPrecision(18, 2);
            entity.Property(i => i.LineTotal).HasPrecision(18, 2);

            entity.HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t =>
                t.HasCheckConstraint("CK_OrderItems_Quantity_Positive", "\"Quantity\" > 0"));
        });
    }
}
