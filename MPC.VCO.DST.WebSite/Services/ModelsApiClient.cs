using System.Net.Http.Json;
using MPC.VCO.DST.WebSite.Models;

namespace MPC.VCO.DST.WebSite.Services;

public sealed class ModelsApiClient(
    HttpClient httpClient,
    DstApiAuthenticatedRequestSender requestSender,
    ILogger<ModelsApiClient> logger)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly DstApiAuthenticatedRequestSender _requestSender = requestSender;
    private readonly ILogger<ModelsApiClient> _logger = logger;

    public async Task<IReadOnlyList<ModelRun>>
        GetModelsAsync(
            PlanningCycle cycle,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cycle);

        if (string.IsNullOrWhiteSpace(cycle.PlanningCycleId))
        {
            return [];
        }

        const int pageSize = 100;
        int pageNumber = 1;
        List<ModelRun> results = [];

        while (true)
        {
            string route =
                $"api/planning-cycles/{cycle.PlanningCycleId}/models?pageNumber={pageNumber}&pageSize={pageSize}";

            using HttpResponseMessage response =
                await _requestSender.SendAsync(
                    _httpClient,
                    HttpMethod.Get,
                    route,
                    cancellationToken);

            response.EnsureSuccessStatusCode();

            List<ModelApiResponse> page =
                await response.Content.ReadFromJsonAsync<List<ModelApiResponse>>(
                    cancellationToken: cancellationToken)
                ?? [];

            if (page.Count == 0)
            {
                break;
            }

            results.AddRange(
                page.Select(model => Map(cycle, model)));

            if (page.Count < pageSize)
            {
                break;
            }

            pageNumber++;
        }

        return results;
    }

    public async Task<ModelRun?>
        GetModelAsync(
            string planningCycleId,
            string modelId,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(planningCycleId))
        {
            throw new ArgumentException("Planning cycle id is required.", nameof(planningCycleId));
        }

        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("Model id is required.", nameof(modelId));
        }

        try
        {
            using HttpResponseMessage response =
                await _requestSender.SendAsync(
                    _httpClient,
                    HttpMethod.Get,
                    $"api/planning-cycles/{planningCycleId}/models/{modelId}",
                    cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            ModelApiResponse? model =
                await response.Content.ReadFromJsonAsync<ModelApiResponse>(
                    cancellationToken: cancellationToken);

            if (model is null)
            {
                return null;
            }

            DateOnly period = ParsePeriodFromPlanningCycleId(planningCycleId);

            return Map(
                new PlanningCycle(planningCycleId, period, 0, null, null, null),
                model);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or InvalidOperationException)
        {
            _logger.LogError(
                exception,
                "Failed to get model {ModelId} for planning cycle {PlanningCycleId}.",
                modelId,
                planningCycleId);

            throw;
        }
    }

    public async Task<ModelRun>
        CreateModelAsync(
            string planningCycleId,
            ModelUpsertRequest request,
            CancellationToken cancellationToken = default)
    {
        ValidateUpsertRequest(planningCycleId, request);

        var payload = new
        {
            Status = request.Status,
            Scope = new
            {
                Type = request.ScopeType,
                Name = request.ScopeName
            },
            ModelType = request.ModelType,
            EthanePriceUsdPerGal = request.EthanePriceUsdPerGal,
            NaturalGasPriceUsdPerMmbtu = request.NaturalGasPriceUsdPerMmbtu
        };

        using HttpResponseMessage response =
            await _requestSender.SendAsync(
                _httpClient,
                HttpMethod.Post,
                $"api/planning-cycles/{planningCycleId}/models",
                cancellationToken,
                JsonContent.Create(payload));

        response.EnsureSuccessStatusCode();

        ModelApiResponse model =
            await response.Content.ReadFromJsonAsync<ModelApiResponse>(
                cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(
                "API returned an empty model payload after create operation.");

        DateOnly period = ParsePeriodFromPlanningCycleId(planningCycleId);

        return Map(
            new PlanningCycle(planningCycleId, period, 0, null, null, null),
            model);
    }

    public async Task<ModelRun?>
        UpdateModelAsync(
            string planningCycleId,
            string modelId,
            ModelUpsertRequest request,
            CancellationToken cancellationToken = default)
    {
        ValidateUpsertRequest(planningCycleId, request);

        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException(
                "Model id is required.",
                nameof(modelId));
        }

        var payload = new
        {
            Status = request.Status,
            Scope = new
            {
                Type = request.ScopeType,
                Name = request.ScopeName
            },
            ModelType = request.ModelType,
            EthanePriceUsdPerGal = request.EthanePriceUsdPerGal,
            NaturalGasPriceUsdPerMmbtu = request.NaturalGasPriceUsdPerMmbtu
        };

        using HttpResponseMessage response =
            await _requestSender.SendAsync(
                _httpClient,
                HttpMethod.Put,
                $"api/planning-cycles/{planningCycleId}/models/{modelId}",
                cancellationToken,
                JsonContent.Create(payload));

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        ModelApiResponse model =
            await response.Content.ReadFromJsonAsync<ModelApiResponse>(
                cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(
                "API returned an empty model payload after update operation.");

        DateOnly period = ParsePeriodFromPlanningCycleId(planningCycleId);

        return Map(
            new PlanningCycle(planningCycleId, period, 0, null, null, null),
            model);
    }

    public async Task<bool>
        DeleteModelAsync(
            string planningCycleId,
            string modelId,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(planningCycleId))
        {
            throw new ArgumentException(
                "Planning cycle id is required.",
                nameof(planningCycleId));
        }

        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException(
                "Model id is required.",
                nameof(modelId));
        }

        using HttpResponseMessage response =
            await _requestSender.SendAsync(
                _httpClient,
                HttpMethod.Delete,
                $"api/planning-cycles/{planningCycleId}/models/{modelId}",
                cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();

        return true;
    }

    public async Task<IReadOnlyList<MentionUser>> SearchMentionUsersAsync(
        string search,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        using HttpResponseMessage response = await _requestSender.SendAsync(
            _httpClient,
            HttpMethod.Get,
            $"api/users/mentions?search={Uri.EscapeDataString(search)}",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        List<MentionUserApiResponse> users =
            await response.Content.ReadFromJsonAsync<List<MentionUserApiResponse>>(
                cancellationToken: cancellationToken)
            ?? [];

        return users.Select(user => new MentionUser(user.Id, user.DisplayName, user.Email)).ToArray();
    }

    public async Task<IReadOnlyList<ModelNote>>
        GetModelNotesAsync(
            string planningCycleId,
            string modelId,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("Model id is required.", nameof(modelId));
        }

        _ = planningCycleId;

        using HttpResponseMessage response =
            await _requestSender.SendAsync(
                _httpClient,
                HttpMethod.Get,
                $"api/models/{modelId}/notes",
                cancellationToken);

        response.EnsureSuccessStatusCode();

        List<ModelNoteApiResponse> notes =
            await response.Content.ReadFromJsonAsync<List<ModelNoteApiResponse>>(
                cancellationToken: cancellationToken)
            ?? [];

        return notes
            .Select(note => new ModelNote(
                note.Id,
                note.ModelId,
                note.Author.DisplayName,
                note.Author.UserPrincipalName,
                note.NoteText,
                note.CreatedAtUtc,
                note.Mentions.Select(mention => new NoteMention(
                    mention.UserId,
                    mention.DisplayName,
                    mention.Email,
                    mention.Start,
                    mention.Length)).ToArray()))
            .ToArray();
    }

    public async Task<ModelNote>
        CreateModelNoteAsync(
            string planningCycleId,
            string modelId,
            CreateModelNoteRequest request,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("Model id is required.", nameof(modelId));
        }

        ArgumentNullException.ThrowIfNull(request);

        _ = planningCycleId;

        string text = request.Text?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            throw new ArgumentException("Note text is required.", nameof(request));
        }

        using HttpResponseMessage response =
            await _requestSender.SendAsync(
                _httpClient,
                HttpMethod.Post,
                $"api/models/{modelId}/notes",
                cancellationToken,
                JsonContent.Create(new
                {
                    noteText = text,
                    mentions = request.Mentions.Select(mention => new
                    {
                        userId = mention.UserId,
                        start = mention.Start,
                        length = mention.Length
                    }).ToArray()
                }));

        response.EnsureSuccessStatusCode();

        ModelNoteApiResponse note =
            await response.Content.ReadFromJsonAsync<ModelNoteApiResponse>(
                cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty note payload after create operation.");

        return new ModelNote(
            note.Id,
            note.ModelId,
            note.Author.DisplayName,
            note.Author.UserPrincipalName,
            note.NoteText,
            note.CreatedAtUtc,
            note.Mentions.Select(mention => new NoteMention(
                mention.UserId,
                mention.DisplayName,
                mention.Email,
                mention.Start,
                mention.Length)).ToArray());
    }

    private static ModelRun Map(
        PlanningCycle cycle,
        ModelApiResponse model)
    {
        ModelStatus status = MapStatus(model.Status);

        int recovery =
            (int)Math.Round(
                model.AverageRecoveryPercent,
                MidpointRounding.AwayFromZero);

        int expectedValue =
            (int)Math.Round(
                model.ExpectedValueMonthlyUsd / 1000m,
                MidpointRounding.AwayFromZero);

        return new ModelRun(
            model.ModelId,
            cycle.Period,
            model.CreatedOn,
            status,
            model.Scope.Type,
            model.Scope.Name,
            model.ModelType,
            model.EthanePriceUsdPerGal,
            model.NaturalGasPriceUsdPerMmbtu,
            recovery,
            expectedValue,
            cycle.PlanningCycleId);
    }

    private static ModelStatus MapStatus(string status)
    {
        return status.Trim().ToLowerInvariant() switch
        {
            "executable" => ModelStatus.Executable,
            "reviewed" => ModelStatus.Reviewed,
            _ => ModelStatus.Draft
        };
    }

    private static DateOnly ParsePeriodFromPlanningCycleId(string planningCycleId)
    {
        string[] segments = planningCycleId.Split('-', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length >= 2
            && int.TryParse(segments[0], out int year)
            && int.TryParse(segments[1], out int month)
            && month is >= 1 and <= 12)
        {
            return new DateOnly(year, month, 1);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new DateOnly(now.Year, now.Month, 1);
    }

    private static void ValidateUpsertRequest(
        string planningCycleId,
        ModelUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(planningCycleId))
        {
            throw new ArgumentException(
                "Planning cycle id is required.",
                nameof(planningCycleId));
        }

        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Status)
            || string.IsNullOrWhiteSpace(request.ScopeType)
            || string.IsNullOrWhiteSpace(request.ScopeName)
            || string.IsNullOrWhiteSpace(request.ModelType))
        {
            throw new ArgumentException(
                "Status, scope, and model type are required.",
                nameof(request));
        }

        if (request.EthanePriceUsdPerGal <= 0
            || request.NaturalGasPriceUsdPerMmbtu <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Ethane and natural gas prices must be greater than zero.");
        }
    }
}
