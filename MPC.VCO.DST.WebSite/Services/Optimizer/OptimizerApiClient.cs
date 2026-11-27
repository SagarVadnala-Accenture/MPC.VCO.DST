using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MPC.VCO.DST.WebSite.Services.Optimizer.Models;

namespace MPC.VCO.DST.WebSite.Services.Optimizer;

public sealed class OptimizerApiClient(
    HttpClient httpClient,
    DstApiAuthenticatedRequestSender requestSender,
    ILogger<OptimizerApiClient> logger)
    : IOptimizerApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient = httpClient;
    private readonly DstApiAuthenticatedRequestSender _requestSender = requestSender;
    private readonly ILogger<OptimizerApiClient> _logger = logger;
    public async Task<OptimizeResponseDto> OptimizeAsync(
        OptimizeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using HttpRequestMessage message = new(HttpMethod.Post, "api/optimizer")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        using HttpResponseMessage response = await SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadSuccessAsync<OptimizeResponseDto>(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _requestSender.SendAsync(
                _httpClient,
                request.Method,
                request.RequestUri!.ToString(),
                cancellationToken,
                request.Content);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Could not connect to the Optimizer API.");
            throw;
        }
    }

    private static async Task<T> ReadSuccessAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        T? result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return result ?? throw new JsonException("The Optimizer API returned an empty response.");
    }

    private async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        ValidationProblemDetailsDto? problem = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                problem = JsonSerializer.Deserialize<ValidationProblemDetailsDto>(body, JsonOptions);
            }
            catch (JsonException exception)
            {
                _logger.LogDebug(exception, "Optimizer API returned a non-ProblemDetails error body.");
            }
        }

        HttpStatusCode statusCode = response.StatusCode;
        string message = statusCode switch
        {
            HttpStatusCode.BadRequest => "Please correct optimizer input values.",
            HttpStatusCode.UnprocessableEntity => "No feasible optimization solution was found for the selected settings.",
            HttpStatusCode.TooManyRequests => "The optimizer is currently processing the maximum number of requests. Please retry.",
            HttpStatusCode.BadGateway => "Forecast data could not be retrieved or validated.",
            HttpStatusCode.UnsupportedMediaType => "The Optimizer API could not process the request format.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "You are not authorized to access the Optimizer API.",
            _ => "The optimizer could not complete the request. Please try again later."
        };

        if (statusCode is not (HttpStatusCode.BadRequest
            or HttpStatusCode.UnprocessableEntity
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.BadGateway
            or HttpStatusCode.UnsupportedMediaType
            or HttpStatusCode.Unauthorized
            or HttpStatusCode.Forbidden))
        {
            _logger.LogError("Optimizer API returned HTTP status {StatusCode}.", (int)statusCode);
        }

        throw new OptimizerApiException(
            statusCode,
            message,
            statusCode == HttpStatusCode.UnprocessableEntity ? problem?.Detail : null,
            statusCode == HttpStatusCode.BadRequest ? problem?.Errors : null);
    }
}

