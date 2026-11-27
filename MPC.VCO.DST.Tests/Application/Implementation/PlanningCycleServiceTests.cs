using MPC.VCO.DST.Application;
using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.DTOs.Response;
using MPC.VCO.DST.Application.Interfaces;
using MPC.VCO.DST.Domain.Interfaces;
using MPC.VCO.DST.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace MPC.VCO.DST.Tests.Application.Implementation;

[TestClass]
public sealed class PlanningCycleServiceTests
{
    [TestMethod]
    public async Task GetPlanningCyclesAsync_MonthOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        Mock<IPlanningCycleRepository> repository = new();
        IPlanningCycleService service = CreateService(repository.Object);

        await AssertThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetPlanningCyclesAsync(new GetPlanningCyclesRequest
            {
                Year = 2026,
                Month = 13
            }));

        repository.Verify(
            x => x.GetPlanningCyclesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task GetPlanningCyclesAsync_ValidRequest_MapsRepositoryResults()
    {
        Mock<IPlanningCycleRepository> repository = new();
        repository
            .Setup(x => x.GetPlanningCyclesAsync(2026, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlanningCycle
                {
                    PlanningCycleId = "PC-2026-07",
                    Year = 2026,
                    Month = 7,
                    Name = "July, 2026",
                    CycleState = "Current",
                    BestModel = new BestModel
                    {
                        ModelId = "PC-2026-07-001",
                        ExpectedValueMonthlyUsd = 120000m
                    }
                }
            ]);

        IPlanningCycleService service = CreateService(repository.Object);

        var result = await service.GetPlanningCyclesAsync(new GetPlanningCyclesRequest
        {
            Year = 2026,
            Month = 7
        });

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("PC-2026-07", result[0].PlanningCycleId);
        Assert.IsNotNull(result[0].BestModel);
        Assert.AreEqual("PC-2026-07-001", result[0].BestModel!.ModelId);
        Assert.AreEqual(120000m, result[0].BestModel!.ExpectedValueMonthlyUsd);
    }

    [TestMethod]
    public async Task GetPlanningCyclesAsync_ValidRequest_PropagatesCancellationToken()
    {
        Mock<IPlanningCycleRepository> repository = new();
        CancellationToken captured = CancellationToken.None;

        repository
            .Setup(x => x.GetPlanningCyclesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<int, int, CancellationToken>((_, _, token) => captured = token)
            .ReturnsAsync([]);

        IPlanningCycleService service = CreateService(repository.Object);

        using CancellationTokenSource cts = new();
        await service.GetPlanningCyclesAsync(new GetPlanningCyclesRequest
        {
            Year = 2026,
            Month = 1
        }, cts.Token);

        Assert.AreEqual(cts.Token, captured);
    }

    private static IPlanningCycleService CreateService(IPlanningCycleRepository repository)
    {
        ServiceCollection services = new();
        services.AddApplication();
        services.AddSingleton(repository);

        ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IPlanningCycleService>();
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
}
