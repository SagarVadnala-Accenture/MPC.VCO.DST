using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using Bunit.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using MPC.VCO.DST.WebSite.Components.Pages.Shared;
using MPC.VCO.DST.WebSite.Models;
using MPC.VCO.DST.WebSite.Services;
using MPC.VCO.DST.WebSite.Services.ModelNotes;

namespace MPC.VCO.DST.Tests.UI.Components.Pages.Shared;

[TestClass]
public sealed class ModelNotesDrawerTests
{
    [TestMethod]
    public async Task Render_WithModelAndNotes_DisplaysSummaryAndMentionHighlight()
    {
        await using BunitContext context = CreateContext((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<MentionUserApiResponse>())
            }));

        ModelRun model = CreateModel();
        ModelNote note = new(
            "n-1",
            "M-001",
            "Jane Doe",
            "jane@contoso.com",
            "Hello @Jane Doe",
            DateTimeOffset.UtcNow,
            [new NoteMention("u-1", "Jane Doe", "jane@contoso.com", 6, 9)]);

        IRenderedComponent<ModelNotesDrawer> cut = RenderDrawer(
            context,
            open: true,
            model: model,
            notes: [note]);

        StringAssert.Contains(cut.Markup, "Model Notes");
        StringAssert.Contains(cut.Markup, "M-001");
        Assert.AreEqual(1, cut.FindAll(".note-mention").Count);
    }

    [TestMethod]
    public async Task Render_LoadingNotes_ShowsLoadingState()
    {
        await using BunitContext context = CreateContext((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        IRenderedComponent<ModelNotesDrawer> cut = RenderDrawer(
            context,
            open: true,
            model: CreateModel(),
            loading: true);

        StringAssert.Contains(cut.Markup, "Loading notes");
    }

    [TestMethod]
    public async Task Render_EmptyNotes_ShowsNoNotesMessage()
    {
        await using BunitContext context = CreateContext((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        IRenderedComponent<ModelNotesDrawer> cut = RenderDrawer(
            context,
            open: true,
            model: CreateModel(),
            notes: []);

        StringAssert.Contains(cut.Markup, "No notes yet.");
    }

    [TestMethod]
    public async Task Render_LoadingError_ShowsRetryAndInvokesRetryRequested()
    {
        await using BunitContext context = CreateContext((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        int retryCalls = 0;
        IRenderedComponent<ModelNotesDrawer> cut = RenderDrawer(
            context,
            open: true,
            model: CreateModel(),
            loadingError: true,
            retryRequested: EventCallback.Factory.Create(this, () => retryCalls++));

        cut.Find(".state-message button").Click();

        Assert.AreEqual(1, retryCalls);
    }

    [TestMethod]
    public async Task CloseButton_Click_RaisesOpenChangedFalse()
    {
        await using BunitContext context = CreateContext((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        bool? changed = null;
        IRenderedComponent<ModelNotesDrawer> cut = RenderDrawer(
            context,
            open: true,
            model: CreateModel(),
            openChanged: EventCallback.Factory.Create<bool>(this, value => changed = value));

        cut.Find("button[aria-label='Close model notes']").Click();

        Assert.AreEqual(false, changed);
    }

    [TestMethod]
    public async Task EscapeKey_RaisesOpenChangedFalse()
    {
        await using BunitContext context = CreateContext((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        bool? changed = null;
        IRenderedComponent<ModelNotesDrawer> cut = RenderDrawer(
            context,
            open: true,
            model: CreateModel(),
            openChanged: EventCallback.Factory.Create<bool>(this, value => changed = value));

        cut.Find("aside.notes-panel").KeyDown("Escape");

        Assert.AreEqual(false, changed);
    }

    [TestMethod]
    public async Task PostButton_WithWhitespaceDraft_IsDisabled()
    {
        await using BunitContext context = CreateContext((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        IRenderedComponent<ModelNotesDrawer> cut = RenderDrawer(
            context,
            open: true,
            model: CreateModel(),
            noteDraft: "   ");

        IElement button = cut.Find("button.post-note");
        Assert.IsTrue(button.HasAttribute("disabled"));
    }

    [TestMethod]
    public async Task PostButton_WithValidDraft_InvokesPostRequested()
    {
        await using BunitContext context = CreateContext((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        int postCalls = 0;
        IRenderedComponent<ModelNotesDrawer> cut = RenderDrawer(
            context,
            open: true,
            model: CreateModel(),
            noteDraft: "Ready to post",
            postRequested: EventCallback.Factory.Create(this, () => postCalls++));

        cut.Find("button.post-note").Click();

        Assert.AreEqual(1, postCalls);
    }

    [TestMethod]
    public async Task PostButton_WhenPosting_IsDisabled()
    {
        await using BunitContext context = CreateContext((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        IRenderedComponent<ModelNotesDrawer> cut = RenderDrawer(
            context,
            open: true,
            model: CreateModel(),
            noteDraft: "note",
            posting: true);

        IElement button = cut.Find("button.post-note");
        Assert.IsTrue(button.HasAttribute("disabled"));
    }

    [TestMethod]
    public async Task MentionSearch_WithResults_ShowsSuggestionsAndSelectionUpdatesDraftAndMentions()
    {
        await using BunitContext context = CreateContext((request, _) =>
        {
            StringAssert.Contains(request.RequestUri!.ToString(), "api/users/mentions");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[]
                {
                    new MentionUserApiResponse
                    {
                        Id = "u-1",
                        DisplayName = "Jane Doe",
                        Email = "jane@contoso.com"
                    }
                })
            });
        });

        context.JSInterop.Setup<int>("modelHistory.getNoteCaret", _ => true).SetResult(7);

        string? changedDraft = null;
        IReadOnlyList<NoteMention>? changedMentions = null;
        IRenderedComponent<ModelNotesDrawer> cut = RenderDrawer(
            context,
            open: true,
            model: CreateModel(),
            noteDraft: "Hello @",
            noteDraftChanged: EventCallback.Factory.Create<string>(this, value => changedDraft = value),
            mentionsChanged: EventCallback.Factory.Create<IReadOnlyList<NoteMention>>(this, value => changedMentions = value));

        cut.Find("textarea.note-input").Input("Hello @");
        cut.WaitForAssertion(() => Assert.AreEqual(1, cut.FindAll("button.mention-option").Count));

        await InvokePrivateTask(
            cut.Instance,
            "SelectMentionAsync",
            new MentionUser("u-1", "Jane Doe", "jane@contoso.com"));

        Assert.AreEqual("Hello @Jane Doe ", changedDraft);
        Assert.IsNotNull(changedMentions);
        Assert.AreEqual(1, changedMentions.Count);
        Assert.AreEqual("u-1", changedMentions[0].UserId);
    }

    [TestMethod]
    public async Task MentionSearch_EmptyResult_ShowsNoGroupMembersFound()
    {
        await using BunitContext context = CreateContext((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Array.Empty<MentionUserApiResponse>())
            }));

        context.JSInterop.Setup<int>("modelHistory.getNoteCaret", _ => true).SetResult(7);

        IRenderedComponent<ModelNotesDrawer> cut = RenderDrawer(
            context,
            open: true,
            model: CreateModel(),
            noteDraft: "Hello @");

        cut.Find("textarea.note-input").Input("Hello @");

        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "No group members found."));
    }

    [TestMethod]
    public async Task MentionSearch_HttpFailure_ShowsErrorAndRetryPath()
    {
        int callCount = 0;
        await using BunitContext context = CreateContext((_, _) =>
        {
            callCount++;
            if (callCount == 1)
            {
                throw new HttpRequestException("network");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[]
                {
                    new MentionUserApiResponse { Id = "u-1", DisplayName = "Jane Doe", Email = "jane@contoso.com" }
                })
            });
        });

        context.JSInterop.Setup<int>("modelHistory.getNoteCaret", _ => true).SetResult(7);

        IRenderedComponent<ModelNotesDrawer> cut = RenderDrawer(
            context,
            open: true,
            model: CreateModel(),
            noteDraft: "Hello @");

        cut.Find("textarea.note-input").Input("Hello @");
        cut.WaitForAssertion(() => StringAssert.Contains(cut.Markup, "Could not load group members."));

        cut.Find(".mention-error button").Click();
        cut.WaitForAssertion(() => Assert.AreEqual(1, cut.FindAll("button.mention-option").Count));
    }

    [TestMethod]
    public async Task MentionSearch_CanceledToken_DoesNotSetMentionError()
    {
        await using BunitContext context = CreateContext((_, cancellationToken) =>
            Task.FromCanceled<HttpResponseMessage>(cancellationToken));

        IRenderedComponent<ModelNotesDrawer> cut = RenderDrawer(
            context,
            open: true,
            model: CreateModel());

        CancellationTokenSource source = new();
        await source.CancelAsync();

        await InvokePrivateTask(cut.Instance, "SearchMentionUsersAsync", "query", source.Token, false);

        Assert.IsFalse(GetPrivateField<bool>(cut.Instance, "_mentionError"));
    }

    private static BunitContext CreateContext(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        BunitContext context = new();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();

        ClaimsPrincipal user = new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-1")
        ], "Test"));

        AuthenticationStateProvider authProvider = new StubAuthenticationStateProvider(user);
        NavigationManager navigationManager = new TestNavigationManager();
        DstApiAuthenticatedRequestSender requestSender = new(authProvider, navigationManager);
        HttpClient httpClient = new(new RecordingHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("https://api.local/")
        };

        ModelsApiClient apiClient = new(httpClient, requestSender, NullLogger<ModelsApiClient>.Instance);
        context.Services.AddSingleton(apiClient);
        context.Services.AddSingleton<IMentionUserLookupService>(new MentionUserLookupService(apiClient));

        return context;
    }

    private static IRenderedComponent<ModelNotesDrawer> RenderDrawer(
        BunitContext context,
        bool open,
        ModelRun? model,
        IReadOnlyList<ModelNote>? notes = null,
        bool loading = false,
        bool loadingError = false,
        string noteDraft = "",
        bool posting = false,
        EventCallback<bool>? openChanged = null,
        EventCallback? retryRequested = null,
        EventCallback? postRequested = null,
        EventCallback<string>? noteDraftChanged = null,
        EventCallback<IReadOnlyList<NoteMention>>? mentionsChanged = null)
    {
        return context.Render<ModelNotesDrawer>(parameters => parameters
            .Add(component => component.Open, open)
            .Add(component => component.OpenChanged, openChanged ?? EventCallback.Factory.Create<bool>(context, _ => { }))
            .Add(component => component.Model, model)
            .Add(component => component.Loading, loading)
            .Add(component => component.LoadingError, loadingError)
            .Add(component => component.Notes, notes ?? [])
            .Add(component => component.NoteDraft, noteDraft)
            .Add(component => component.Mentions, (IReadOnlyList<NoteMention>)[])
            .Add(component => component.Posting, posting)
            .Add(component => component.RetryRequested, retryRequested ?? EventCallback.Factory.Create(context, () => { }))
            .Add(component => component.PostRequested, postRequested ?? EventCallback.Factory.Create(context, () => { }))
            .Add(component => component.NoteDraftChanged, noteDraftChanged ?? EventCallback.Factory.Create<string>(context, _ => { }))
            .Add(component => component.MentionsChanged, mentionsChanged ?? EventCallback.Factory.Create<IReadOnlyList<NoteMention>>(context, _ => { })));
    }

    private static ModelRun CreateModel() =>
        new(
            "M-001",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 10),
            ModelStatus.Reviewed,
            "Refinery",
            "TX",
            "Type-A",
            1.1m,
            2.2m,
            66,
            219,
            "PC-2026-07");

    private static async Task InvokePrivateTask(object instance, string methodName, params object?[] args)
    {
        MethodInfo method = GetMethod(instance.GetType(), methodName);
        Task task = (Task)method.Invoke(instance, args)!;
        await task;
    }

    private static MethodInfo GetMethod(Type type, string methodName)
    {
        MethodInfo? method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Method '{methodName}' was not found.");
        return method!;
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        FieldInfo? field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
        return (T)field!.GetValue(instance)!;
    }

    private sealed class StubAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal _user;

        public StubAuthenticationStateProvider(ClaimsPrincipal user)
        {
            _user = user;
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(_user));
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _handler(request, cancellationToken);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager()
        {
            Initialize("https://app.local/", "https://app.local/");
        }

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }
}
