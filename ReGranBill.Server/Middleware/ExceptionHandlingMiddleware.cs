using System.Text.Json;
using ReGranBill.Server.Exceptions;

namespace ReGranBill.Server.Middleware;

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
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            AppException appException => (appException.StatusCode, appException.ClientMessage),
            InvalidOperationException invalidOperationException => (StatusCodes.Status400BadRequest, invalidOperationException.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Request failed with status code {StatusCode}", statusCode);
        }
        else
        {
            _logger.LogWarning(exception, "Request rejected with status code {StatusCode}", statusCode);
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var payload = JsonSerializer.Serialize(new { statusCode, message });
        await context.Response.WriteAsync(payload);
    }
}
