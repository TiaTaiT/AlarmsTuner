namespace AlarmsTuner.Models;

public class TerminalMessage
{
    public string Text { get; set; } = string.Empty;
    public bool IsSent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
