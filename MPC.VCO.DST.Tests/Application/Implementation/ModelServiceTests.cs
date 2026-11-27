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
public sealed class ModelServiceTests
{
    [TestMethod]
    public async Task GetModelsAsync_ValidPagination_ReturnsRequestedPage()
    {
        Mock<IModelRepository> repository = new();
        repository
            .Setup(x => x.GetByPlanningCycleAsync("PC-2026-07", 2, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                CreateModel("PC-2026-07-003")
            ]);

        IModelService service = CreateService(repository.Object);

        IReadOnlyList<ModelResponse> result = await service.GetModelsAsync(new GetModelsRequest
        {
            PlanningCycleId = "PC-2026-07",
            PageNumber = 2,
            PageSize = 2
        });

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("PC-2026-07-003", result[0].ModelId);
        repository.Verify(
            x => x.GetByPlanningCycleAsync("PC-2026-07", 2, 2, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task GetModelsAsync_MissingPlanningCycleId_ThrowsArgumentException()
    {
        Mock<IModelRepository> repository = new();
        IModelService service = CreateService(repository.Object);

        await AssertThrowsAsync<ArgumentException>(() => service.GetModelsAsync(new GetModelsRequest
        {
            PlanningCycleId = " ",
            PageNumber = 1,
            PageSize = 10
        }));
    }

    [TestMethod]
    public async Task GetModelsAsync_InvalidPageNumber_ThrowsArgumentOutOfRangeException()
    {
        Mock<IModelRepository> repository = new();
        IModelService service = CreateService(repository.Object);

        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => service.GetModelsAsync(new GetModelsRequest
        {
            PlanningCycleId = "PC-2026-07",
            PageNumber = 0,
            PageSize = 10
        }));
    }

    [TestMethod]
    public async Task GetModelsAsync_InvalidPageSize_ThrowsArgumentOutOfRangeException()
    {
        Mock<IModelRepository> repository = new();
        IModelService service = CreateService(repository.Object);

        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => service.GetModelsAsync(new GetModelsRequest
        {
            PlanningCycleId = "PC-2026-07",
            PageNumber = 1,
            PageSize = 0
        }));
    }

    [TestMethod]
    public async Task GetModelAsync_ExistingModel_ReturnsMappedResponse()
    {
        Mock<IModelRepository> repository = new();
        CancellationToken capturedToken = CancellationToken.None;

        repository
            .Setup(x => x.GetByIdAsync("PC-2026-07", "PC-2026-07-001", It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, _, token) => capturedToken = token)
            .ReturnsAsync(new DstModel
            {
                ModelId = "PC-2026-07-001",
                PlanningCycleId = "PC-2026-07",
                CreatedOn = new DateOnly(2026, 7, 1),
                Status = "Reviewed"
            });

        IModelService service = CreateService(repository.Object);

        using CancellationTokenSource cts = new();
        ModelResponse? result = await service.GetModelAsync("PC-2026-07", "PC-2026-07-001", cts.Token);

        Assert.IsNotNull(result);
        Assert.AreEqual("PC-2026-07-001", result.ModelId);
        Assert.AreEqual("Reviewed", result.Status);
        Assert.AreEqual(cts.Token, capturedToken);
    }

    [TestMethod]
    public async Task GetModelAsync_InvalidPlanningCycleId_ThrowsArgumentException()
    {
        Mock<IModelRepository> repository = new();
        IModelService service = CreateService(repository.Object);

        await AssertThrowsAsync<ArgumentException>(
            () => service.GetModelAsync(string.Empty, "PC-2026-07-001", CancellationToken.None));
    }

    [TestMethod]
    public async Task GetModelAsync_InvalidModelId_ThrowsArgumentException()
    {
        Mock<IModelRepository> repository = new();
        IModelService service = CreateService(repository.Object);

        await AssertThrowsAsync<ArgumentException>(
            () => service.GetModelAsync("PC-2026-07", "   ", CancellationToken.None));
    }

    [TestMethod]
    public async Task CreateModelAsync_ValidRequest_GeneratesNextModelIdAndCallsRepository()
    {
        Mock<IModelRepository> repository = new();
        DstModel? capturedModel = null;

        repository
            .Setup(x => x.ReserveNextModelIdAsync("PC-2026-07", It.IsAny<CancellationToken>()))
            .ReturnsAsync("PC-2026-07-011");

        repository
            .Setup(x => x.CreateAsync(It.IsAny<DstModel>(), It.IsAny<CancellationToken>()))
            .Callback<DstModel, CancellationToken>((model, _) => capturedModel = model)
            .ReturnsAsync((DstModel model, CancellationToken _) => model);

        IModelService service = CreateService(repository.Object);

        ModelResponse result = await service.CreateModelAsync(new CreateModelRequest
        {
            PlanningCycleId = "PC-2026-07",
            Status = " Draft ",
            Scope = new ModelScopeRequest
            {
                Type = " Refinery ",
                Name = " Galveston "
            },
            ModelType = " Type-A ",
            EthanePriceUsdPerGal = 1.20m,
            NaturalGasPriceUsdPerMmbtu = 2.30m
        });

        Assert.IsNotNull(capturedModel);
        Assert.AreEqual("PC-2026-07-011", capturedModel.ModelId);
        Assert.AreEqual("Draft", capturedModel.Status);
        Assert.AreEqual("Refinery", capturedModel.Scope.Type);
        Assert.AreEqual("Galveston", capturedModel.Scope.Name);
        Assert.AreEqual("Type-A", capturedModel.ModelType);
        Assert.AreEqual("PC-2026-07-011", result.ModelId);

        repository.Verify(x => x.CreateAsync(It.IsAny<DstModel>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task CreateModelAsync_InvalidStatus_ThrowsArgumentException()
    {
        Mock<IModelRepository> repository = new();
        IModelService service = CreateService(repository.Object);

        await AssertThrowsAsync<ArgumentException>(() => service.CreateModelAsync(new CreateModelRequest
        {
            PlanningCycleId = "PC-2026-07",
            Status = "Invalid",
            Scope = new ModelScopeRequest { Type = "Scope", Name = "Name" },
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.2m,
            NaturalGasPriceUsdPerMmbtu = 2.3m
        }));

        repository.Verify(
            x => x.ReserveNextModelIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task CreateModelAsync_MissingPlanningCycleId_ThrowsArgumentException()
    {
        Mock<IModelRepository> repository = new();
        IModelService service = CreateService(repository.Object);

        await AssertThrowsAsync<ArgumentException>(() => service.CreateModelAsync(new CreateModelRequest
        {
            PlanningCycleId = "",
            Status = "Draft",
            Scope = new ModelScopeRequest { Type = "Scope", Name = "Name" },
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.2m,
            NaturalGasPriceUsdPerMmbtu = 2.3m
        }));
    }

    [TestMethod]
    public async Task CreateModelAsync_MissingScope_ThrowsArgumentException()
    {
        Mock<IModelRepository> repository = new();
        IModelService service = CreateService(repository.Object);

        await AssertThrowsAsync<ArgumentException>(() => service.CreateModelAsync(new CreateModelRequest
        {
            PlanningCycleId = "PC-2026-07",
            Status = "Draft",
            Scope = null!,
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.2m,
            NaturalGasPriceUsdPerMmbtu = 2.3m
        }));
    }

    [TestMethod]
    public async Task CreateModelAsync_MissingModelType_ThrowsArgumentException()
    {
        Mock<IModelRepository> repository = new();
        IModelService service = CreateService(repository.Object);

        await AssertThrowsAsync<ArgumentException>(() => service.CreateModelAsync(new CreateModelRequest
        {
            PlanningCycleId = "PC-2026-07",
            Status = "Draft",
            Scope = new ModelScopeRequest { Type = "Scope", Name = "Name" },
            ModelType = " ",
            EthanePriceUsdPerGal = 1.2m,
            NaturalGasPriceUsdPerMmbtu = 2.3m
        }));
    }

    [TestMethod]
    public async Task CreateModelAsync_InvalidPrice_ThrowsArgumentOutOfRangeException()
    {
        Mock<IModelRepository> repository = new();
        IModelService service = CreateService(repository.Object);

        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => service.CreateModelAsync(new CreateModelRequest
        {
            PlanningCycleId = "PC-2026-07",
            Status = "Draft",
            Scope = new ModelScopeRequest { Type = "Scope", Name = "Name" },
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 0m,
            NaturalGasPriceUsdPerMmbtu = 2.3m
        }));
    }

    [TestMethod]
    public async Task CreateModelAsync_InvalidNaturalGasPrice_ThrowsArgumentOutOfRangeException()
    {
        Mock<IModelRepository> repository = new();
        IModelService service = CreateService(repository.Object);

        await AssertThrowsAsync<ArgumentOutOfRangeException>(() => service.CreateModelAsync(new CreateModelRequest
        {
            PlanningCycleId = "PC-2026-07",
            Status = "Draft",
            Scope = new ModelScopeRequest { Type = "Scope", Name = "Name" },
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.2m,
            NaturalGasPriceUsdPerMmbtu = 0m
        }));
    }

    [TestMethod]
    public async Task UpdateModelAsync_MissingExistingModel_ReturnsNull()
    {
        Mock<IModelRepository> repository = new();
        repository
            .Setup(x => x.GetByIdAsync("PC-2026-07", "PC-2026-07-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DstModel?)null);

        IModelService service = CreateService(repository.Object);

        var result = await service.UpdateModelAsync(new UpdateModelRequest
        {
            PlanningCycleId = "PC-2026-07",
            ModelId = "PC-2026-07-001",
            Status = "Draft",
            Scope = new ModelScopeRequest { Type = "Scope", Name = "Name" },
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.2m,
            NaturalGasPriceUsdPerMmbtu = 2.3m
        });

        Assert.IsNull(result);
        repository.Verify(x => x.UpdateAsync(It.IsAny<DstModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task UpdateModelAsync_ValidRequest_UpdatesModelAndPropagatesToken()
    {
        Mock<IModelRepository> repository = new();
        DstModel? updatedCaptured = null;
        CancellationToken capturedToken = CancellationToken.None;

        DstModel existing = new()
        {
            ModelId = "PC-2026-07-001",
            PlanningCycleId = "PC-2026-07",
            Status = "Draft",
            Scope = new ModelScope { Type = "Scope", Name = "Name" },
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.2m,
            NaturalGasPriceUsdPerMmbtu = 2.3m,
            ExpectedValueMonthlyUsd = 220000m,
            CreatedOn = new DateOnly(2026, 7, 4)
        };

        repository
            .Setup(x => x.GetByIdAsync("PC-2026-07", "PC-2026-07-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        repository
            .Setup(x => x.UpdateAsync(It.IsAny<DstModel>(), It.IsAny<CancellationToken>()))
            .Callback<DstModel, CancellationToken>((model, token) =>
            {
                updatedCaptured = model;
                capturedToken = token;
            })
            .ReturnsAsync((DstModel model, CancellationToken _) => model);

        IModelService service = CreateService(repository.Object);

        using CancellationTokenSource cts = new();
        ModelResponse? result = await service.UpdateModelAsync(new UpdateModelRequest
        {
            PlanningCycleId = "PC-2026-07",
            ModelId = "PC-2026-07-001",
            Status = " Reviewed ",
            Scope = new ModelScopeRequest { Type = " Refinery ", Name = " TX " },
            ModelType = " Type-B ",
            EthanePriceUsdPerGal = 1.9m,
            NaturalGasPriceUsdPerMmbtu = 3.1m
        }, cts.Token);

        Assert.IsNotNull(result);
        Assert.IsNotNull(updatedCaptured);
        Assert.AreEqual("PC-2026-07-001", updatedCaptured.ModelId);
        Assert.AreEqual("PC-2026-07", updatedCaptured.PlanningCycleId);
        Assert.AreEqual(existing.CreatedOn, updatedCaptured.CreatedOn);
        Assert.AreEqual(existing.ExpectedValueMonthlyUsd, updatedCaptured.ExpectedValueMonthlyUsd);
        Assert.AreEqual("Reviewed", updatedCaptured.Status);
        Assert.AreEqual("Refinery", updatedCaptured.Scope.Type);
        Assert.AreEqual("TX", updatedCaptured.Scope.Name);
        Assert.AreEqual("Type-B", updatedCaptured.ModelType);
        Assert.AreEqual(cts.Token, capturedToken);
    }

    [TestMethod]
    public async Task UpdateModelAsync_RepositoryReturnsNull_ReturnsNull()
    {
        Mock<IModelRepository> repository = new();

        repository
            .Setup(x => x.GetByIdAsync("PC-2026-07", "PC-2026-07-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateModel("PC-2026-07-001"));

        repository
            .Setup(x => x.UpdateAsync(It.IsAny<DstModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DstModel?)null);

        IModelService service = CreateService(repository.Object);

        ModelResponse? result = await service.UpdateModelAsync(new UpdateModelRequest
        {
            PlanningCycleId = "PC-2026-07",
            ModelId = "PC-2026-07-001",
            Status = "Draft",
            Scope = new ModelScopeRequest { Type = "Scope", Name = "Name" },
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.2m,
            NaturalGasPriceUsdPerMmbtu = 2.3m
        });

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task UpdateModelAsync_MissingModelId_ThrowsArgumentException()
    {
        Mock<IModelRepository> repository = new();
        IModelService service = CreateService(repository.Object);

        await AssertThrowsAsync<ArgumentException>(() => service.UpdateModelAsync(new UpdateModelRequest
        {
            PlanningCycleId = "PC-2026-07",
            ModelId = "  ",
            Status = "Draft",
            Scope = new ModelScopeRequest { Type = "Scope", Name = "Name" },
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.2m,
            NaturalGasPriceUsdPerMmbtu = 2.3m
        }));
    }

    [TestMethod]
    public async Task UpdateModelAsync_InvalidStatus_ThrowsArgumentException()
    {
        Mock<IModelRepository> repository = new();
        IModelService service = CreateService(repository.Object);

        await AssertThrowsAsync<ArgumentException>(() => service.UpdateModelAsync(new UpdateModelRequest
        {
            PlanningCycleId = "PC-2026-07",
            ModelId = "PC-2026-07-001",
            Status = "Invalid",
            Scope = new ModelScopeRequest { Type = "Scope", Name = "Name" },
            ModelType = "Type-A",
            EthanePriceUsdPerGal = 1.2m,
            NaturalGasPriceUsdPerMmbtu = 2.3m
        }));
    }

    [TestMethod]
    public async Task DeleteModelAsync_InvalidModelId_ThrowsArgumentException()
    {
        Mock<IModelRepository> repository = new();
        IModelService service = CreateService(repository.Object);

        await AssertThrowsAsync<ArgumentException>(
            () => service.DeleteModelAsync("PC-2026-07", string.Empty));

        repository.Verify(
            x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task DeleteModelAsync_ValidRequest_PropagatesToken()
    {
        Mock<IModelRepository> repository = new();
        CancellationToken captured = CancellationToken.None;

        repository
            .Setup(x => x.DeleteAsync("PC-2026-07", "PC-2026-07-001", It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, _, token) => captured = token)
            .ReturnsAsync(true);

        IModelService service = CreateService(repository.Object);

        using CancellationTokenSource cts = new();
        bool deleted = await service.DeleteModelAsync("PC-2026-07", "PC-2026-07-001", cts.Token);

        Assert.IsTrue(deleted);
        Assert.AreEqual(cts.Token, captured);
    }

    private static IModelService CreateService(IModelRepository repository)
    {
        ServiceCollection services = new();
        services.AddApplication();
        services.AddSingleton(repository);

        ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IModelService>();
    }

    private static DstModel CreateModel(string id) => new()
    {
        ModelId = id,
        PlanningCycleId = "PC-2026-07",
        CreatedOn = new DateOnly(2026, 7, 1),
        Status = "Draft",
        Scope = new ModelScope { Type = "Scope", Name = "Name" },
        ModelType = "Type-A",
        EthanePriceUsdPerGal = 1.2m,
        NaturalGasPriceUsdPerMmbtu = 2.3m,
        ExpectedValueMonthlyUsd = 100000m
    };

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
