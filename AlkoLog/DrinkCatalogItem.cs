using CommunityToolkit.Mvvm.ComponentModel;

namespace AlkoLog;

public partial class DrinkCatalogItem : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private double alcoholPercent;

    [ObservableProperty]
    private double defaultAmountMl;

    public string DefaultAmountText => DefaultAmountMl > 0
        ? $"Alapmennyiség: {DefaultAmountMl:0.#} ml"
        : "Alapmennyiség: nincs";

    partial void OnDefaultAmountMlChanged(double value)
    {
        OnPropertyChanged(nameof(DefaultAmountText));
    }

    [ObservableProperty]
    private bool isFavorite;

    [ObservableProperty]
    private string imagePath = "ital.png";
}