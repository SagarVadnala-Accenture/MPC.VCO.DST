using MPC.VCO.DST.WebSite.Services.Optimizer;

namespace MPC.VCO.DST.Tests.UI.State;

[TestClass]
public sealed class OptimizerScenarioStateStoreTests
{
    [TestMethod]
    public void GetVersions_FirstCall_ReturnsFiveDefaultVersions()
    {
        OptimizerScenarioStateStore store = new();

        OptimizerScenarioState[] versions = store.GetVersions("MODEL-1");

        Assert.AreEqual(5, versions.Length);
        Assert.AreEqual(30, versions[0].Parameters.HorizonDays);
    }

    [TestMethod]
    public void GetVersions_SameModelId_ReturnsSameStateArrayInstance()
    {
        OptimizerScenarioStateStore store = new();

        OptimizerScenarioState[] first = store.GetVersions("MODEL-1");
        first[0].Parameters.HorizonDays = 45;

        OptimizerScenarioState[] second = store.GetVersions("MODEL-1");

        Assert.AreSame(first, second);
        Assert.AreEqual(45, second[0].Parameters.HorizonDays);
    }

    [TestMethod]
    public void GetVersions_DifferentModelIds_ReturnIndependentStateArrays()
    {
        OptimizerScenarioStateStore store = new();

        OptimizerScenarioState[] first = store.GetVersions("MODEL-1");
        first[0].Parameters.HorizonDays = 45;

        OptimizerScenarioState[] second = store.GetVersions("MODEL-2");

        Assert.AreNotSame(first, second);
        Assert.AreEqual(30, second[0].Parameters.HorizonDays);
    }
}
