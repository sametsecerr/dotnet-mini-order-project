using Microsoft.AspNetCore.Mvc;

namespace OrderApp.Api.Features.Products;

[ApiController]
[Route("api/products")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _productService;

    public ProductsController(ProductService productService)
    {
        _productService = productService;
    }

    /// <summary>Ürünleri listeler. search parametresi isim veya stok kodunda arar.</summary>
    /// <example>GET /api/products?search=klavye</example>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetProducts(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var products = await _productService.GetProductsAsync(search, cancellationToken);
        return Ok(products);
    }

    /// <summary>Tek bir ürünün detayını getirir.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetProduct(int id, CancellationToken cancellationToken)
    {
        var product = await _productService.GetProductByIdAsync(id, cancellationToken);
        return Ok(product);
    }
}
