using OneMonitor.Agent.Core.Abstractions;
using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Core.Services;

/// <summary>
/// Resolve categoria por uma lista ordenada de <see cref="CategoryDefinition"/>: percorre as
/// categorias na ordem recebida e, dentro de cada uma, suas regras; a primeira regra que bater
/// decide a categoria. Não faz nenhuma I/O — o carregamento do arquivo de configuração fica a
/// cargo da camada de infraestrutura.
/// </summary>
public class RuleBasedCategoryResolver : ICategoryResolver
{
    private readonly IReadOnlyList<CategoryDefinition> _categories;
    private readonly string _defaultCategory;

    public RuleBasedCategoryResolver(IReadOnlyList<CategoryDefinition> categories, string defaultCategory = "Outros")
    {
        _categories = categories;
        _defaultCategory = defaultCategory;
    }

    public string Resolve(string processName, string windowTitle)
    {
        foreach (var category in _categories)
        {
            foreach (var rule in category.Rules)
            {
                if (!string.Equals(rule.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (rule.WindowTitleContains is not null &&
                    !windowTitle.Contains(rule.WindowTitleContains, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return category.Name;
            }
        }

        return _defaultCategory;
    }
}
