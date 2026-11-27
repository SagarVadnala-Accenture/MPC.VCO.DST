using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MPC.VCO.DST.Application.Exceptions;
using MPC.VCO.DST.Infrastructure.Data.Models;
using MPC.VCO.DST.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Text;

namespace MPC.VCO.DST.Tests.Infrastructure;

[TestClass]
public sealed class ForecastRepositoryProcessingTests
{
    [TestMethod]
    public void NormalizePlantId_SupportedFormats_ReturnsCanonicalPlantId()
    {
        Assert.AreEqual("P1", ForecastRepository.NormalizePlantId("P1"));
        Assert.AreEqual("P1", ForecastRepository.NormalizePlantId("PLANT_01"));
        Assert.AreEqual("P1", ForecastRepository.NormalizePlantId("plant_001"));
        Assert.AreEqual("P9", ForecastRepository.NormalizePlantId("p9"));
    }

    [TestMethod]
    public void Interpolate_MissingDates_InterpolatesValuesWithinPlanningRange()
    {
        ForecastRepository repository = CreateRepository();
        DateOnly start = new(2026, 11, 20);
        ForecastRepository.ForecastObservation[] observations =
        [
            new() { Date = start.AddDays(-1), Value = 1 },
            new() { Date = start.AddDays(3), Value = 5 }
        ];

        double[] values = repository.Interpolate(
            observations,
            null,
            "FORECAST_ETHANE_PRICE",
            start,
            start.AddDays(2),
            ForecastRepository.ForecastValueKind.MarketPrice);

        CollectionAssert.AreEqual(new[] { 2d, 3d, 4d }, values);
    }

    [TestMethod]
    public void ValidateRows_NonFiniteSqlValue_ThrowsForecastUnavailableException()
    {
        ForecastRepository.ForecastObservation[] rows =
        [
            new() { Date = new DateOnly(2026, 11, 20), Value = double.NaN }
        ];

        Assert.Throws<ForecastUnavailableException>(() => ForecastRepository.ValidateRows(
            "FORECAST_ETHANE_PRICE",
            rows,
            ForecastRepository.ForecastValueKind.MarketPrice));
    }

    [TestMethod]
    public void ValidateRows_UnknownPlantPoolId_ThrowsForecastUnavailableException()
    {
        ForecastRepository.ForecastObservation[] rows =
        [
            new() { Date = new DateOnly(2026, 11, 20), Plant = "POOL-X", Value = 1 }
        ];

        Assert.Throws<ForecastUnavailableException>(() => ForecastRepository.ValidateRows(
            "FORECAST_ETHANE_TAKEAWAY_CAPACITY",
            rows,
            ForecastRepository.ForecastValueKind.NonNegative,
            plantLevel: true));
    }

    private static ForecastRepository CreateRepository()
    {
        DbContextOptions<dstSqlDbContext> options = new DbContextOptionsBuilder<dstSqlDbContext>().Options;
        return new ForecastRepository(
            new dstSqlDbContext(options),
            NullLogger<ForecastRepository>.Instance);
    }
}
