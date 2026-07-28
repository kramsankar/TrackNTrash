using Microsoft.Extensions.Logging;
using ZXing.Net.Maui.Controls;
using TrackNTrash.DriverApp.Services;
using TrackNTrash.DriverApp.ViewModels;
using TrackNTrash.DriverApp.Views;

namespace TrackNTrash.DriverApp;

public static class MauiProgram
{
    // Point at the deployed tracking API (Module 6/7). Use an app setting / build config in prod.
    private const string ApiBaseUrl = "https://tracktrash-tracking.azurewebsites.net";

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeReader();

        builder.Services.AddSingleton(sp =>
            new TrackingApiClient(new HttpClient { BaseAddress = new Uri(ApiBaseUrl) }));

        // Device identity for event attribution (replace with MDM device id).
        var deviceId = $"DRIVER-{DeviceInfo.Current.Name}";
        builder.Services.AddTransient(sp =>
            new LoadingViewModel(sp.GetRequiredService<TrackingApiClient>(), deviceId, userId: null));
        builder.Services.AddTransient<LoadingPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
