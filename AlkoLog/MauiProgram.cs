using Microsoft.Extensions.Logging;

namespace AlkoLog
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<MainPageViewModel>();
            builder.Services.AddSingleton<ProfilePageViewModel>();
            builder.Services.AddSingleton<CatalogPageViewModel>();

            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<ProfilePage>();
            builder.Services.AddSingleton<CatalogPage>();
            builder.Services.AddSingleton<AboutPage>();
            builder.Services.AddSingleton<ResetPage>();
            builder.Services.AddTransient<NewDrinkPage>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
