using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.DTOs.Response;
using MPC.VCO.DST.Application.Interfaces;
using MPC.VCO.DST.Application.Mappers;
using MPC.VCO.DST.Application.Models;
using MPC.VCO.DST.Domain.Interfaces;
using MPC.VCO.DST.Domain.Models;

namespace MPC.VCO.DST.Application.Implementation;

internal sealed class ModelNoteService(
    IModelNoteRepository modelNoteRepository,
    IMentionUserService mentionUserService)
    : IModelNoteService
{
    private sealed record MentionPosition(string UserId, int Start, int Length);

    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const int MaxNoteLength = 4000;

    private readonly IModelNoteRepository _modelNoteRepository =
        modelNoteRepository ?? throw new ArgumentNullException(nameof(modelNoteRepository));

    private readonly IMentionUserService _mentionUserService =
        mentionUserService ?? throw new ArgumentNullException(nameof(mentionUserService));
    public async Task<GetModelNotesResponse>
        GetModelNotesAsync(
            GetModelNotesRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ModelId))
        {
            throw new ArgumentException("ModelId is required.", nameof(request.ModelId));
        }

        int pageSize = request.PageSize <= 0 ? DefaultPageSize : request.PageSize;

        if (pageSize > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(request.PageSize), $"PageSize cannot exceed {MaxPageSize}.");
        }

        ModelNoteQueryResult result = await _modelNoteRepository.GetByModelIdAsync(
            request.ModelId,
            pageSize,
            request.ContinuationToken,
            cancellationToken);

        return new GetModelNotesResponse
        {
            Notes = result.Notes.Select(note => note.ToResponse()).ToArray(),
            ContinuationToken = result.ContinuationToken
        };
    }

    public async Task<ModelNoteResponse>
        CreateModelNoteAsync(
            CreateModelNoteRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ModelId))
        {
            throw new ArgumentException("ModelId is required.", nameof(request.ModelId));
        }

        string suppliedText = request.NoteText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(suppliedText))
        {
            throw new ArgumentException("NoteText is required.", nameof(request));
        }

        string noteText = suppliedText.Trim();

        // Mention positions are based on the original, untrimmed text and must be adjusted
        // after leading whitespace is removed.
        // Example: "   Hi @User" -> "Hi @User".
        // If @User originally starts at index 6, trimming 3 leading spaces moves it to index 3.
        int leadingWhitespaceLength = suppliedText.Length - suppliedText.TrimStart().Length;

        if (noteText.Length > MaxNoteLength)
        {
            throw new ArgumentOutOfRangeException(nameof(request), $"NoteText cannot exceed {MaxNoteLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(request.UserId)
            || string.IsNullOrWhiteSpace(request.DisplayName)
            || string.IsNullOrWhiteSpace(request.UserPrincipalName)
            || string.IsNullOrWhiteSpace(request.Initials))
        {
            throw new ArgumentException("Authenticated user information is required.", nameof(request));
        }

        // TODO: enforce model-level authorization and model-existence checks when a dedicated authorization/existence service is available at the application boundary.

        NoteMention[] mentions = await ValidateAndNormalizeMentionsAsync(
            noteText,
            leadingWhitespaceLength,
            request.Mentions ?? [],
            cancellationToken);

        ModelNote note = new()
        {
            Id = Guid.NewGuid().ToString(),
            ModelId = request.ModelId,
            NoteText = noteText,
            Author = new ModelNoteAuthor
            {
                UserId = request.UserId,
                DisplayName = request.DisplayName,
                UserPrincipalName = request.UserPrincipalName,
                Initials = request.Initials
            },
            Mentions = mentions,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = null,
            IsDeleted = false,
            SchemaVersion = 1
        };

        ModelNote created = await _modelNoteRepository.CreateAsync(note, cancellationToken);

        return created.ToResponse();
    }

    private async Task<NoteMention[]> ValidateAndNormalizeMentionsAsync(
        string noteText,
        int leadingWhitespaceLength,
        IReadOnlyList<NoteMentionRequest> requestedMentions,
        CancellationToken cancellationToken)
    {
        if (requestedMentions.Count == 0)
        {
            return [];
        }

        MentionPosition[] positions = requestedMentions
            .Select(mention => new MentionPosition(
                mention.UserId,
                mention.Start - leadingWhitespaceLength,
                mention.Length))
            .OrderBy(mention => mention.Start)
            .ToArray();

        int previousEnd = 0;
        foreach (MentionPosition position in positions)
        {
            if (string.IsNullOrWhiteSpace(position.UserId)
                || position.Start < previousEnd
                || position.Start < 0
                || position.Length <= 0
                || position.Start > noteText.Length - position.Length)
            {
                throw new ArgumentException("Mention positions are invalid.", nameof(requestedMentions));
            }

            previousEnd = position.Start + position.Length;
        }

        string[] mentionedUserIds = positions
            .Select(position => position.UserId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        IReadOnlyList<MentionUser> authorizedUsers = await _mentionUserService.GetAuthorizedUsersAsync(
            mentionedUserIds,
            cancellationToken);
        Dictionary<string, MentionUser> usersById = authorizedUsers.ToDictionary(
            user => user.Id,
            StringComparer.OrdinalIgnoreCase);

        if (positions.Any(position => !usersById.ContainsKey(position.UserId)))
        {
            throw new ArgumentException("One or more mentioned users are not eligible to access this application.", nameof(requestedMentions));
        }

        List<NoteMention> mentions = [];
        foreach (MentionPosition position in positions)
        {
            MentionUser user = usersById[position.UserId];
            string expectedMention = $"@{user.DisplayName}";
            if (position.Length != expectedMention.Length
                || !string.Equals(
                    noteText.Substring(position.Start, position.Length),
                    expectedMention,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException("Mention text does not match the selected directory user.", nameof(requestedMentions));
            }

            mentions.Add(new NoteMention
            {
                UserId = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email,
                Start = position.Start,
                Length = position.Length
            });
        }

        return mentions.ToArray();
    }

}
