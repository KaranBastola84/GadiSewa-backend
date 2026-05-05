using System.Net;
using System.Text.Json;
using GadiSewa.Application.Common.Responses;

namespace GadiSewa.API.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
            _logger.LogError(ex, "Unhandled exception");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var status = HttpStatusCode.InternalServerError;
        var message = "An unexpected error occurred.";

        // Map known exception types to status codes if needed
        if (exception is UnauthorizedAccessException)
        {
            status = HttpStatusCode.Unauthorized;
            message = "Unauthorized.";
        }

        var response = ApiResponse<object>.Failure(message, (int)status);
        var payload = JsonSerializer.Serialize(response);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)status;
        return context.Response.WriteAsync(payload);
    }
}
