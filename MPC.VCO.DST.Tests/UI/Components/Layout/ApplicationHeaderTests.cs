using Bunit;
using Bunit.JSInterop;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using MPC.VCO.DST.WebSite.Components.Layout;

namespace MPC.VCO.DST.Tests.UI.Components.Layout;

[TestClass]
public sealed class ApplicationHeaderTests
{
    [TestMethod]
    public void DeriveInitials_WhitespaceName_ReturnsDefaultInitials()
    {
        using BunitContext context = CreateContext(new StubTimeProvider(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)));

        IRenderedComponent<CascadingAuthenticationState> cut = RenderHeader(context, "   ");

        Assert.AreEqual("DU", cut.Find(".user-avatar").TextContent.Trim());
    }

    [TestMethod]
    public void DeriveInitials_SingleSegment_ReturnsFirstCharacterUppercase()
    {
        using BunitContext context = CreateContext(new StubTimeProvider(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)));

        IRenderedComponent<CascadingAuthenticationState> cut = RenderHeader(context, "marathon");

        Assert.AreEqual("M", cut.Find(".user-avatar").TextContent.Trim());
    }

    [TestMethod]
    public void DeriveInitials_MultiSegment_ReturnsFirstAndLastCharacters()
    {
        using BunitContext context = CreateContext(new StubTimeProvider(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)));

        IRenderedComponent<CascadingAuthenticationState> cut = RenderHeader(context, "Marathon Petroleum Corporation");

        Assert.AreEqual("MC", cut.Find(".user-avatar").TextContent.Trim());
    }

    [TestMethod]
    public void DisplayDate_UsesOrdinalSuffixRules()
    {
        StubTimeProvider firstProvider = new(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
        using BunitContext firstContext = CreateContext(firstProvider);
        IRenderedComponent<CascadingAuthenticationState> firstCut = RenderHeader(firstContext, "DST User");

        string first = firstCut.Find("time.current-date").TextContent;

        StubTimeProvider eleventhProvider = new(new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero));
        using BunitContext eleventhContext = CreateContext(eleventhProvider);
        IRenderedComponent<CascadingAuthenticationState> eleventhCut = RenderHeader(eleventhContext, "DST User");

        string eleventh = eleventhCut.Find("time.current-date").TextContent;

        StringAssert.Contains(first, "1st");
        StringAssert.Contains(eleventh, "11th");
    }

    [TestMethod]
    public void Dispose_DisposesClockTimer()
    {
        StubTimeProvider timeProvider = new(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));
        using BunitContext context = CreateContext(timeProvider);
        IRenderedComponent<CascadingAuthenticationState> cut = RenderHeader(context, "DST User");

        cut.WaitForAssertion(() => Assert.IsNotNull(timeProvider.LastTimer));

        ApplicationHeader header = cut.FindComponent<ApplicationHeader>().Instance;

        header.Dispose();

        Assert.IsTrue(timeProvider.LastTimer!.Disposed);
    }

    private static BunitContext CreateContext(TimeProvider timeProvider)
    {
        BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton(timeProvider);
        return context;
    }

    private static IRenderedComponent<CascadingAuthenticationState> RenderHeader(
        BunitContext context,
        string displayName)
    {
        BunitAuthorizationContext authContext = context.AddAuthorization();
        authContext.SetAuthorized(displayName);

        return context.Render<CascadingAuthenticationState>(parameters =>
            parameters.AddChildContent<ApplicationHeader>());
    }

    private sealed class StubTimeProvider(DateTimeOffset localNow) : TimeProvider
    {
        private DateTimeOffset _localNow = localNow;

        public StubTimer? LastTimer { get; private set; }

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => _localNow.ToUniversalTime();

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            LastTimer = new StubTimer(callback, state);
            return LastTimer;
        }
    }

    private sealed class StubTimer(TimerCallback callback, object? state) : ITimer
    {
        private readonly TimerCallback _callback = callback;
        private readonly object? _state = state;

        public bool Disposed { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (Disposed)
            {
                return false;
            }

            return true;
        }

        public void Trigger() => _callback(_state);

        public void Dispose()
        {
            Disposed = true;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
