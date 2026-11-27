using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.DTOs.Response;
using MPC.VCO.DST.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MPC.VCO.DST.WebSite.API.Controllers;

namespace MPC.VCO.DST.Tests.API.Controllers;

[TestClass]
public sealed class ModelsControllerTests
{
    [TestMethod]
    public async Task GetModelsAsync_ValidRequest_ReturnsOkWithModels()
    {
        Mock<IModelService> service = new();
        IReadOnlyList<ModelResponse> expected =
        [
            new ModelResponse
            {
                ModelId = "M-001",
                CreatedOn = new DateOnly(2026, 7, 1),
                Status = "Draft",
                Scope = new ModelScopeResponse { Type = "Refinery", Name = "TX" },
                ModelType = "Type-A",
                EthanePriceUsdPerGal = 1.2m,
                NaturalGasPriceUsdPerMmbtu = 2.3m,
                ExpectedValueMonthlyUsd = 0
            }
        ];

        service
            .Setup(x => x.GetModelsAsync(
                It.Is<GetModelsRequest>(request =>
                    request.PlanningCycleId == "PC-2026-07"
                    && request.PageNumber == 2
                    && request.PageSize == 25),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        ModelsController controller = new(service.Object);

        ActionResult<IReadOnlyList<ModelResponse>> response = await controller.GetModelsAsync(
            "PC-2026-07",
            2,
            25,
            CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(response.Result);
        OkObjectResult ok = (OkObjectResult)response.Result!;
        Assert.AreSame(expected, ok.Value);
    }

    [TestMethod]
    public async Task GetModelAsync_ModelNotFound_ReturnsNotFound()
    {
        Mock<IModelService> service = new();
        service
            .Setup(x => x.GetModelAsync("PC-2026-07", "M-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModelResponse?)null);

        ModelsController controller = new(service.Object);

        ActionResult<ModelResponse> response = await controller.GetModelAsync("PC-2026-07", "M-001", CancellationToken.None);

        Assert.IsInstanceOfType<NotFoundResult>(response.Result);
    }

    [TestMethod]
    public async Task UpdateModelAsync_ModelNotFound_ReturnsNotFound()
    {
        Mock<IModelService> service = new();
        service
            .Setup(x => x.UpdateModelAsync(It.IsAny<UpdateModelRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModelResponse?)null);

        ModelsController controller = new(service.Object);

        ActionResult<ModelResponse> response = await controller.UpdateModelAsync(
            "PC-2026-07",
            "M-001",
            new UpdateModelRequest
            {
                Status = "Draft",
                Scope = new ModelScopeRequest { Type = "Refinery", Name = "TX" },
                ModelType = "Type-A",
                EthanePriceUsdPerGal = 1.2m,
                NaturalGasPriceUsdPerMmbtu = 2.3m
            },
            CancellationToken.None);

        Assert.IsInstanceOfType<NotFoundResult>(response.Result);
    }

    [TestMethod]
    public async Task UpdateModelAsync_ValidRequest_ReturnsOk()
    {
        Mock<IModelService> service = new();
        service
            .Setup(x => x.UpdateModelAsync(It.IsAny<UpdateModelRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModelResponse
            {
                ModelId = "M-001",
                CreatedOn = new DateOnly(2026, 7, 1),
                Status = "Reviewed",
                Scope = new ModelScopeResponse { Type = "Refinery", Name = "TX" },
                ModelType = "Type-A",
                EthanePriceUsdPerGal = 1.2m,
                NaturalGasPriceUsdPerMmbtu = 2.3m,
                ExpectedValueMonthlyUsd = 1000
            });

        ModelsController controller = new(service.Object);

        ActionResult<ModelResponse> response = await controller.UpdateModelAsync(
            "PC-2026-07",
            "M-001",
            new UpdateModelRequest
            {
                Status = "Reviewed",
                Scope = new ModelScopeRequest { Type = "Refinery", Name = "TX" },
                ModelType = "Type-A",
                EthanePriceUsdPerGal = 1.2m,
                NaturalGasPriceUsdPerMmbtu = 2.3m
            },
            CancellationToken.None);

        Assert.IsInstanceOfType<OkObjectResult>(response.Result);
    }

    [TestMethod]
    public async Task UpdateModelAsync_InvalidRequest_ReturnsBadRequest()
    {
        Mock<IModelService> service = new();
        service
            .Setup(x => x.UpdateModelAsync(It.IsAny<UpdateModelRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid request."));

        ModelsController controller = new(service.Object);

        await AssertThrowsAsync<ArgumentException>(() => controller.UpdateModelAsync(
            "PC-2026-07",
            "M-001",
            new UpdateModelRequest(),
            CancellationToken.None));
    }

    [TestMethod]
    public async Task CreateModelAsync_ValidRequest_ReturnsCreatedAtAction()
    {
        Mock<IModelService> service = new();
        service
            .Setup(x => x.CreateModelAsync(It.IsAny<CreateModelRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModelResponse
            {
                ModelId = "M-001",
                CreatedOn = new DateOnly(2026, 7, 1),
                Status = "Draft",
                Scope = new ModelScopeResponse { Type = "Refinery", Name = "TX" },
                ModelType = "Type-A",
                EthanePriceUsdPerGal = 1.2m,
                NaturalGasPriceUsdPerMmbtu = 2.3m,
                ExpectedValueMonthlyUsd = 0
            });

        ModelsController controller = new(service.Object);

        ActionResult<ModelResponse> response = await controller.CreateModelAsync(
            "PC-2026-07",
            new CreateModelRequest
            {
                Status = "Draft",
                Scope = new ModelScopeRequest { Type = "Refinery", Name = "TX" },
                ModelType = "Type-A",
                EthanePriceUsdPerGal = 1.2m,
                NaturalGasPriceUsdPerMmbtu = 2.3m
            },
            CancellationToken.None);

        Assert.IsInstanceOfType<CreatedAtActionResult>(response.Result);
        CreatedAtActionResult created = (CreatedAtActionResult)response.Result!;
        Assert.AreEqual(nameof(ModelsController.GetModelAsync), created.ActionName);
        Assert.AreEqual("PC-2026-07", created.RouteValues!["planningCycleId"]);
        Assert.AreEqual("M-001", created.RouteValues["modelId"]);
    }

    [TestMethod]
    public async Task CreateModelAsync_InvalidRequest_ReturnsBadRequest()
    {
        Mock<IModelService> service = new();
        service
            .Setup(x => x.CreateModelAsync(It.IsAny<CreateModelRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Status must be one of: Draft, Reviewed, Executable."));

        ModelsController controller = new(service.Object);

        await AssertThrowsAsync<ArgumentException>(() => controller.CreateModelAsync(
            "PC-2026-07",
            new CreateModelRequest(),
            CancellationToken.None));
    }

    [TestMethod]
    public async Task DeleteModelAsync_ModelMissing_ReturnsNotFound()
    {
        Mock<IModelService> service = new();
        service
            .Setup(x => x.DeleteModelAsync("PC-2026-07", "M-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        ModelsController controller = new(service.Object);

        IActionResult response = await controller.DeleteModelAsync("PC-2026-07", "M-001", CancellationToken.None);

        Assert.IsInstanceOfType<NotFoundResult>(response);
    }

    [TestMethod]
    public async Task DeleteModelAsync_ModelDeleted_ReturnsNoContent()
    {
        Mock<IModelService> service = new();
        service
            .Setup(x => x.DeleteModelAsync("PC-2026-07", "M-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        ModelsController controller = new(service.Object);

        IActionResult response = await controller.DeleteModelAsync("PC-2026-07", "M-001", CancellationToken.None);

        Assert.IsInstanceOfType<NoContentResult>(response);
    }

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
            Assert.Fail($"Expected exception {typeof(TException).Name} was not thrown.");
            throw new InvalidOperationException("Unreachable assertion path.");
        }
        catch (TException exception)
        {
            return exception;
        }
    }
}
