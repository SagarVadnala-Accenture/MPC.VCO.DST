using MPC.VCO.DST.WebSite.Services.Optimizer.Models;

namespace MPC.VCO.DST.WebSite.Services.Optimizer;

public sealed class OptimizerScenarioStateStore
{
    public static readonly string[] VersionNames = ["Baseline", "Version 1", "Version 2", "Version 3", "Version 4"];

    private readonly Dictionary<string, OptimizerScenarioState[]> _scenarios = new(StringComparer.Ordinal);

    public OptimizerScenarioState[] GetVersions(string modelId)
    {
        string key = modelId ?? string.Empty;
        if (!_scenarios.TryGetValue(key, out OptimizerScenarioState[]? versions))
        {
            versions = VersionNames.Select(_ => new OptimizerScenarioState()).ToArray();
            _scenarios.Add(key, versions);
        }

        return versions;
    }
}

public sealed class OptimizerScenarioState
{
    public OptimizeRequestDto Parameters { get; set; } = new()
    {
        PlanningStartDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime),
        PlanningEndDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.AddDays(29).UtcDateTime),
        HorizonDays = 30
    };

    public OptimizeResponseDto? Result { get; set; }

    public OptimizeRequestDto? CompletedRequest { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public TimeSpan? Elapsed { get; set; }

    public HashSet<string> EnabledTrainOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, TrainSettingsDto> TrainOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> KnownTrainIds => Result?.TrainSchedule
        .Select(row => row.Train)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? [];
}
