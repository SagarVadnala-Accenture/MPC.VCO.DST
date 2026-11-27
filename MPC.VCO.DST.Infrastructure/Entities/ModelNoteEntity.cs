using System.Text.Json.Serialization;

namespace MPC.VCO.DST.Infrastructure.Entities;

internal sealed class ModelNoteEntity
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = string.Empty;

    [JsonPropertyName("noteText")]
    public string NoteText { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public ModelNoteAuthorEntity Author { get; set; } = new();

    [JsonPropertyName("mentions")]
    public List<ModelNoteMentionEntity> Mentions { get; set; } = [];

    [JsonPropertyName("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [JsonPropertyName("updatedAtUtc")]
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; }

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }
}

internal sealed class ModelNoteMentionEntity
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("start")]
    public int Start { get; set; }

    [JsonPropertyName("length")]
    public int Length { get; set; }
}

internal sealed class ModelNoteAuthorEntity
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("userPrincipalName")]
    public string UserPrincipalName { get; set; } = string.Empty;

    [JsonPropertyName("initials")]
    public string Initials { get; set; } = string.Empty;
}
