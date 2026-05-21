namespace AlkoLog
{
    public partial class AppShell : Shell
    {
        bool _profileRedirectChecked;

        public AppShell()
        {
            InitializeComponent();
            // REGISZTRÁCIÓ: Ha valaki a "newdrink" szóra hivatkozik,
            // akkor a NewDrinkPage oldalt kell megnyitni.
            Routing.RegisterRoute("newdrink", typeof(NewDrinkPage));

            Navigated += AppShell_Navigated;
        }

        async void AppShell_Navigated(object? sender, ShellNavigatedEventArgs e)
        {
            if (_profileRedirectChecked)
            {
                return;
            }

            _profileRedirectChecked = true;

            try
            {
                string filePath = Path.Combine(FileSystem.Current.AppDataDirectory, "profile.json");

                if (!File.Exists(filePath))
                {
                    await DisplayAlert("Figyelem", "Először készítsd el a profilodat!", "OK");
                    await GoToAsync("//ProfilePage");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("HIBA SHELL PROFIL ELLENŐRZÉSNÉL: " + ex.Message);
            }
        }
    }
}
