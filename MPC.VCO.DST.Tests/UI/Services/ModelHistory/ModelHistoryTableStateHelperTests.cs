using MPC.VCO.DST.WebSite.Models;
using MPC.VCO.DST.WebSite.Services.ModelHistory;

namespace MPC.VCO.DST.Tests.UI.Services.ModelHistory;

[TestClass]
public sealed class ModelHistoryTableStateHelperTests
{
    [TestMethod]
    public void Build_TabOutOfRange_ResetsToAllModelsTab()
    {
        IReadOnlyList<ModelRun> models =
        [
            CreateModelRun("M-001", "Type-B"),
            CreateModelRun("M-002", "Type-A")
        ];

        ModelHistoryTableState state = ModelHistoryTableStateHelper.Build(
            models,
            search: string.Empty,
            tabIndex: 99,
            sorts: [],
            page: 1,
            pageSize: 10);

        Assert.AreEqual(0, state.TabIndex);
        CollectionAssert.AreEqual(new[] { "All Models", "Type-A", "Type-B" }, state.Tabs.ToArray());
        Assert.AreEqual(2, state.Filtered.Count);
    }

    [TestMethod]
    public void Build_SearchAndTabFilter_ReturnsMatchingRowsOnly()
    {
        IReadOnlyList<ModelRun> models =
        [
            CreateModelRun("AAA-001", "Type-A") with { Scope = "Refinery" },
            CreateModelRun("BBB-002", "Type-B") with { Scope = "Terminal" },
            CreateModelRun("AAA-003", "Type-B") with { Scope = "Terminal" }
        ];

        ModelHistoryTableState state = ModelHistoryTableStateHelper.Build(
            models,
            search: "AAA",
            tabIndex: 2,
            sorts: [],
            page: 1,
            pageSize: 10);

        Assert.AreEqual(3, state.Tabs.Count);
        Assert.AreEqual("Type-B", state.Tabs[state.TabIndex]);
        Assert.AreEqual(1, state.Filtered.Count);
        Assert.AreEqual("AAA-003", state.Filtered[0].Id);
    }

    [TestMethod]
    public void Build_SortsAndClampsPage_ReturnsExpectedOrderingAndPagination()
    {
        IReadOnlyList<ModelRun> models =
        [
            CreateModelRun("M-003", "Type-A") with { ExpectedValue = 150 },
            CreateModelRun("M-001", "Type-A") with { ExpectedValue = 200 },
            CreateModelRun("M-002", "Type-A") with { ExpectedValue = 200 }
        ];

        IReadOnlyList<ModelHistorySortDefinition> sorts =
        [
            new(model => model.ExpectedValue ?? -1, Descending: true),
            new(model => model.Id, Descending: false)
        ];

        ModelHistoryTableState state = ModelHistoryTableStateHelper.Build(
            models,
            search: string.Empty,
            tabIndex: 0,
            sorts: sorts,
            page: 99,
            pageSize: 2);

        Assert.AreEqual(2, state.PageCount);
        Assert.AreEqual(2, state.Page);
        CollectionAssert.AreEqual(
            new[] { "M-001", "M-002", "M-003" },
            state.Filtered.Select(model => model.Id).ToArray());
    }

    private static ModelRun CreateModelRun(string id, string modelType)
    {
        return new ModelRun(
            id,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 2),
            ModelStatus.Draft,
            "Refinery",
            "TX",
            modelType,
            1.2m,
            2.3m,
            70,
            210,
            "PC-2026-07");
    }
}
