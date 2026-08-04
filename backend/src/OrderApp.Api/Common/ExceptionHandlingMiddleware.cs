using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace OrderApp.Api.Common;

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
            await WriteAsync(context, CreateProblem(context, StatusCodes.Status404NotFound, "Kayit bulunamadi", ex.Message));
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Eszamanli stok guncellemesi cakisti.");

            await WriteAsync(context, CreateProblem(
                context,
                StatusCodes.Status409Conflict,
                "Stok bilgisi degisti",
                "Urun stogu siz islem yaparken degisti. Lutfen tekrar deneyin."));
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Arama kutusu her tusta onceki istegi iptal ediyor; bu bir hata degil.
            _logger.LogDebug("Istek istemci tarafindan iptal edildi.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Beklenmeyen hata.");

            await WriteAsync(context, CreateProblem(
                context,
                StatusCodes.Status500InternalServerError,
                "Beklenmeyen bir hata olustu",
                "Istek islenirken beklenmeyen bir hata olustu."));
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
        context.Response.StatusCode = problem.Status!.Value;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
