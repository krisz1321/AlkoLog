namespace AlkoLog;

[QueryProperty(nameof(ReceivedDrink), "editdrink")]
public partial class NewDrinkPage : ContentPage
{
    public DrinkCatalogItem NewDrink { get; set; }

    bool _isEditMode;

    public DrinkCatalogItem ReceivedDrink
    {
        set
        {
            if (value != null)
            {
                NewDrink = value;
                BindingContext = NewDrink;
                _isEditMode = true;

                if (string.IsNullOrWhiteSpace(NewDrink.ImagePath))
                {
                    NewDrink.ImagePath = "ital.png";
                }

                RefreshUiForMode();
            }
        }
    }

    public NewDrinkPage()
    {
        InitializeComponent();

        NewDrink = new DrinkCatalogItem
        {
            ImagePath = "ital.png",
            DefaultAmountMl = 0
        };

        BindingContext = NewDrink;
        RefreshUiForMode();
    }

    void RefreshUiForMode()
    {
        if (SaveButton == null)
        {
            return;
        }

        if (_isEditMode)
        {
            Title = "Ital szerkesztése";
            SaveButton.Text = "Mentés";
        }
        else
        {
            Title = "Új ital felvétele";
            SaveButton.Text = "Mentés és visszalépés";
        }
    }

    private async void TakePhotoButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (!await PermissionHelper.EnsureCameraPermissionAsync())
            {
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync();

            if (photo != null)
            {
                string targetPath = Path.Combine(FileSystem.Current.AppDataDirectory, "drinkphoto.jpg");

                await using var sourceStream = await photo.OpenReadAsync();
                await using var targetStream = File.Open(targetPath, FileMode.Create, FileAccess.Write);
                await sourceStream.CopyToAsync(targetStream);

                NewDrink.ImagePath = targetPath;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("HIBA ITALKÉPKÉSZÍTÉSKOR: " + ex.Message);
        }
    }

    private async void Button_Clicked(object sender, EventArgs e)
    {
        NewDrink.Name = NewDrink.Name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(NewDrink.Name) || NewDrink.AlcoholPercent <= 0)
        {
            await DisplayAlert("Hiányzó adat", "Az ital nevének és az alkoholszázaléknak is ki kell lennie töltve.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(NewDrink.ImagePath))
        {
            NewDrink.ImagePath = "ital.png";
        }

        if (_isEditMode)
        {
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            var navigationParameter = new ShellNavigationQueryParameters
            {
                { "newdrink", NewDrink }
            };

            await Shell.Current.GoToAsync("..", navigationParameter);
        }
    }
}