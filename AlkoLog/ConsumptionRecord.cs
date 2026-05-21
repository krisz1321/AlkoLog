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

    string emptyingProgressText = string.Empty;

    [JsonIgnore]
    public string EmptyingProgressText
    {
        get => emptyingProgressText;
        set
        {
            if (emptyingProgressText == value)
            {
                return;
            }

            emptyingProgressText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EmptyingProgressText)));
        }
    }

    bool isEmptyingProgressVisible;

    [JsonIgnore]
    public bool IsEmptyingProgressVisible
    {
        get => isEmptyingProgressVisible;
        set
        {
            if (isEmptyingProgressVisible == value)
            {
                return;
            }

            isEmptyingProgressVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEmptyingProgressVisible)));
        }
    }

    bool isEmpty;

    [JsonIgnore]
    public bool IsEmpty
    {
        get => isEmpty;
        set
        {
            if (isEmpty == value)
            {
                return;
            }

            isEmpty = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEmpty)));
        }
    }
}