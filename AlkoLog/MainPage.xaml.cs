namespace AlkoLog
{
    public partial class MainPage : ContentPage
    {
        MainPageViewModel _viewModel;

        public MainPage(MainPageViewModel viewModel)
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

}
