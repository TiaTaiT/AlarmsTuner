using AlarmsTuner.Models;
using System.Collections.Concurrent;
using System.IO.Ports;

namespace AlarmsTuner.Services;

public class UsbTerminalService : IUsbTerminalService
{
    private const int ComBaudRate = 115200;
    private const int ComDataBits = 8;
    private const StopBits ComStopBits = StopBits.One;
    private const Parity ComParity = Parity.None;
    public ConcurrentQueue<TerminalMessage> History { get; } = [];
    
    private SerialPort? _serialPort;

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

    private CancellationTokenSource? _readCts;

    public IEnumerable<string> GetAvailablePorts()
    {
        try
        {
            return SerialPort.GetPortNames();
        }
        catch (Exception ex)
        {
            // Log or handle exceptions based on platform differences
            History.Enqueue(new TerminalMessage { Text = $"Error fetching ports: {ex.Message}", IsSent = false });
            StateChanged?.Invoke();
            return Array.Empty<string>();
        }
    }

    public async Task ConnectAsync(string portName)
    {
        if (IsConnected) return;

        try
        {
            _serialPort = new SerialPort(portName, ComBaudRate, ComParity, ComDataBits, ComStopBits);
            // Configure suitable timeouts
            _serialPort.ReadTimeout = 500;
            _serialPort.WriteTimeout = 500;
            
            _serialPort.Open();

            IsConnected = true;
            History.Enqueue(new TerminalMessage { Text = $"Connected to {portName} at {ComBaudRate} baud.", IsSent = false });
            StateChanged?.Invoke();

            _readCts = new CancellationTokenSource();
            _ = Task.Run(() => ReadLoopAsync(_readCts.Token));
        }
        catch (Exception ex)
        {
            History.Enqueue(new TerminalMessage { Text = $"Failed to connect: {ex.Message}", IsSent = false });
            StateChanged?.Invoke();
        }
    }

    public Task DisconnectAsync()
    {
        if (!IsConnected) return Task.CompletedTask;

        try
        {
            _readCts?.Cancel();
            
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;
            }

            IsConnected = false;
            History.Enqueue(new TerminalMessage { Text = "Disconnected.", IsSent = false });
            StateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            History.Enqueue(new TerminalMessage { Text = $"Error disconnecting: {ex.Message}", IsSent = false });
            StateChanged?.Invoke();
        }

        return Task.CompletedTask;
    }

    public Task SendCommandAsync(string command)
    {
        if (!IsConnected || _serialPort == null || !_serialPort.IsOpen) 
            return Task.CompletedTask;

        try
        {
            History.Enqueue(new TerminalMessage { Text = command, IsSent = true });
            StateChanged?.Invoke();

            // Depending on the target device, it might expect a newline \r\n
            if (!command.EndsWith("\r") && !command.EndsWith("\n"))
            {
                _serialPort.WriteLine(command);
            }
            else
            {
                _serialPort.Write(command);
            }
        }
        catch (Exception ex)
        {
            History.Enqueue(new TerminalMessage { Text = $"Failed to send: {ex.Message}", IsSent = false });
            StateChanged?.Invoke();
        }

        return Task.CompletedTask;
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        if (_serialPort == null) return;

        byte[] buffer = new byte[1024];

        while (!cancellationToken.IsCancellationRequested && _serialPort.IsOpen)
        {
            try
            {
                if (_serialPort.BytesToRead > 0)
                {
                    int bytesRead = await _serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead > 0)
                    {
                        string data = _serialPort.Encoding.GetString(buffer, 0, bytesRead);
                        // Optional: you might want to buffer partial strings until a newline is hit for cleaner logs
                        // Here we just append whatever comes in
                        History.Enqueue(new TerminalMessage { Text = data, IsSent = false });
                        StateChanged?.Invoke();
                    }
                }
                else
                {
                    await Task.Delay(50, cancellationToken); // Prevent hot loop
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (TimeoutException)
            {
                // Ignore timeouts and continue reading logic
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    History.Enqueue(new TerminalMessage { Text = $"Read error: {ex.Message}", IsSent = false });
                    StateChanged?.Invoke();
                }
                break;
            }
        }
    }
}
