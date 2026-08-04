using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OrderApp.Api.Common;
using OrderApp.Api.Data;
using OrderApp.Api.Features.Orders;
using OrderApp.Api.Features.Products;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "frontend";

// Para alanlari decimal(18,2) olarak modellenir. SQLite decimal'i TEXT olarak
// saklar; bu yuzden SQL tarafinda fiyata gore siralama/karsilastirma yapmiyoruz
// (listeleme isme gore siralanir, toplamlar uygulama tarafinda hesaplanir).
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ProductCache>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<OrderService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Mini Siparis API",
        Version = "v1",
        Description = "Urun listeleme/arama ve siparis olusturma islemleri."
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

// Migration + seed: uygulama ilk acildiginda veritabani hazir ve dolu olsun.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DatabaseSeeder.MigrateAndSeedAsync(db);
}

// Hata -> ProblemDetails cevirisi pipeline'in en basinda.
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Mini Siparis API v1"));

app.UseCors(FrontendCorsPolicy);
app.MapControllers();

app.Run();

/// <summary>Integration testlerin WebApplicationFactory ile bu projeyi kullanabilmesi icin.</summary>
public partial class Program;
