using System.Collections.ObjectModel;
using AlarmsTuner.Models;

namespace AlarmsTuner.Services;

public interface IUsbTerminalService
{
    ObservableCollection<TerminalMessage> History { get; }
    bool IsConnected { get; }
    
    event Action? StateChanged;

    Task ConnectAsync(string port);
    Task DisconnectAsync();
    Task SendCommandAsync(string command);
    IEnumerable<string> GetAvailablePorts();
}
