using AlarmsTuner.Services;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace AlarmsTuner
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
                });

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddMudServices();
            builder.Services.AddMudBlazorDialog();

            // Register Virtual COM Port Terminal Service
#if WINDOWS
            builder.Services.AddSingleton<AlarmsTuner.Services.IUsbTerminalService, AlarmsTuner.Services.UsbTerminalService>();
#elif ANDROID
            builder.Services.AddSingleton<IUsbTerminalService, AndroidUsbTerminalService>();
#endif

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
