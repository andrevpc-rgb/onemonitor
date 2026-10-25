using System.Text.Json;
using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Core.Services;

namespace OneMonitor.Agent.Infrastructure.Categorization;

/// <summary>
/// Lê categories.json e monta um <see cref="RuleBasedCategoryResolver"/>. Toda a lógica de
/// match fica no Core (sem I/O); esta classe só traduz o JSON para os modelos de domínio.
/// </summary>
public static class CategoryRulesLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static RuleBasedCategoryResolver LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            return new RuleBasedCategoryResolver(Array.Empty<CategoryDefinition>());
        }

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<CategoryConfigFile>(json, JsonOptions);
        if (config is null)
        {
            return new RuleBasedCategoryResolver(Array.Empty<CategoryDefinition>());
        }

        var categories = config.Categories
            .Select(c => new CategoryDefinition(
                c.Name,
                c.Rules.Select(r => new CategoryRule(r.ProcessName, r.WindowTitleContains)).ToList()))
            .ToList();

        return new RuleBasedCategoryResolver(categories, config.DefaultCategory ?? "Outros");
    }
}
