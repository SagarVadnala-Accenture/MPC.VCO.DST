using Bunit;
using Bunit.JSInterop;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using MudBlazor.Services;
using MPC.VCO.DST.WebSite.Components.Pages.Shared;
using MPC.VCO.DST.WebSite.Models;

namespace MPC.VCO.DST.Tests.UI.Components.Pages.Shared;

[TestClass]
public sealed class PlanningCycleCardTests
{
    [TestMethod]
    public async Task Render_BestModelMissing_DisplaysDash()
    {
        await using BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();

        PlanningCycle cycle = new(
            "PC-2026-07",
            new DateOnly(2026, 7, 1),
            0,
            null,
            null,
            null);

        IRenderedComponent<PlanningCycleCard> cut = context.Render<PlanningCycleCard>(
            parameters => parameters
                .Add(component => component.Cycle, cycle)
                .Add(component => component.Selected, false));

        string markup = cut.Markup;

        StringAssert.Contains(markup, "Published Model ID");
        StringAssert.Contains(markup, "—");
    }

    [TestMethod]
    public async Task Render_BestModelPresent_RendersOptimizerLink()
    {
        await using BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();

        PlanningCycle cycle = new(
            "PC-2026-07",
            new DateOnly(2026, 7, 1),
            0,
            "MODEL-001",
            71,
            219);

        IRenderedComponent<PlanningCycleCard> cut = context.Render<PlanningCycleCard>(
            parameters => parameters
                .Add(component => component.Cycle, cycle)
                .Add(component => component.Selected, true));

        IElement link = cut.Find("a.model-id-link");
        Assert.AreEqual("/optimizer/MODEL-001", link.GetAttribute("href"));
    }

    [TestMethod]
    public async Task Click_Card_RaisesSelectedChangedWithCyclePeriod()
    {
        await using BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();

        DateOnly? selected = null;
        PlanningCycle cycle = new(
            "PC-2026-07",
            new DateOnly(2026, 7, 1),
            0,
            "MODEL-001",
            71,
            219);

        IRenderedComponent<PlanningCycleCard> cut = context.Render<PlanningCycleCard>(
            parameters => parameters
                .Add(component => component.Cycle, cycle)
                .Add(component => component.SelectedChanged, EventCallback.Factory.Create<DateOnly>(this, value => selected = value)));

        cut.Find(".cycle-card").Click();

        Assert.AreEqual(new DateOnly(2026, 7, 1), selected);
    }
}
