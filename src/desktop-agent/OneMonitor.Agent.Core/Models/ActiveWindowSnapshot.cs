namespace OneMonitor.Agent.Core.Models;

/// <summary>
/// Metadados da janela em primeiro plano em um instante: apenas processo e título,
/// nunca conteúdo da janela.
/// </summary>
public sealed record ActiveWindowSnapshot(string ProcessName, string WindowTitle);
