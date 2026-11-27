using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MPC.VCO.DST.Application.DTOs.Request;
using MPC.VCO.DST.Application.DTOs.Response;
using MPC.VCO.DST.Application.Interfaces;
using MPC.VCO.DST.Domain.Models.Optimizer;
using MPC.VCO.DST.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;

namespace MPC.VCO.DST.Tests.Infrastructure;

[TestClass]
public sealed class OrToolsPortfolioOptimizerTests
{
    [TestMethod]
    public async Task SolveAsync_LockedRecovery_RemainsFixed()
    {
        DateOnly start = new(2026, 11, 20);
        double[] prices = [10];
        double[,] gasVolume = new double[9, 1];
        double[,] composition = new double[9, 1];
        double[,] ethaneCapacity = new double[9, 1];
        double[,] residueCapacity = new double[9, 1];
        for (int plant = 0; plant < 9; plant++)
        {
            gasVolume[plant, 0] = 100;
            composition[plant, 0] = 0.05;
            ethaneCapacity[plant, 0] = 100000;
            residueCapacity[plant, 0] = 100;
        }
        Forecast forecast = Forecast.Create(
            start,
            start,
            prices,
            [1],
            gasVolume,
            composition,
            ethaneCapacity,
            residueCapacity);
        OptimizeRequest request = new()
        {
            TfFee = 0,
            EthaneHeatingValue = 0,
            KFix = 0,
            KVar = 0,
            TakeawayBblPerDay = 100000,
            Trains = new Dictionary<string, TrainSettings>
            {
                ["P1"] = new() { LockedAt = 0.4 }
            }
        };
        using ServiceProvider provider = CreateProvider();
        IPortfolioOptimizer optimizer = provider.GetRequiredService<IPortfolioOptimizer>();

        PortfolioOptimizationResult result = await optimizer.SolveAsync(request, forecast);

        Assert.IsTrue(result.Status is "optimal" or "feasible");
        Assert.AreEqual(0.4, result.TrainSchedule.Single(day => day.Train == "P1").Recovery, 1e-4);
        Assert.AreEqual(start, result.TrainSchedule[0].Date);
    }

    [TestMethod]
    public async Task SolveAsync_MaximumChangesZero_KeepsTrainRecoveryConstant()
    {
        Forecast forecast = CreateForecast([10, 0]);
        OptimizeRequest request = new()
        {
            TfFee = 0,
            EthaneHeatingValue = 0,
            KFix = 0,
            KVar = 0,
            TakeawayBblPerDay = 100000,
            MaxChangesPerTrain = 0,
            Trains = new Dictionary<string, TrainSettings>
            {
                ["P1"] = new() { RMin = 0, RMax = 1, R0 = 0.3, COp = 5 }
            }
        };
        using ServiceProvider provider = CreateProvider();
        IPortfolioOptimizer optimizer = provider.GetRequiredService<IPortfolioOptimizer>();

        PortfolioOptimizationResult result = await optimizer.SolveAsync(request, forecast);

        TrainDay[] schedule = result.TrainSchedule.Where(day => day.Train == "P1").ToArray();
        Assert.AreEqual(2, schedule.Length);
        Assert.AreEqual(schedule[0].Recovery, schedule[1].Recovery, 1e-4);
    }

    [TestMethod]
    public async Task SolveAsync_PortfolioTakeawayCapacityBoundsRecoveredEthane()
    {
        Forecast forecast = CreateForecast([10]);
        double capacity = Enumerable.Range(0, forecast.Trains.Length)
            .Sum(train => forecast.InletEthaneBbl[train, 0]) * 0.25;
        OptimizeRequest request = new()
        {
            TfFee = 0,
            EthaneHeatingValue = 0,
            KFix = 0,
            KVar = 0,
            TakeawayBblPerDay = capacity
        };
        using ServiceProvider provider = CreateProvider();
        IPortfolioOptimizer optimizer = provider.GetRequiredService<IPortfolioOptimizer>();

        PortfolioOptimizationResult result = await optimizer.SolveAsync(request, forecast);

        double recoveredEthane = result.TrainSchedule.Sum(day =>
        {
            int trainIndex = Array.IndexOf(forecast.Trains, day.Train);
            return forecast.InletEthaneBbl[trainIndex, day.Day - 1] * day.Recovery;
        });
        Assert.IsTrue(result.Status is "optimal" or "feasible");
        Assert.IsTrue(recoveredEthane <= capacity + 1e-4);
    }

