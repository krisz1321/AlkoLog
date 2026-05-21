using System.ComponentModel;
using System.Text.Json.Serialization;

namespace AlkoLog;

public class ConsumptionRecord : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string DrinkName { get; set; } = string.Empty;

    public double AlcoholPercent { get; set; }

    public string DrinkDisplayName => $"{DrinkName} - {AlcoholPercent}%";

    public double AmountMl { get; set; }

    public DateTime Timestamp { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    TextDecorations textDecorations = TextDecorations.None;

    [JsonIgnore]
    public TextDecorations TextDecorations
    {
        get => textDecorations;
        set
        {
            if (textDecorations == value)
            {
                return;
            }

            textDecorations = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextDecorations)));
        }
    }
}