
namespace MPC.VCO.DST.Domain.Models.Optimizer;

public sealed class Forecast
{
    public const double StandardCubicFeetPerLbMol = 379.482;
    public const double EthaneMolecularWeightLbPerLbMol = 30.069;
    public const double AssumedLiquidEthaneDensityLbPerBbl = 124.8;

    public const double EthaneBblPerMmscfAtOneMoleFraction =
        1_000_000 / StandardCubicFeetPerLbMol
        * EthaneMolecularWeightLbPerLbMol
        / AssumedLiquidEthaneDensityLbPerBbl;

    private Forecast(
        DateOnly startDate,
        DateOnly endDate,
        string[] plants,
        string[] trains,
        IReadOnlyDictionary<string, string> trainPlant,
        int[] days,
        double[] ethanePrice,
        double[] gasPrice,
        double[,] inletGasVolumeMmscfPerDay,
        double[,] inletEthaneMoleFraction,
        double[,] plantEthaneTakeawayBblPerDay,
        double[,] plantResidueGasTakeawayMmscfPerDay,
        double[,] inletEthaneBbl)
    {
        StartDate = startDate;
        EndDate = endDate;
        Plants = plants;
        Trains = trains;
        TrainPlant = trainPlant;
        Days = days;
        EthanePrice = ethanePrice;
        GasPrice = gasPrice;
        PlantInletGasVolumeMmscfPerDay = inletGasVolumeMmscfPerDay;
        PlantInletEthaneMoleFraction = inletEthaneMoleFraction;
        PlantEthaneTakeawayBblPerDay = plantEthaneTakeawayBblPerDay;
        PlantResidueGasTakeawayMmscfPerDay = plantResidueGasTakeawayMmscfPerDay;
        InletEthaneBbl = inletEthaneBbl;
    }

    public DateOnly StartDate { get; }
    public DateOnly EndDate { get; }
    public string[] Plants { get; }
    public string[] Trains { get; }
    public IReadOnlyDictionary<string, string> TrainPlant { get; }
    public int[] Days { get; }
    public double[,] InletEthaneBbl { get; }
    public double[] EthanePrice { get; }
    public double[] GasPrice { get; }
    public double[,] PlantInletGasVolumeMmscfPerDay { get; }
    public double[,] PlantInletEthaneMoleFraction { get; }
    public double[,] PlantEthaneTakeawayBblPerDay { get; }
    public double[,] PlantResidueGasTakeawayMmscfPerDay { get; }

    public static Forecast Create(
        DateOnly startDate,
        DateOnly endDate,
        double[] ethanePrice,
        double[] naturalGasPrice,
        double[,] plantInletGasVolumeMmscfPerDay,
        double[,] plantInletEthaneMoleFraction,
        double[,] plantEthaneTakeawayBblPerDay,
        double[,] plantResidueGasTakeawayMmscfPerDay)
    {
        ArgumentNullException.ThrowIfNull(ethanePrice);
        ArgumentNullException.ThrowIfNull(naturalGasPrice);
        ArgumentNullException.ThrowIfNull(plantInletGasVolumeMmscfPerDay);
        ArgumentNullException.ThrowIfNull(plantInletEthaneMoleFraction);
        ArgumentNullException.ThrowIfNull(plantEthaneTakeawayBblPerDay);
        ArgumentNullException.ThrowIfNull(plantResidueGasTakeawayMmscfPerDay);

        int horizon = endDate.DayNumber - startDate.DayNumber + 1;
        if (horizon < 1 || ethanePrice.Length != horizon || naturalGasPrice.Length != horizon)
        {
            throw new ArgumentException("Forecast dates and portfolio series must have the same non-zero inclusive horizon.");
        }

        const int plantCount = 9;
        foreach (double[,] values in new[]
        {
            plantInletGasVolumeMmscfPerDay,
            plantInletEthaneMoleFraction,
            plantEthaneTakeawayBblPerDay,
            plantResidueGasTakeawayMmscfPerDay
        })
        {
            if (values.GetLength(0) != plantCount || values.GetLength(1) != horizon)
            {
                throw new ArgumentException("Plant forecast arrays must have dimensions [9 plants, inclusive horizon].");
            }
        }

        string[] plants = Enumerable.Range(1, plantCount).Select(number => $"P{number}").ToArray();
        string[] trains = [.. plants];
        var trainPlant = plants.ToDictionary(plant => plant, plant => plant, StringComparer.Ordinal);
        int[] days = Enumerable.Range(1, horizon).ToArray();
        var inletEthaneBbl = new double[plantCount, horizon];

        for (int plantIndex = 0; plantIndex < plantCount; plantIndex++)
        {
            for (int day = 0; day < horizon; day++)
            {
                inletEthaneBbl[plantIndex, day] =
                    plantInletGasVolumeMmscfPerDay[plantIndex, day]
                    * plantInletEthaneMoleFraction[plantIndex, day]
                    * EthaneBblPerMmscfAtOneMoleFraction;
            }
        }

        return new Forecast(
            startDate,
            endDate,
            plants,
            trains,
            trainPlant,
            days,
            ethanePrice,
            naturalGasPrice,
            plantInletGasVolumeMmscfPerDay,
            plantInletEthaneMoleFraction,
            plantEthaneTakeawayBblPerDay,
            plantResidueGasTakeawayMmscfPerDay,
            inletEthaneBbl);
    }
}
