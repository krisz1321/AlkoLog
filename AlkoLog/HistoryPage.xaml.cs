namespace AlkoLog;

public partial class HistoryPage : ContentPage
{
    readonly HistoryPageViewModel _viewModel;

    public HistoryPage(HistoryPageViewModel viewModel)
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

    private async void BackButton_Clicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//MainPage");
    }
}
