namespace MPC.VCO.DST.Domain.Models;

public class BestModel
{
    public string ModelId { get; init; } = string.Empty;

    //To enable recovery percent calculation, uncomment the following line

    //public decimal AverageRecoveryPercent { get; init; }

    public decimal ExpectedValueMonthlyUsd { get; init; }
}
