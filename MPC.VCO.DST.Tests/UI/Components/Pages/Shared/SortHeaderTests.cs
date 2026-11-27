using Bunit;
using Bunit.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor.Services;
using MPC.VCO.DST.WebSite.Components.Pages.Shared;
using MPC.VCO.DST.WebSite.Models;

namespace MPC.VCO.DST.Tests.UI.Components.Pages.Shared;

[TestClass]
public sealed class SortHeaderTests
{
    [TestMethod]
    public async Task Render_ValidParameters_DisplaysHeaderTitle()
    {
        await using BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();

        IRenderedComponent<SortHeader> cut = context.Render<SortHeader>(
            parameters => parameters
                .Add(component => component.Title, "Model ID")
                .Add(component => component.Column, "modelId")
                .Add(component => component.Active, true));

        string markup = cut.Markup;
        StringAssert.Contains(markup, "Model ID");
        StringAssert.Contains(markup, "Sort by model id");
    }

    [TestMethod]
    public async Task Click_WithShiftKey_EmitsAdditiveSortRequest()
    {
        await using BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();

        SortRequest? captured = null;

        IRenderedComponent<SortHeader> cut = context.Render<SortHeader>(
            parameters => parameters
                .Add(component => component.Title, "Created On")
                .Add(component => component.Column, "createdOn")
                .Add(component => component.SortChanged, EventCallback.Factory.Create<SortRequest>(this, request => captured = request)));

        cut.Find("button").Click(new MouseEventArgs { ShiftKey = true });

        Assert.IsNotNull(captured);
        Assert.AreEqual("createdOn", captured!.Column);
        Assert.IsTrue(captured.Additive);
    }
}
