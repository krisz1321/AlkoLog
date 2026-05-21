namespace AlkoLog;

public partial class ProfilePage : ContentPage
{
    ProfilePageViewModel _viewModel;

    public ProfilePage(ProfilePageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _viewModel.LoadDataAsync();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();

        await _viewModel.SaveDataAsync();
    }
}