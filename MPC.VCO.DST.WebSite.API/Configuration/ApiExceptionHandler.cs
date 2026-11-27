using System.Security.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using MPC.VCO.DST.Application.Exceptions;

namespace MPC.VCO.DST.WebSite.API.Configuration;

public sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger,
    IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger = logger;
    private readonly IProblemDetailsService _problemDetailsService = problemDetailsService;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (int statusCode, string title, string detail) = exception switch
        {
            OptimizerValidationException => (
            StatusCodes.Status400BadRequest,
            "Validation Failed",
            "One or more optimizer settings are invalid."),
            ForecastUnavailableException => (
                StatusCodes.Status502BadGateway,
                "Bad Gateway",
                "Forecast data could not be retrieved or validated."),
            OptimizerInfeasibleException infeasible => (
                StatusCodes.Status422UnprocessableEntity,
                "Unprocessable Entity",
                infeasible.Message),
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested => (
                StatusCodes.Status499ClientClosedRequest,
                "Client Closed Request",
                "The request was canceled by the client."),
            ArgumentException => (
                StatusCodes.Status400BadRequest,
                "Bad Request",
                exception.Message),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "Not Found",
                "The requested resource was not found."),
            AuthenticationException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Authentication is required to access this resource."),
            UnauthorizedAccessException => (
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "You do not have permission to access this resource."),
            HttpRequestException => (
                StatusCodes.Status502BadGateway,
                "Bad Gateway",
                "User search is temporarily unavailable."),
            MicrosoftIdentityWebChallengeUserException => (
                StatusCodes.Status502BadGateway,
                "Bad Gateway",
                "User search is temporarily unavailable."),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred.")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception for request {Method} {Path}.",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;

        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{statusCode}",
            Instance = httpContext.Request.Path
        };

        await _problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });

        return true;
    }
}

public static class ApiExceptionHandlingServiceCollectionExtensions
{
    public static IServiceCollection AddApiExceptionHandling(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        return services;
    }
}
