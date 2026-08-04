using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace OrderApp.Api.Common;

/// <summary>
/// Tüm hataları tek noktada ProblemDetails formatına çevirir. Böylece controller'lar
/// try/catch ile dolmaz ve API her durumda tutarlı bir hata gövdesi döner.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogInformation(ex, "Is kurali ihlali: {Message}", ex.Message);
            var problem = CreateProblem(context, StatusCodes.Status400BadRequest, "Islem gerceklestirilemedi", ex.Message);
            if (ex.Reasons.Count > 0)
            {
                problem.Extensions["reasons"] = ex.Reasons;
            }

            await WriteAsync(context, problem);
        }
        catch (NotFoundException ex)
        {
            var problem = CreateProblem(context, StatusCodes.Status404NotFound, "Kayit bulunamadi", ex.Message);
            await WriteAsync(context, problem);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Aynı ürün için eşzamanlı iki sipariş: concurrency token uyuşmadı,
            // hiçbir değişiklik kaydedilmedi.
            _logger.LogWarning(ex, "Eszamanli stok guncellemesi cakisti.");
            var problem = CreateProblem(
                context,
                StatusCodes.Status409Conflict,
                "Stok bilgisi degisti",
                "Urun stogu siz islem yaparken degisti. Lutfen tekrar deneyin.");
            await WriteAsync(context, problem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Beklenmeyen hata.");
            var problem = CreateProblem(
                context,
                StatusCodes.Status500InternalServerError,
                "Beklenmeyen bir hata olustu",
                "Istek islenirken beklenmeyen bir hata olustu.");
            await WriteAsync(context, problem);
        }
    }

    private static ProblemDetails CreateProblem(HttpContext context, int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = context.Request.Path,
        Extensions = { ["traceId"] = context.TraceIdentifier }
    };

    private static async Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
