using System.Net;

namespace MPC.VCO.DST.WebSite.Services.Optimizer;

public sealed class OptimizerApiException : Exception
{
    public OptimizerApiException(
        HttpStatusCode statusCode,
        string userMessage,
        string? apiDetail = null,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
        : base(userMessage)
    {
        StatusCode = statusCode;
        UserMessage = userMessage;
        ApiDetail = apiDetail;
        ValidationErrors = validationErrors ?? new Dictionary<string, string[]>();
    }

    public HttpStatusCode StatusCode { get; }

    public string UserMessage { get; }

    public string? ApiDetail { get; }

    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }
}
