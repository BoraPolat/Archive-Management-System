using System.Threading.Tasks;

namespace SoftwareDesign;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Resources bölümüne
        Resources.Add("StringNotEmptyConverter", new StringNotEmptyConverter());

        // ✅ Platform izinlerini başlangıçta iste
        RequestPermissionsAsync();

        MainPage = new NavigationPage(new LoginPage())
        {
            BarBackgroundColor = Color.FromArgb("#000E38"),
            BarTextColor = Colors.White
        };

#if WINDOWS
        // Windows'ta mouse wheel desteğini başlat
        Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping("MouseWheelSupport", (handler, view) =>
        {
            if (handler.PlatformView is Microsoft.UI.Xaml.Window window)
            {
                Platforms.Windows.MouseWheelHelper.Initialize(window);
            }
        });
#endif
    }

    // ✅ YENİ: Mobil platformlar için izin isteme
    private async void RequestPermissionsAsync()
    {
        try
        {
            // Android için storage izinleri
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                await RequestAndroidPermissionsAsync();
            }

            // iOS için özel izin gerekmez (Info.plist yeterli)
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Permission request failed: {ex.Message}");
        }
    }

    private async Task RequestAndroidPermissionsAsync()
    {
        // Storage Read Permission
        var readStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
        if (readStatus != PermissionStatus.Granted)
        {
            readStatus = await Permissions.RequestAsync<Permissions.StorageRead>();
        }

        // Storage Write Permission (Android 10 ve altı için)
        if (DeviceInfo.Version.Major < 11)
        {
            var writeStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
            if (writeStatus != PermissionStatus.Granted)
            {
                writeStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
            }
        }
    }
}