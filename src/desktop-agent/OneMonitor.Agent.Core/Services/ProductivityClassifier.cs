using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Core.Services;

/// <summary>
/// Porte em C# de lib/dashboard/productivityClassification.ts (classifyProductivity) — usado
/// pelo <see cref="ActivityTracker"/> para o alerta em tempo real (ver
/// ActivityTracker.UnproductiveActivityDetected), enquanto o TS classifica os mesmos dados já
/// sincronizados para os agregados históricos do dashboard. As duas listas de palavras-chave
/// abaixo precisam ser mantidas iguais às de lá — qualquer divergência faz o alerta do agente
/// e o dashboard discordarem sobre o mesmo app.
/// </summary>
public static class ProductivityClassifier
{
    private static readonly string[] UnproductiveKeywords =
    {
        "youtube", "instagram", "facebook", "tiktok", "netflix", "twitter", "x.com", "twitch",
        "spotify", "kwai"
    };

    private static readonly string[] ProductiveKeywords =
    {
        "excel", "planilha", "google sheets", "word", "powerpoint", "outlook", "e-cac", "ecac",
        "receita federal", "gov.br", "sped", "simples nacional", "sefaz", "jucesc", "e-social",
        "esocial", "contábil", "contabil", "fiscal", "erp"
    };

    private static readonly string[] NeutralKeywords =
    {
        "explorer.exe", "file explorer", "gerenciador de arquivos", "whatsapp"
    };

    /// <summary>
    /// Mesma ordem de prioridade do TS: regras da organização (improdutivo, depois
    /// produtivo, depois neutro) antes das palavras-chave padrão; sem match em nada, Neutro.
    /// </summary>
    public static ProductivityClass Classify(
        string processName, string windowTitle, IReadOnlyList<ProductivityRuleDto>? customRules)
    {
        var haystack = $"{processName} {windowTitle}".ToLowerInvariant();

        if (CustomMatch(haystack, customRules, "unproductive"))
        {
            return ProductivityClass.Unproductive;
        }
        if (CustomMatch(haystack, customRules, "productive"))
        {
            return ProductivityClass.Productive;
        }
        if (CustomMatch(haystack, customRules, "neutral"))
        {
            return ProductivityClass.Neutral;
        }

        if (Array.Exists(UnproductiveKeywords, haystack.Contains))
        {
            return ProductivityClass.Unproductive;
        }
        if (Array.Exists(ProductiveKeywords, haystack.Contains))
        {
            return ProductivityClass.Productive;
        }
        if (Array.Exists(NeutralKeywords, haystack.Contains))
        {
            return ProductivityClass.Neutral;
        }

        return ProductivityClass.Neutral;
    }

    private static bool CustomMatch(string haystack, IReadOnlyList<ProductivityRuleDto>? customRules, string targetClass)
    {
        if (customRules is null)
        {
            return false;
        }

        foreach (var rule in customRules)
        {
            if (string.Equals(rule.Classification, targetClass, StringComparison.OrdinalIgnoreCase)
                && haystack.Contains(rule.Keyword.ToLowerInvariant()))
            {
                return true;
            }
        }

        return false;
    }
}
