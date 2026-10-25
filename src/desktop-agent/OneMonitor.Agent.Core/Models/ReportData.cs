namespace OneMonitor.Agent.Core.Models;

public sealed record AppUsage(string ProcessName, TimeSpan Duration);

/// <summary>
/// Tempo ativo em uma janela específica (ex.: uma aba/site do navegador, identificada
/// pelo título da janela). Permite diferenciar sites dentro do mesmo processo (chrome, etc).
/// </summary>
public sealed record WindowUsage(string ProcessName, string WindowTitle, TimeSpan Duration);

/// <summary>Tempo ativo agregado por categoria configurável (ex.: "Desenvolvimento").</summary>
public sealed record CategoryUsage(string Category, TimeSpan Duration);

/// <summary>Período(s) de inatividade justificados pelo usuário, agregados por motivo.</summary>
public sealed record JustifiedAbsence(string Justification, TimeSpan Duration, int Occurrences);

/// <param name="Timeline">Todos os registros do dia, em ordem cronológica (horário real de início/fim de cada bloco).</param>
public sealed record ReportData(
    DateOnly Date,
    IReadOnlyList<AppUsage> AppUsages,
    IReadOnlyList<WindowUsage> WindowUsages,
    IReadOnlyList<CategoryUsage> CategoryUsages,
    IReadOnlyList<JustifiedAbsence> JustifiedAbsences,
    IReadOnlyList<ActivityRecord> Timeline,
    TimeSpan TotalActive,
    TimeSpan TotalIdle,
    TimeSpan TotalJustified,
    TimeSpan TotalUnclassified)
{
    public TimeSpan TotalTracked => TotalActive + TotalIdle + TotalUnclassified;
}
