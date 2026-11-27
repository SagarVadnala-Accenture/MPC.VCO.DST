using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application;
using MPC.VCO.DST.Application.Interfaces;
using MPC.VCO.DST.Application.Models;
using MPC.VCO.DST.Domain.Interfaces;
using MPC.VCO.DST.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MPC.VCO.DST.Tests.Application.Implementation;

[TestClass]
public sealed class ModelNoteServiceTests
{
    [TestMethod]
    public async Task GetModelNotesAsync_PageSizeAboveMaximum_ThrowsArgumentOutOfRangeException()
    {
        var repository = new FakeModelNoteRepository();
        IModelNoteService service = CreateService(repository);

        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => service.GetModelNotesAsync(new GetModelNotesRequest
            {
                ModelId = "model-123",
                PageSize = 101,
                ContinuationToken = null
            }, CancellationToken.None));
    }

    [TestMethod]
    public async Task GetModelNotesAsync_PageSizeNotSpecified_UsesDefaultPageSize()
    {
        var repository = new FakeModelNoteRepository();
        IModelNoteService service = CreateService(repository);

        await service.GetModelNotesAsync(new GetModelNotesRequest
        {
            ModelId = "model-123",
            PageSize = 0,
            ContinuationToken = "abc"
        }, CancellationToken.None);

        Assert.AreEqual(20, repository.LastGetPageSize);
        Assert.AreEqual("abc", repository.LastGetContinuationToken);
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_RejectsWhitespaceNote()
    {
        var repository = new FakeModelNoteRepository();
        IModelNoteService service = CreateService(repository);

        var request = new CreateModelNoteRequest
        {
            ModelId = "model-123",
            NoteText = "   ",
            UserId = "user-1",
            DisplayName = "Jane Doe",
            UserPrincipalName = "jane.doe@contoso.com",
            Initials = "JD"
        };

        await AssertThrowsAsync<ArgumentException>(
            () => service.CreateModelNoteAsync(request, CancellationToken.None));
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_RejectsTooLongNote()
    {
        var repository = new FakeModelNoteRepository();
        IModelNoteService service = CreateService(repository);

        var request = new CreateModelNoteRequest
        {
            ModelId = "model-123",
            NoteText = new string('x', 4001),
            UserId = "user-1",
            DisplayName = "Jane Doe",
            UserPrincipalName = "jane.doe@contoso.com",
            Initials = "JD"
        };

        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => service.CreateModelNoteAsync(request, CancellationToken.None));
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_TrimAndGenerateServerFields()
    {
        var repository = new FakeModelNoteRepository();
        IModelNoteService service = CreateService(repository);

        var request = new CreateModelNoteRequest
        {
            ModelId = "model-123",
            NoteText = "  hello note  ",
            UserId = "user-1",
            DisplayName = "Jane Doe",
            UserPrincipalName = "jane.doe@contoso.com",
            Initials = "JD"
        };

        var before = DateTimeOffset.UtcNow;
        var result = await service.CreateModelNoteAsync(request, CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        Assert.AreEqual("hello note", repository.LastCreated!.NoteText);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Id));
        Assert.AreEqual("model-123", result.ModelId);
        Assert.AreEqual("hello note", result.NoteText);
        Assert.AreEqual("user-1", result.Author.UserId);
        Assert.AreEqual("Jane Doe", result.Author.DisplayName);
        Assert.AreEqual("jane.doe@contoso.com", result.Author.UserPrincipalName);
        Assert.AreEqual("JD", result.Author.Initials);
        Assert.IsNull(result.UpdatedAtUtc);
        Assert.IsTrue(result.CreatedAtUtc >= before && result.CreatedAtUtc <= after.AddSeconds(1));
        Assert.IsFalse(repository.LastCreated.IsDeleted);
        Assert.AreEqual(1, repository.LastCreated.SchemaVersion);
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_PropagatesCancellationToken()
    {
        var repository = new FakeModelNoteRepository();
        IModelNoteService service = CreateService(repository);

        using var cts = new CancellationTokenSource();
        CancellationToken token = cts.Token;

        await service.CreateModelNoteAsync(new CreateModelNoteRequest
        {
            ModelId = "model-123",
            NoteText = "hello",
            UserId = "user-1",
            DisplayName = "Jane Doe",
            UserPrincipalName = "jane.doe@contoso.com",
            Initials = "JD"
        }, token);

        Assert.AreEqual(token, repository.CreateToken);
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_ValidMention_AdjustsStartAndMapsDirectoryFields()
    {
        var repository = new FakeModelNoteRepository();
        var mentionDirectory = new FakeMentionUserDirectory
        {
            AuthorizedUsers =
            [
                new MentionUser("u1", "Jane Doe", "jane.doe@contoso.com")
            ]
        };

        IModelNoteService service = CreateService(repository, mentionDirectory);

        await service.CreateModelNoteAsync(new CreateModelNoteRequest
        {
            ModelId = "model-123",
            NoteText = "   hello @Jane Doe",
            UserId = "author-1",
            DisplayName = "Author One",
            UserPrincipalName = "author.one@contoso.com",
            Initials = "AO",
            Mentions =
            [
                new NoteMentionRequest
                {
                    UserId = "u1",
                    Start = 9,
                    Length = 9
                }
            ]
        }, CancellationToken.None);

        Assert.IsNotNull(repository.LastCreated);
        Assert.AreEqual(1, repository.LastCreated.Mentions.Count);
        Assert.AreEqual("u1", repository.LastCreated.Mentions[0].UserId);
        Assert.AreEqual("Jane Doe", repository.LastCreated.Mentions[0].DisplayName);
        Assert.AreEqual("jane.doe@contoso.com", repository.LastCreated.Mentions[0].Email);
        Assert.AreEqual(6, repository.LastCreated.Mentions[0].Start);
        Assert.AreEqual(9, repository.LastCreated.Mentions[0].Length);
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_InvalidMentionRange_ThrowsArgumentException()
    {
        var repository = new FakeModelNoteRepository();
        var mentionDirectory = new FakeMentionUserDirectory
        {
            AuthorizedUsers =
            [
                new MentionUser("u1", "Jane Doe", "jane.doe@contoso.com")
            ]
        };

        IModelNoteService service = CreateService(repository, mentionDirectory);

        await AssertThrowsAsync<ArgumentException>(() => service.CreateModelNoteAsync(new CreateModelNoteRequest
        {
            ModelId = "model-123",
            NoteText = "hello @Jane Doe",
            UserId = "author-1",
            DisplayName = "Author One",
            UserPrincipalName = "author.one@contoso.com",
            Initials = "AO",
            Mentions =
            [
                new NoteMentionRequest
                {
                    UserId = "u1",
                    Start = -1,
                    Length = 9
                }
            ]
        }, CancellationToken.None));
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_UnauthorizedMentionUser_ThrowsArgumentException()
    {
        var repository = new FakeModelNoteRepository();
        var mentionDirectory = new FakeMentionUserDirectory
        {
            AuthorizedUsers = []
        };

        IModelNoteService service = CreateService(repository, mentionDirectory);

        await AssertThrowsAsync<ArgumentException>(() => service.CreateModelNoteAsync(new CreateModelNoteRequest
        {
            ModelId = "model-123",
            NoteText = "hello @Jane Doe",
            UserId = "author-1",
            DisplayName = "Author One",
            UserPrincipalName = "author.one@contoso.com",
            Initials = "AO",
            Mentions =
            [
                new NoteMentionRequest
                {
                    UserId = "u1",
                    Start = 6,
                    Length = 9
                }
            ]
        }, CancellationToken.None));
    }

    [TestMethod]
    public async Task CreateModelNoteAsync_MentionTextMismatch_ThrowsArgumentException()
    {
        var repository = new FakeModelNoteRepository();
        var mentionDirectory = new FakeMentionUserDirectory
        {
            AuthorizedUsers =
            [
                new MentionUser("u1", "Jane Doe", "jane.doe@contoso.com")
            ]
        };

        IModelNoteService service = CreateService(repository, mentionDirectory);

        await AssertThrowsAsync<ArgumentException>(() => service.CreateModelNoteAsync(new CreateModelNoteRequest
        {
            ModelId = "model-123",
            NoteText = "hello @Jane Roe",
            UserId = "author-1",
            DisplayName = "Author One",
            UserPrincipalName = "author.one@contoso.com",
            Initials = "AO",
            Mentions =
            [
                new NoteMentionRequest
                {
                    UserId = "u1",
                    Start = 6,
                    Length = 9
                }
            ]
        }, CancellationToken.None));
    }

    [TestMethod]
    public async Task GetModelNotesAsync_PropagatesCancellationToken()
    {
        var repository = new FakeModelNoteRepository();
        IModelNoteService service = CreateService(repository);

        using var cts = new CancellationTokenSource();
        CancellationToken token = cts.Token;

        await service.GetModelNotesAsync(new GetModelNotesRequest
        {
            ModelId = "model-123",
            PageSize = 20,
            ContinuationToken = "abc"
        }, token);

        Assert.AreEqual(token, repository.GetToken);
        Assert.AreEqual("model-123", repository.LastGetModelId);
    }

    private static IModelNoteService CreateService(
        IModelNoteRepository repository,
        IMentionUserDirectory? mentionUserDirectory = null)
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton(repository);
        services.AddSingleton(mentionUserDirectory ?? new FakeMentionUserDirectory());

        ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IModelNoteService>();
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
            Assert.Fail($"Expected exception {typeof(TException).Name} was not thrown.");
        }
        catch (TException)
        {
        }
    }

    private sealed class FakeMentionUserDirectory : IMentionUserDirectory
    {
        public IReadOnlyList<MentionUser> AuthorizedUsers { get; init; } = [];

        public Task<IReadOnlyList<MentionUser>> SearchAsync(string search, int maxResults, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MentionUser>>([]);

        public Task<IReadOnlyList<MentionUser>> GetAuthorizedUsersAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MentionUser>>(
                AuthorizedUsers
                    .Where(user => userIds.Contains(user.Id, StringComparer.OrdinalIgnoreCase))
                    .ToArray());
    }

    private sealed class FakeModelNoteRepository : IModelNoteRepository
    {
        public ModelNote? LastCreated { get; private set; }
        public CancellationToken CreateToken { get; private set; }
        public CancellationToken GetToken { get; private set; }
        public string? LastGetModelId { get; private set; }
        public int LastGetPageSize { get; private set; }
        public string? LastGetContinuationToken { get; private set; }

        public Task<ModelNote> CreateAsync(ModelNote note, CancellationToken cancellationToken = default)
        {
            LastCreated = note;
            CreateToken = cancellationToken;
            return Task.FromResult(note);
        }

        public Task<ModelNoteQueryResult> GetByModelIdAsync(string modelId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default)
        {
            LastGetModelId = modelId;
            LastGetPageSize = pageSize;
            LastGetContinuationToken = continuationToken;
            GetToken = cancellationToken;

            return Task.FromResult(new ModelNoteQueryResult
            {
                Notes =
                [
                    new ModelNote
                    {
                        Id = "n1",
                        ModelId = modelId,
                        NoteText = "abc",
                        Author = new ModelNoteAuthor
                        {
                            UserId = "u1",
                            DisplayName = "A B",
                            UserPrincipalName = "a@b.com",
                            Initials = "AB"
                        },
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        UpdatedAtUtc = null,
                        IsDeleted = false,
                        SchemaVersion = 1
                    }
                ],
                ContinuationToken = null
            });
        }
    }
}
