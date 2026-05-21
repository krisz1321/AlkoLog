namespace AlkoLog;

public partial class CatalogPage : ContentPage
{
    CatalogPageViewModel _viewModel;

    public CatalogPage(CatalogPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await _viewModel.LoadDataAsync();
        _viewModel.ResortCatalog();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();

        await _viewModel.SaveDataAsync();
    }

}