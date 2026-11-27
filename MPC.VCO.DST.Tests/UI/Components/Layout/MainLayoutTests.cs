using System.Reflection;
using Bunit;
using Bunit.JSInterop;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using MPC.VCO.DST.WebSite.Components.Layout;

namespace MPC.VCO.DST.Tests.UI.Components.Layout;

[TestClass]
public sealed class MainLayoutTests
{
    [TestMethod]
    public async Task Render_ShowsBodyAndNetworkMapButton()
    {
        await using BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.ComponentFactories.AddStub<ApplicationHeader>();

        IRenderedComponent<MainLayout> cut = context.Render<MainLayout>(parameters =>
            parameters.Add(component => component.Body, builder =>
                builder.AddMarkupContent(0, "<div id='layout-body'>Body content</div>")));

        StringAssert.Contains(cut.Markup, "Network Map");
        StringAssert.Contains(cut.Markup, "Body content");
    }

    [TestMethod]
    public async Task NetworkMapButton_Click_DoesNotThrow()
    {
        await using BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.ComponentFactories.AddStub<ApplicationHeader>();

        IRenderedComponent<MainLayout> cut = context.Render<MainLayout>(parameters =>
            parameters.Add(component => component.Body, builder =>
                builder.AddMarkupContent(0, "<div>Body content</div>")));

        cut.Find("button.network-map").Click();

        StringAssert.Contains(cut.Markup, "Network Map");
    }

    [TestMethod]
    public void Theme_UsesExpectedBrandPaletteValues()
    {
        FieldInfo? themeField = typeof(MainLayout)
            .GetField("Theme", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(themeField);

        MudTheme theme = (MudTheme)themeField!.GetValue(null)!;

        Assert.AreEqual("#00509e", theme.PaletteLight.Primary);
        Assert.AreEqual("#2a3e5a", theme.PaletteLight.Secondary);
        Assert.AreEqual("#f8f8f8", theme.PaletteLight.Background);
        Assert.AreEqual("#ffffff", theme.PaletteLight.Surface);
        Assert.AreEqual("#007834", theme.PaletteLight.Success);
    }
}
