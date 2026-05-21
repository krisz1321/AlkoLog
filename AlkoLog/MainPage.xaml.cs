using Microsoft.Maui.Dispatching;

namespace AlkoLog
{
    public partial class MainPage : ContentPage
    {
        MainPageViewModel _viewModel;
        IDispatcherTimer? _summaryRefreshTimer;

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
            StartSummaryRefreshTimer();
        }

        protected override async void OnDisappearing()
        {
            StopSummaryRefreshTimer();
            base.OnDisappearing();

            await _viewModel.SaveDataAsync();
        }

        void StartSummaryRefreshTimer()
        {
            if (_summaryRefreshTimer != null)
            {
                return;
            }

            _summaryRefreshTimer = Dispatcher.CreateTimer();
            _summaryRefreshTimer.Interval = TimeSpan.FromSeconds(10);
            _summaryRefreshTimer.Tick += SummaryRefreshTimer_Tick;
            _summaryRefreshTimer.Start();
        }

        void StopSummaryRefreshTimer()
        {
            if (_summaryRefreshTimer == null)
            {
                return;
            }

            _summaryRefreshTimer.Tick -= SummaryRefreshTimer_Tick;
            _summaryRefreshTimer.Stop();
            _summaryRefreshTimer = null;
        }

        void SummaryRefreshTimer_Tick(object? sender, EventArgs e)
        {
            _viewModel.RefreshSummary(true);
        }
    }

}
