using AlarmsTuner.Models;
using System.Collections.Concurrent;

namespace AlarmsTuner.Services;

public interface IUsbTerminalService
{
    ConcurrentQueue<TerminalMessage> History { get; }
    bool IsConnected { get; }
    
    event Action? StateChanged;

    Task ConnectAsync(string port);
    Task DisconnectAsync();
    Task SendCommandAsync(string command);
    IEnumerable<string> GetAvailablePorts();
}
