namespace OneMonitor.Agent.Core.Models;

public sealed record AppUsage(string ProcessName, TimeSpan Duration);

/// <summary>
/// Tempo ativo em uma janela específica (ex.: uma aba/site do navegador, identificada
/// pelo título da janela). Permite diferenciar sites dentro do mesmo processo (chrome, etc).
/// </summary>
public sealed record WindowUsage(string ProcessName, string WindowTitle, TimeSpan Duration);

public sealed record ReportData(
    DateOnly Date,
    IReadOnlyList<AppUsage> AppUsages,
    IReadOnlyList<WindowUsage> WindowUsages,
    TimeSpan TotalActive,
    TimeSpan TotalIdle,
    TimeSpan TotalUnclassified)
{
    public TimeSpan TotalTracked => TotalActive + TotalIdle + TotalUnclassified;
}
