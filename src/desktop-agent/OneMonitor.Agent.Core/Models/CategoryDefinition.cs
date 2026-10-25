namespace OneMonitor.Agent.Core.Models;

/// <summary>
/// Uma regra de categorização: casa pelo nome do processo (obrigatório, case-insensitive)
/// e, opcionalmente, por um trecho contido no título da janela (case-insensitive).
/// </summary>
public sealed record CategoryRule(string ProcessName, string? WindowTitleContains = null);

/// <summary>
/// Uma categoria configurável (ex.: "Desenvolvimento") e as regras que levam a ela.
/// Regras mais específicas devem estar em categorias declaradas antes das genéricas,
/// já que a primeira categoria cuja regra bater vence (ver <see cref="OneMonitor.Agent.Core.Services.RuleBasedCategoryResolver"/>).
/// </summary>
public sealed record CategoryDefinition(string Name, IReadOnlyList<CategoryRule> Rules);
