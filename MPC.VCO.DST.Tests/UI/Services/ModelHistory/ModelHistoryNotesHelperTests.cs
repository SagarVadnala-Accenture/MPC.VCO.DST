using MPC.VCO.DST.WebSite.Models;
using MPC.VCO.DST.WebSite.Services.ModelHistory;

namespace MPC.VCO.DST.Tests.UI.Services.ModelHistory;

[TestClass]
public sealed class ModelHistoryNotesHelperTests
{
    [TestMethod]
    public void TryBuildCreateRequest_WhitespaceOnly_ReturnsFalse()
    {
        bool success = ModelHistoryNotesHelper.TryBuildCreateRequest(
            "   ",
            [],
            out CreateModelNoteRequest request);

        Assert.IsFalse(success);
        Assert.AreEqual(string.Empty, request.Text);
        Assert.AreEqual(0, request.Mentions.Count);
    }

    [TestMethod]
    public void TryBuildCreateRequest_LeadingWhitespace_ShiftsMentionOffsets()
    {
        IReadOnlyList<NoteMention> mentions =
        [
            new("u-1", "User One", "user1@contoso.com", 2, 8),
            new("u-2", "User Two", "user2@contoso.com", 0, 3)
        ];

        bool success = ModelHistoryNotesHelper.TryBuildCreateRequest(
            "  hello @User One",
            mentions,
            out CreateModelNoteRequest request);

        Assert.IsTrue(success);
        Assert.AreEqual("hello @User One", request.Text);
        Assert.AreEqual(1, request.Mentions.Count);
        Assert.AreEqual(0, request.Mentions[0].Start);
    }

    [TestMethod]
    public void MergePostedNote_PrependsAndRemovesDuplicateByNoteId()
    {
        IReadOnlyList<ModelNote> source =
        [
            new("n-1", "m-1", "User", "user@contoso.com", "old-1", DateTimeOffset.UtcNow.AddMinutes(-2), []),
            new("n-2", "m-1", "User", "user@contoso.com", "old-2", DateTimeOffset.UtcNow.AddMinutes(-3), [])
        ];

        ModelNote posted = new("n-2", "m-1", "User", "user@contoso.com", "new", DateTimeOffset.UtcNow, []);

        IReadOnlyList<ModelNote> merged = ModelHistoryNotesHelper.MergePostedNote(source, posted);

        Assert.AreEqual(2, merged.Count);
        Assert.AreEqual("n-2", merged[0].NoteId);
        Assert.AreEqual("new", merged[0].Text);
        Assert.AreEqual("n-1", merged[1].NoteId);
    }

    [TestMethod]
    public void SortNotes_OrdersByCreatedOnThenNoteIdDescending()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        IReadOnlyList<ModelNote> notes =
        [
            new("n-1", "m-1", "User", "user@contoso.com", "a", now, []),
            new("n-3", "m-1", "User", "user@contoso.com", "b", now, []),
            new("n-2", "m-1", "User", "user@contoso.com", "c", now.AddMinutes(-1), [])
        ];

        IReadOnlyList<ModelNote> sorted = ModelHistoryNotesHelper.SortNotes(notes);

        CollectionAssert.AreEqual(new[] { "n-3", "n-1", "n-2" }, sorted.Select(note => note.NoteId).ToArray());
    }

    [TestMethod]
    public void GetPostNoteErrorMessage_BadRequest_ReturnsSpecificMessage()
    {
        HttpRequestException exception = new(
            "bad request",
            inner: null,
            statusCode: System.Net.HttpStatusCode.BadRequest);

        string message = ModelHistoryNotesHelper.GetPostNoteErrorMessage(exception);

        Assert.AreEqual("Could not post note: the note request is invalid.", message);
    }
}
