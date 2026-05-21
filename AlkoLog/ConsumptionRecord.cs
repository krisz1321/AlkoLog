namespace AlkoLog;

public class ConsumptionRecord
{
    public string DrinkName { get; set; } = string.Empty;

    public double AlcoholPercent { get; set; }

    public string DrinkDisplayName => $"{DrinkName} - {AlcoholPercent}%";

    public double AmountMl { get; set; }

    public DateTime Timestamp { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }
}