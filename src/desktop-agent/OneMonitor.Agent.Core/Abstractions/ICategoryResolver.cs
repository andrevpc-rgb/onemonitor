namespace OneMonitor.Agent.Core.Abstractions;

/// <summary>Resolve a categoria de um processo/janela (ex.: "Desenvolvimento", "Navegação").</summary>
public interface ICategoryResolver
{
    string Resolve(string processName, string windowTitle);
}
