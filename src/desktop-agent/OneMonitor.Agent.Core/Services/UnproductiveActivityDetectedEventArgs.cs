namespace OneMonitor.Agent.Core.Services;

/// <summary>Disparado quando uma nova atividade Ativa classificada como Improdutiva é aberta.</summary>
public sealed class UnproductiveActivityDetectedEventArgs : EventArgs
{
    public UnproductiveActivityDetectedEventArgs(string processName, string windowTitle)
    {
        ProcessName = processName;
        WindowTitle = windowTitle;
    }

    public string ProcessName { get; }

    public string WindowTitle { get; }
}
