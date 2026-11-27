using Microsoft.Extensions.DependencyInjection;
using Moq;
using MPC.VCO.DST.Application;
using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.Exceptions;
using MPC.VCO.DST.Application.Interfaces;
using MPC.VCO.DST.Domain.Models.Optimizer;
using System;
using System.Collections.Generic;
using System.Text;

namespace MPC.VCO.DST.Tests.Application.Implementation;

[TestClass]
public sealed class OptimizerServiceTests
{
    [TestMethod]
    public async Task OptimizeAsync_InvalidRequest_ThrowsValidationWithoutLoadingForecast()
    {
        Mock<IForecastRepository> forecastRepository = new();
        Mock<IPortfolioOptimizer> portfolioOptimizer = new();
        using ServiceProvider provider = CreateProvider(forecastRepository, portfolioOptimizer);
        IOptimizerService service = provider.GetRequiredService<IOptimizerService>();

        OptimizerValidationException exception = await AssertThrowsAsync<OptimizerValidationException>(
            () => service.OptimizeAsync(new OptimizeRequest
            {
                PlanningStartDate = new DateOnly(2026, 11, 20),
                PlanningEndDate = new DateOnly(2026, 11, 19),
                KFix = -1
            }));

        Assert.IsTrue(exception.Errors.ContainsKey("planning_end_date"));
        Assert.IsTrue(exception.Errors.ContainsKey("k_fix"));
        forecastRepository.Verify(
            repository => repository.GetForecastAsync(
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task OptimizeAsync_UnknownTrain_ThrowsValidationAfterForecastLoad()
    {
        Forecast forecast = CreateForecast(new DateOnly(2026, 11, 20), 1);
        Mock<IForecastRepository> forecastRepository = new();
        forecastRepository
            .Setup(repository => repository.GetForecastAsync(
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(forecast);
        Mock<IPortfolioOptimizer> portfolioOptimizer = new();
        using ServiceProvider provider = CreateProvider(forecastRepository, portfolioOptimizer);
        IOptimizerService service = provider.GetRequiredService<IOptimizerService>();

        OptimizerValidationException exception = await AssertThrowsAsync<OptimizerValidationException>(
            () => service.OptimizeAsync(new OptimizeRequest
            {
                PlanningStartDate = forecast.StartDate,
                PlanningEndDate = forecast.EndDate,
                Trains = new Dictionary<string, TrainSettings> { ["T10"] = new() }
            }));

        Assert.IsTrue(exception.Errors.ContainsKey("trains"));
        portfolioOptimizer.Verify(
            optimizer => optimizer.SolveAsync(
                It.IsAny<OptimizeRequest>(),
                It.IsAny<Forecast>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ServiceProvider CreateProvider(
        Mock<IForecastRepository> forecastRepository,
        Mock<IPortfolioOptimizer> portfolioOptimizer)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton(forecastRepository.Object);
        services.AddSingleton(portfolioOptimizer.Object);
        return services.BuildServiceProvider();
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

    private static Forecast CreateForecast(DateOnly start, int horizon)
    {
        DateOnly end = start.AddDays(horizon - 1);
        double[] prices = Enumerable.Repeat(1d, horizon).ToArray();
        double[,] plantValues = new double[9, horizon];
        return Forecast.Create(
            start,
            end,
            prices,
            prices,
            plantValues,
            plantValues,
            plantValues,
            plantValues);
    }
}
