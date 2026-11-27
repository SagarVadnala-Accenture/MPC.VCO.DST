using MPC.VCO.DST.Domain.Models.Optimizer;
using System;
using System.Collections.Generic;
using System.Text;

namespace MPC.VCO.DST.Tests.Application.Implementation;

[TestClass]
public sealed class ForecastFactoryTests
{
    [TestMethod]
    public void Create_InletEthane_IsDerivedFromGasVolumeAndMoleFraction()
    {
        DateOnly start = new(2026, 11, 20);
        double[] portfolio = [10];
        double[,] gasVolume = new double[9, 1];
        double[,] composition = new double[9, 1];
        double[,] capacities = new double[9, 1];
        gasVolume[0, 0] = 100;
        composition[0, 0] = 0.05;

        Forecast forecast = Forecast.Create(
            start,
            start,
            portfolio,
            portfolio,
            gasVolume,
            composition,
            capacities,
            capacities);

        Assert.AreEqual(
            100 * 0.05 * Forecast.EthaneBblPerMmscfAtOneMoleFraction,
            forecast.InletEthaneBbl[0, 0],
            1e-8);
        Assert.AreEqual("P9", forecast.Trains[8]);
        Assert.AreEqual("P1", forecast.TrainPlant["P1"]);
    }

    [TestMethod]
    public void Create_MismatchedHorizon_ThrowsArgumentException()
    {
        double[] prices = [1];
        double[,] plantValues = new double[9, 1];

        Assert.Throws<ArgumentException>(() => Forecast.Create(
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 21),
            prices,
            prices,
            plantValues,
            plantValues,
            plantValues,
            plantValues));
    }
}
