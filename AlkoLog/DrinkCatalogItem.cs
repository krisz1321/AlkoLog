using CommunityToolkit.Mvvm.ComponentModel;

namespace AlkoLog;

public partial class DrinkCatalogItem : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private double alcoholPercent;

    [ObservableProperty]
    private bool isFavorite;

    [ObservableProperty]
    private string imagePath = "ital.png";
}