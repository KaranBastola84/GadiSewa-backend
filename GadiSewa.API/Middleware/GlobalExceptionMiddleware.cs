using GadiSewa.Application.Common.Exceptions;
using GadiSewa.Application.Common.Responses;

namespace GadiSewa.API.Middleware;

public sealed class GlobalExceptionMiddleware : IMiddleware
{
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        ILogger<GlobalExceptionMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception for {Method} {Path}. TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);

            if (context.Response.HasStarted)
            {
                _logger.LogWarning("Response has already started, exception middleware cannot write error response.");
                throw;
            }

            await HandleExceptionAsync(context, ex, _environment);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception, IWebHostEnvironment environment)
    {
        var (statusCode, message) = MapException(exception, environment.IsDevelopment());
        var payload = ApiResponse<object?>.Failure(message, statusCode);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(payload);
    }

    private static (int StatusCode, string Message) MapException(Exception exception, bool isDevelopment)
    {
        return exception switch
        {
            UnauthorizedException => (StatusCodes.Status401Unauthorized, exception.Message),
            NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, exception.Message),
            ArgumentException => (StatusCodes.Status400BadRequest, exception.Message),
            FormatException => (StatusCodes.Status400BadRequest, exception.Message),
            _ => (StatusCodes.Status500InternalServerError, isDevelopment ? exception.Message : "An unexpected error occurred.")
        };
    }
}
