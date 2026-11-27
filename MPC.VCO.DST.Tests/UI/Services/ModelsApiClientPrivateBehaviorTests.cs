using System.Reflection;
using MPC.VCO.DST.WebSite.Models;
using MPC.VCO.DST.WebSite.Services;

namespace MPC.VCO.DST.Tests.UI.Services;

[TestClass]
public sealed class ModelsApiClientPrivateBehaviorTests
{
    [TestMethod]
    public void MapStatus_ExecutableAndReviewedAndFallback_AreMappedCorrectly()
    {
        Assert.AreEqual(ModelStatus.Executable, InvokeMapStatus(" executable "));
        Assert.AreEqual(ModelStatus.Reviewed, InvokeMapStatus("REVIEWED"));
        Assert.AreEqual(ModelStatus.Draft, InvokeMapStatus("unknown"));
    }

    [TestMethod]
    public void ParsePeriodFromPlanningCycleId_ValidAndInvalidInputs_AreHandled()
    {
        Assert.AreEqual(new DateOnly(2026, 7, 1), InvokeParsePeriod("2026-07"));
        Assert.AreEqual(new DateOnly(2026, 12, 1), InvokeParsePeriod("2026-12-extra"));

        DateOnly fallback = InvokeParsePeriod("invalid");
        DateOnly expectedCurrentMonth = new(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1);
        Assert.AreEqual(expectedCurrentMonth, fallback);
    }

    [TestMethod]
    public void ValidateUpsertRequest_InvalidValues_ThrowExpectedExceptions()
    {
        ModelUpsertRequest valid = new()
        {
            Status = "Draft",
            ScopeType = "Refinery",
            ScopeName = "TX",
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.2m,
            NaturalGasPriceUsdPerMmbtu = 2.3m
        };

        AssertThrows<TargetInvocationException>(() => InvokeValidateUpsert(string.Empty, valid));

        AssertThrows<TargetInvocationException>(() => InvokeValidateUpsert("PC-2026-07", new ModelUpsertRequest
        {
            Status = "",
            ScopeType = "Refinery",
            ScopeName = "TX",
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.2m,
            NaturalGasPriceUsdPerMmbtu = 2.3m
        }));

        AssertThrows<TargetInvocationException>(() => InvokeValidateUpsert("PC-2026-07", new ModelUpsertRequest
        {
            Status = "Draft",
            ScopeType = "Refinery",
            ScopeName = "TX",
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 0m,
            NaturalGasPriceUsdPerMmbtu = 2.3m
        }));
    }

    [TestMethod]
    public void Map_ValidApiResponse_ReturnsExpectedModelRun()
    {
        PlanningCycle cycle = new("PC-2026-07", new DateOnly(2026, 7, 1), 0, null, null, null);

        ModelApiResponse apiModel = new()
        {
            ModelId = "PC-2026-07-001",
            CreatedOn = new DateOnly(2026, 7, 2),
            Status = "reviewed",
            Scope = new ModelScopeApiResponse { Type = "Refinery", Name = "TX" },
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.23m,
            NaturalGasPriceUsdPerMmbtu = 2.34m,
            AverageRecoveryPercent = 66.6m,
            ExpectedValueMonthlyUsd = 219000m
        };

        ModelRun result = InvokeMap(cycle, apiModel);

        Assert.AreEqual("PC-2026-07-001", result.Id);
        Assert.AreEqual(ModelStatus.Reviewed, result.Status);
        Assert.AreEqual(67, result.Recovery);
        Assert.AreEqual(219, result.ExpectedValue);
        Assert.AreEqual("PC-2026-07", result.PlanningCycleId);
    }

    private static ModelStatus InvokeMapStatus(string status)
    {
        MethodInfo method = GetPrivateStaticMethod("MapStatus");
        object? value = method.Invoke(null, [status]);
        return (ModelStatus)value!;
    }

    private static DateOnly InvokeParsePeriod(string planningCycleId)
    {
        MethodInfo method = GetPrivateStaticMethod("ParsePeriodFromPlanningCycleId");
        object? value = method.Invoke(null, [planningCycleId]);
        return (DateOnly)value!;
    }

    private static void InvokeValidateUpsert(string planningCycleId, ModelUpsertRequest request)
    {
        MethodInfo method = GetPrivateStaticMethod("ValidateUpsertRequest");
        method.Invoke(null, [planningCycleId, request]);
    }

    private static ModelRun InvokeMap(PlanningCycle cycle, ModelApiResponse model)
    {
        MethodInfo method = GetPrivateStaticMethod("Map");
        object? value = method.Invoke(null, [cycle, model]);
        return (ModelRun)value!;
    }

    private static MethodInfo GetPrivateStaticMethod(string methodName)
    {
        MethodInfo? method = typeof(ModelsApiClient)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method, $"Method '{methodName}' was not found.");
        return method!;
    }

    private static void AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            Assert.Fail($"Expected exception {typeof(TException).Name} was not thrown.");
        }
        catch (TException)
        {
        }
    }
}
