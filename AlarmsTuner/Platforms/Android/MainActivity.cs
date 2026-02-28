using Android.App;
using Android.Content.PM;
using Android.Hardware.Usb;
using Android.OS;

namespace AlarmsTuner
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [IntentFilter([UsbManager.ActionUsbDeviceAttached])]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}

