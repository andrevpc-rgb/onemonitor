using System.Text.Json;
using System.Text.Json.Nodes;

namespace OneMonitor.Agent;

/// <summary>
/// Configuração carregada de appsettings.json, com defaults que priorizam o
/// data/ do repositório em ambiente de desenvolvimento e caem para
/// %LOCALAPPDATA%\OneMonitor fora de um checkout de desenvolvimento.
///
/// Um appsettings.Local.json opcional (gitignorado) sobrepõe campos individuais do
/// appsettings.json versionado — útil para segredos (ex.: Sync.ApiKey) que não devem
/// ir para um repositório público. Veja appsettings.Local.json.example.
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

    public SyncSettings Sync { get; set; } = new();

    public AutostartSettings Autostart { get; set; } = new();

    public string ResolvedCategoriesConfigPath =>
        !string.IsNullOrWhiteSpace(CategoriesConfigPath)
            ? CategoriesConfigPath
            : Path.Combine(AppContext.BaseDirectory, "categories.json");

    public string ResolvedDeviceIdFilePath => Path.Combine(ResolveDataRoot(), "device-id.txt");

    /// <summary>
    /// Cache local da última OrganizationSettings recebida do backend (ver
    /// FileWorkPolicyProvider) — sobrevive a reinícios sem precisar de rede.
    /// </summary>
    public string ResolvedOrganizationSettingsFilePath => Path.Combine(ResolveDataRoot(), "organization-settings.json");

    public string ResolvedDatabasePath =>
        !string.IsNullOrWhiteSpace(DatabasePath)
            ? DatabasePath
            : Path.Combine(ResolveDataRoot(), "onemonitor.db");

    public string ResolvedReportOutputDirectory =>
        !string.IsNullOrWhiteSpace(ReportOutputDirectory)
            ? ReportOutputDirectory
            : Path.Combine(ResolveDataRoot(), "reports");

    // Permite "// comentários" nos arquivos de configuração (ex.: appsettings.Local.json.example,
    // que é pensado pra ser copiado direto) sem que isso derrube o parse silenciosamente.
    private static readonly JsonDocumentOptions JsonParseOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static AppSettings Load()
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var localPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json");

        try
        {
            JsonObject merged = File.Exists(basePath)
                ? (JsonNode.Parse(File.ReadAllText(basePath), documentOptions: JsonParseOptions) as JsonObject ?? new JsonObject())
                : new JsonObject();

            if (File.Exists(localPath))
            {
                var local = JsonNode.Parse(File.ReadAllText(localPath), documentOptions: JsonParseOptions) as JsonObject;
                MergeInto(merged, local);
            }

            return merged.Deserialize<AppSettings>() ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    /// <summary>Mescla "source" em "target", recursivamente para objetos aninhados (ex.: "Sync").</summary>
    private static void MergeInto(JsonObject target, JsonObject? source)
    {
        if (source is null)
        {
            return;
        }

        foreach (var property in source)
        {
            if (property.Value is JsonObject sourceChild && target[property.Key] is JsonObject targetChild)
            {
                MergeInto(targetChild, sourceChild);
            }
            else
            {
                target[property.Key] = property.Value?.DeepClone();
            }
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

/// <summary>
/// Configuração da sincronização com um backend. Desabilitada por padrão — não há backend
/// nesta fase do projeto; o agente continua 100% funcional offline sem ela.
/// </summary>
public class SyncSettings
{
    public bool Enabled { get; set; }

    /// <summary>URL base do backend (ex.: "https://api.onemonitor.exemplo.com/"). Precisa terminar com "/".</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>Chave/token enviado no header de autenticação de cada requisição.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public int BatchSize { get; set; } = 50;

    public int SyncIntervalSeconds { get; set; } = 60;

    public int MaxRetries { get; set; } = 3;
}

/// <summary>Controla se o agente se registra para iniciar junto com o Windows. Desabilitado por padrão.</summary>
public class AutostartSettings
{
    public bool Enabled { get; set; }
}
