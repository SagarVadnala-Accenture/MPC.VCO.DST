namespace MPC.VCO.DST.Tests.UI.Models;

[TestClass]
public sealed class ModelNotesUiSourceTests
{
    [TestMethod]
    public void ExpectedValueText_WithValue_ReturnsFormattedText()
    {
        var model = new MPC.VCO.DST.WebSite.Models.ModelRun(
            "M-001",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 1),
            MPC.VCO.DST.WebSite.Models.ModelStatus.Draft,
            "Refinery",
            "Texas",
            "Model A",
            1.23m,
            2.34m,
            72,
            219,
            "PC-2026-07");

        Assert.AreEqual("+$219K/mo", model.ExpectedValueText);
    }

    [TestMethod]
    public void RecoveryText_WithNullRecovery_ReturnsDash()
    {
        var model = new MPC.VCO.DST.WebSite.Models.ModelRun(
            "M-002",
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 1),
            MPC.VCO.DST.WebSite.Models.ModelStatus.Reviewed,
            "Plant",
            "Ohio",
            "Model B",
            1.10m,
            2.20m,
            null,
            null,
            "PC-2026-07");

        Assert.AreEqual("-", model.RecoveryText);
    }
}
