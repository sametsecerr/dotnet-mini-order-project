using Microsoft.AspNetCore.Mvc;

namespace OrderApp.Api.Features.Orders;

[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;

    public OrdersController(OrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>Yeni sipariş oluşturur ve ilgili ürün stoklarını düşer.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDetailResponse>> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await _orderService.CreateOrderAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    /// <summary>Siparişleri en yeniden eskiye doğru listeler.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OrderSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryResponse>>> GetOrders(CancellationToken cancellationToken)
    {
        var orders = await _orderService.GetOrdersAsync(cancellationToken);
        return Ok(orders);
    }

    /// <summary>Sipariş detayını satırlarıyla birlikte getirir.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDetailResponse>> GetOrder(int id, CancellationToken cancellationToken)
    {
        var order = await _orderService.GetOrderByIdAsync(id, cancellationToken);
        return Ok(order);
    }
}
