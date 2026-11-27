using System.Globalization;
using MPC.VCO.DST.WebSite.Models;

namespace MPC.VCO.DST.WebSite.Services.ModelHistory;

public static class ModelHistoryTableStateHelper
{
    public static ModelHistoryTableState Build(
        IReadOnlyList<ModelRun> allModels,
        string search,
        int tabIndex,
        IReadOnlyList<ModelHistorySortDefinition> sorts,
        int page,
        int pageSize)
    {
        ArgumentNullException.ThrowIfNull(allModels);
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(sorts);

        IReadOnlyList<string> tabs =
        [
            "All Models",
            .. allModels
                .Select(model => model.ModelType)
                .Where(modelType => !string.IsNullOrWhiteSpace(modelType))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(modelType => modelType)
        ];

        int effectiveTabIndex =
            tabIndex >= 0 && tabIndex < tabs.Count
                ? tabIndex
                : 0;

        string? selectedType = effectiveTabIndex == 0
            ? null
            : tabs[effectiveTabIndex];

        string query = search.Trim();

        IEnumerable<ModelRun> models = allModels.Where(model =>
            (selectedType is null || string.Equals(model.ModelType, selectedType, StringComparison.OrdinalIgnoreCase))
            && (query.Length == 0 || SearchText(model).Contains(query, StringComparison.OrdinalIgnoreCase)));

        IOrderedEnumerable<ModelRun>? sorted = null;
        foreach (ModelHistorySortDefinition sort in sorts)
        {
            sorted = sorted is null
                ? sort.Descending
                    ? models.OrderByDescending(sort.Selector)
                    : models.OrderBy(sort.Selector)
                : sort.Descending
                    ? sorted.ThenByDescending(sort.Selector)
                    : sorted.ThenBy(sort.Selector);
        }

        IReadOnlyList<ModelRun> filtered = (sorted ?? models).ToArray();
        int pageCount = Math.Max(1, (filtered.Count + pageSize - 1) / pageSize);
        int effectivePage = Math.Clamp(page, 1, pageCount);

        return new ModelHistoryTableState(
            tabs,
            effectiveTabIndex,
            filtered,
            effectivePage,
            pageCount);
    }

    private static string SearchText(ModelRun model) =>
        string.Join(' ',
            model.Id,
            model.Status,
            model.Scope,
            model.Location,
            model.ModelType,
            model.CreatedOn.ToString("MM/dd/yyyy"),
            model.RecoveryText,
            model.ExpectedValueText,
            model.EthanePrice.ToString(CultureInfo.InvariantCulture),
            model.NaturalGasPrice.ToString(CultureInfo.InvariantCulture));
}

public sealed record ModelHistorySortDefinition(
    Func<ModelRun, IComparable> Selector,
    bool Descending);

public sealed record ModelHistoryTableState(
    IReadOnlyList<string> Tabs,
    int TabIndex,
    IReadOnlyList<ModelRun> Filtered,
    int Page,
    int PageCount);