    [TestMethod]
    public async Task SolveAsync_MinimumHoldDays_PreventsAdjacentChanges()
    {
        Forecast forecast = CreateForecast([10, -10, 10]);
        OptimizeRequest request = new()
        {
            TfFee = 0,
            EthaneHeatingValue = 0,
            KFix = 0,
            KVar = 0,
            TakeawayBblPerDay = 100000,
            MinHoldDays = 2,
            Trains = new Dictionary<string, TrainSettings>
            {
                ["P1"] = new() { RMin = 0, RMax = 1, R0 = 0.2 }
            }
        };
        using ServiceProvider provider = CreateProvider();
        IPortfolioOptimizer optimizer = provider.GetRequiredService<IPortfolioOptimizer>();

        PortfolioOptimizationResult result = await optimizer.SolveAsync(request, forecast);

        TrainDay[] schedule = result.TrainSchedule.Where(day => day.Train == "P1").OrderBy(day => day.Day).ToArray();
        Assert.IsTrue(result.Status is "optimal" or "feasible");
        for (int day = 0; day < schedule.Length - 1; day++)
        {
            Assert.IsFalse(schedule[day].Changed && schedule[day + 1].Changed);
        }
    }

    [TestMethod]
    public async Task SolveAsync_PlantEthaneCapacityLimitsRecovery()
    {
        Forecast forecast = CreateForecast([10], p1EthaneCapacity: 0);
        OptimizeRequest request = new()
        {
            TfFee = 0,
            EthaneHeatingValue = 0,
            KFix = 0,
            KVar = 0,
            TakeawayBblPerDay = 100000,
            Trains = new Dictionary<string, TrainSettings>
            {
                ["P1"] = new() { RMin = 0, RMax = 1, R0 = 0.2 }
            }
        };
        using ServiceProvider provider = CreateProvider();
        IPortfolioOptimizer optimizer = provider.GetRequiredService<IPortfolioOptimizer>();

        PortfolioOptimizationResult result = await optimizer.SolveAsync(request, forecast);

        Assert.IsTrue(result.Status is "optimal" or "feasible");
        Assert.AreEqual(0, result.TrainSchedule.Single(day => day.Train == "P1").Recovery, 1e-4);
    }

    [TestMethod]
    public async Task SolveAsync_LockedRecoveryOutsideCapacity_ReturnsInfeasibleStatus()
    {
        Forecast forecast = CreateForecast([10], p1EthaneCapacity: 0);
        OptimizeRequest request = new()
        {
            TfFee = 0,
            EthaneHeatingValue = 0,
            KFix = 0,
            KVar = 0,
            TakeawayBblPerDay = 100000,
            Trains = new Dictionary<string, TrainSettings>
            {
                ["P1"] = new() { LockedAt = 0.4 }
            }
        };
        using ServiceProvider provider = CreateProvider();
        IPortfolioOptimizer optimizer = provider.GetRequiredService<IPortfolioOptimizer>();

        PortfolioOptimizationResult result = await optimizer.SolveAsync(request, forecast);

        Assert.AreEqual("infeasible", result.Status);
        Assert.IsNull(result.MarginUsd);
        Assert.AreEqual(0, result.TrainSchedule.Count);
    }

    [TestMethod]
    public async Task SolveAsync_PreCanceledToken_ThrowsOperationCanceledException()
    {
        DateOnly start = new(2026, 11, 20);
        double[] prices = [1];
        double[,] values = new double[9, 1];
        Forecast forecast = Forecast.Create(start, start, prices, prices, values, values, values, values);
        using ServiceProvider provider = CreateProvider();
        IPortfolioOptimizer optimizer = provider.GetRequiredService<IPortfolioOptimizer>();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await AssertThrowsAsync<OperationCanceledException>(() =>
            optimizer.SolveAsync(new OptimizeRequest(), forecast, cancellation.Token));
    }

    private static Forecast CreateForecast(double[] ethanePrices, double p1EthaneCapacity = 100000)
    {
        DateOnly start = new(2026, 11, 20);
        DateOnly end = start.AddDays(ethanePrices.Length - 1);
        double[] gasPrices = Enumerable.Repeat(1d, ethanePrices.Length).ToArray();
        double[,] gasVolume = new double[9, ethanePrices.Length];
        double[,] composition = new double[9, ethanePrices.Length];
        double[,] ethaneCapacity = new double[9, ethanePrices.Length];
        double[,] residueCapacity = new double[9, ethanePrices.Length];
        for (int plant = 0; plant < 9; plant++)
        {
            for (int day = 0; day < ethanePrices.Length; day++)
            {
                gasVolume[plant, day] = 100;
                composition[plant, day] = 0.05;
                ethaneCapacity[plant, day] = plant == 0 ? p1EthaneCapacity : 100000;
                residueCapacity[plant, day] = 100;
            }
        }

        return Forecast.Create(
            start,
            end,
            ethanePrices,
            gasPrices,
            gasVolume,
            composition,
            ethaneCapacity,
            residueCapacity);
    }

    private static ServiceProvider CreateProvider()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CosmosDb:Endpoint"] = "https://localhost:8081/",
                ["CosmosDb:Key"] = "C2y6yDjf5/R+ob0N8A7Cgv30VRM="
            })
            .Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddInfrastructure(configuration);
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
}
