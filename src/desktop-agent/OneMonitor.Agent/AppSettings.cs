using System.Text.Json;

namespace OneMonitor.Agent;

/// <summary>
/// Configuração carregada de appsettings.json, com defaults que priorizam o
/// data/ do repositório em ambiente de desenvolvimento e caem para
/// %LOCALAPPDATA%\OneMonitor fora de um checkout de desenvolvimento.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Limite de inatividade em minutos. Aceita frações para testes (ex.: 0.25 = 15s,
    /// 0.0833... ≈ 5s) — em produção, use um valor inteiro (padrão: 5).
    /// </summary>
    public double InactivityThresholdMinutes { get; set; } = 5;

    public int PollingIntervalSeconds { get; set; } = 2;

    public string? DatabasePath { get; set; }

    public string? ReportOutputDirectory { get; set; }

    public string? CategoriesConfigPath { get; set; }

    public string ResolvedCategoriesConfigPath =>
        !string.IsNullOrWhiteSpace(CategoriesConfigPath)
            ? CategoriesConfigPath
            : Path.Combine(AppContext.BaseDirectory, "categories.json");

    public string ResolvedDatabasePath =>
        !string.IsNullOrWhiteSpace(DatabasePath)
            ? DatabasePath
            : Path.Combine(ResolveDataRoot(), "onemonitor.db");

    public string ResolvedReportOutputDirectory =>
        !string.IsNullOrWhiteSpace(ReportOutputDirectory)
            ? ReportOutputDirectory
            : Path.Combine(ResolveDataRoot(), "reports");

    public static AppSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    private static string ResolveDataRoot()
    {
        var repoRoot = FindRepositoryRoot();
        if (repoRoot is not null)
        {
            return Path.Combine(repoRoot, "data");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OneMonitor");
    }

    /// <summary>
    /// Procura o OneMonitor.Agent.sln subindo a partir do diretório de execução para
    /// localizar a raiz do repositório durante o desenvolvimento. Retorna null quando
    /// não encontrado (ex.: binário publicado fora do checkout).
    /// </summary>
    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OneMonitor.Agent.sln")))
            {
                // directory == src/desktop-agent -> dois níveis acima é a raiz do repositório.
                return directory.Parent?.Parent?.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
