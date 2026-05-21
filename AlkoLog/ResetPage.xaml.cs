namespace AlkoLog;

public partial class ResetPage : ContentPage
{
    readonly MainPageViewModel _mainPageViewModel;
    readonly ProfilePageViewModel _profilePageViewModel;
    readonly CatalogPageViewModel _catalogPageViewModel;

    public ResetPage(MainPageViewModel mainPageViewModel, ProfilePageViewModel profilePageViewModel, CatalogPageViewModel catalogPageViewModel)
    {
        InitializeComponent();

        _mainPageViewModel = mainPageViewModel;
        _profilePageViewModel = profilePageViewModel;
        _catalogPageViewModel = catalogPageViewModel;
    }

    private async void Button_Clicked(object sender, EventArgs e)
    {
        bool confirmed = await DisplayAlert("Megerősítés", "Biztosan törlöd az összes mentett adatot?", "Törlés", "Mégse");

        if (!confirmed)
        {
            return;
        }

        try
        {
            DeleteIfExists(Path.Combine(FileSystem.Current.AppDataDirectory, "profile.json"));
            DeleteIfExists(Path.Combine(FileSystem.Current.AppDataDirectory, "profilephoto.jpg"));
            DeleteIfExists(Path.Combine(FileSystem.Current.AppDataDirectory, "catalog.json"));
            DeleteIfExists(Path.Combine(FileSystem.Current.AppDataDirectory, "drinkphoto.jpg"));
            DeleteIfExists(Path.Combine(FileSystem.Current.AppDataDirectory, "consumed.json"));

            _mainPageViewModel.ResetData();
            _profilePageViewModel.ResetData();
            await _catalogPageViewModel.ResetDataAsync();

            await DisplayAlert("Kész", "Az alkalmazás visszaállt az alaphelyzetre.", "OK");
            await Shell.Current.GoToAsync("//ProfilePage");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("HIBA ALAPHELYZETBE ÁLLÍTÁSKOR: " + ex.Message);
            await DisplayAlert("Hiba", "Nem sikerült törölni az adatokat.", "OK");
        }
    }

    void DeleteIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}