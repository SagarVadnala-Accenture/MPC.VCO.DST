using MPC.VCO.DST.Domain.Models;

namespace MPC.VCO.DST.Tests.Domain.Models;

[TestClass]
public sealed class ModelNoteDefaultsTests
{
    [TestMethod]
    public void ModelNote_DefaultInitialization_ProvidesSafeNonNullValues()
    {
        ModelNote note = new();

        Assert.AreEqual(string.Empty, note.Id);
        Assert.AreEqual(string.Empty, note.ModelId);
        Assert.AreEqual(string.Empty, note.NoteText);
        Assert.IsNotNull(note.Author);
        Assert.IsNotNull(note.Mentions);
        Assert.AreEqual(0, note.Mentions.Count);
        Assert.IsFalse(note.IsDeleted);
        Assert.AreEqual(0, note.SchemaVersion);
    }

    [TestMethod]
    public void ModelNoteQueryResult_DefaultInitialization_ProvidesEmptyNotesCollection()
    {
        ModelNoteQueryResult queryResult = new();

        Assert.IsNotNull(queryResult.Notes);
        Assert.AreEqual(0, queryResult.Notes.Count);
        Assert.IsNull(queryResult.ContinuationToken);
    }
}
