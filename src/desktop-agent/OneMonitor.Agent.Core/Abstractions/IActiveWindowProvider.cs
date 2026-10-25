using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Core.Abstractions;

/// <summary>
/// Lê apenas metadados da janela em primeiro plano (processo + título).
/// Implementações nunca devem ler o conteúdo da janela.
/// </summary>
public interface IActiveWindowProvider
{
    ActiveWindowSnapshot? GetActiveWindow();
}
