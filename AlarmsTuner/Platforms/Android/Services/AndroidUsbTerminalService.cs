#if ANDROID
using AlarmsTuner.Models;
using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Anotherlab.UsbSerialForAndroid.Driver;
using System.Collections.ObjectModel;
using Application = Android.App.Application;

namespace AlarmsTuner.Services;

public class AndroidUsbTerminalService : IUsbTerminalService
{
    // STM32 Firmware VID(0x16C0) and PID(0x27DD)
    private const int VendorId = 0x16C0;
    private const int ProductId = 0x27DD;

    public ObservableCollection<TerminalMessage> History { get; } = [];

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (_isConnected != value)
            {
                _isConnected = value;
                StateChanged?.Invoke();
            }
        }
    }

    public event Action? StateChanged;

    private UsbManager? _usbManager;
    private UsbSerialPort? _port;
    private CancellationTokenSource? _readCts;

    public AndroidUsbTerminalService()
    {
        _usbManager = Application.Context.GetSystemService(Context.UsbService) as UsbManager;
    }

    public IEnumerable<string> GetAvailablePorts()
    {
        if (_usbManager == null) return [];

        // Use the custom prober here
        var prober = GetCustomProber();
        var availableDrivers = prober.FindAllDrivers(_usbManager);

        var ports = new List<string>();
        foreach (var driver in availableDrivers)
        {
            ports.Add(driver.Device.DeviceName);
        }

        return ports;
    }

    public async Task ConnectAsync(string portName)
    {
        if (IsConnected || _usbManager == null) return;

        try
        {
            var prober = GetCustomProber();
            var availableDrivers = prober.FindAllDrivers(_usbManager);
            var driver = availableDrivers.FirstOrDefault(d => d.Device.DeviceName == portName) ?? throw new Exception("Device not found or disconnected.");
            
            _port = driver.Ports[0];

            if (!_usbManager.HasPermission(driver.Device))
            {
                var intent = new Intent("com.isvol.app.USB_PERMISSION");
                intent.SetPackage(Application.Context.PackageName);
                var pendingIntent = PendingIntent.GetBroadcast(Application.Context, 0, intent, PendingIntentFlags.Mutable);
                _usbManager.RequestPermission(driver.Device, pendingIntent);

                History.Add(new TerminalMessage { Text = "Requested USB permission. Please accept the prompt and click Connect again.", IsSent = false });
                StateChanged?.Invoke();
                return;
            }

            var connection = _usbManager.OpenDevice(driver.Device) ?? throw new Exception("Failed to open USB device.");
            _port.Open(connection);
            _port.SetParameters(115200, 8, StopBits.One, Parity.None);

            IsConnected = true;
            History.Add(new TerminalMessage { Text = $"Connected to {portName} at 115200 baud.", IsSent = false });
            StateChanged?.Invoke();

            // We start a manual read loop just like the Windows implementation
            _readCts = new CancellationTokenSource();
            _ = Task.Run(() => ReadLoopAsync(_readCts.Token));
        }
        catch (Exception ex)
        {
            History.Add(new TerminalMessage { Text = $"Failed to connect: {ex.Message}", IsSent = false });
            StateChanged?.Invoke();
        }
    }

    public Task DisconnectAsync()
    {
        if (!IsConnected) return Task.CompletedTask;

        try
        {
            _readCts?.Cancel();
            _port?.Close();
            _port = null;

            IsConnected = false;
            History.Add(new TerminalMessage { Text = "Disconnected.", IsSent = false });
            StateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            History.Add(new TerminalMessage { Text = $"Error disconnecting: {ex.Message}", IsSent = false });
            StateChanged?.Invoke();
        }

        return Task.CompletedTask;
    }

    public Task SendCommandAsync(string command)
    {
        if (!IsConnected || _port == null) return Task.CompletedTask;

        try
        {
            History.Add(new TerminalMessage { Text = command, IsSent = true });
            StateChanged?.Invoke();

            string toSend = (!command.EndsWith("\r") && !command.EndsWith("\n")) ? command + "\r\n" : command;
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(toSend);

            _port.Write(bytes, 500); // 500ms timeout
        }
        catch (Exception ex)
        {
            History.Add(new TerminalMessage { Text = $"Failed to send: {ex.Message}", IsSent = false });
            StateChanged?.Invoke();
        }

        return Task.CompletedTask;
    }

    private void ReadLoopAsync(CancellationToken cancellationToken)
    {
        if (_port == null) return;

        byte[] buffer = new byte[1024];

        while (!cancellationToken.IsCancellationRequested && IsConnected)
        {
            try
            {
                // Synchronous read with a 200ms timeout on a background thread.
                int bytesRead = _port.Read(buffer, 200);

                if (bytesRead > 0)
                {
                    string data = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    // Dispatch to MainThread to update UI safely
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        History.Add(new TerminalMessage { Text = data, IsSent = false });
                        StateChanged?.Invoke();
                    });
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Timeout exceptions are perfectly normal if no data arrives during the 200ms window
                if (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                    continue;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    History.Add(new TerminalMessage { Text = $"Read error: {ex.Message}", IsSent = false });
                    StateChanged?.Invoke();
                    _ = DisconnectAsync();
                });
                break;
            }
        }
    }

    private static UsbSerialProber GetCustomProber()
    {
        // Start with the default table so standard chips still work
        var probeTable = UsbSerialProber.DefaultProbeTable;

        // Map it to the standard CDC ACM driver class
        probeTable.AddProduct(VendorId, ProductId, typeof(CdcAcmSerialDriver));

        return new UsbSerialProber(probeTable);
    }
}
#endif