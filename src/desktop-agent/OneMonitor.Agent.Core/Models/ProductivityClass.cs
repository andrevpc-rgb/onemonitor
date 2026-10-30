namespace OneMonitor.Agent.Core.Models;

/// <summary>
/// Classificação Produtivo/Neutro/Improdutivo de um app/janela — mesmo conceito de
/// lib/dashboard/productivityClassification.ts (ProductivityClass), usada lá no dashboard
/// para agregados históricos e aqui no agente para o alerta em tempo real (ver
/// ActivityTracker.UnproductiveActivityDetected).
/// </summary>
public enum ProductivityClass
{
    Productive,
    Neutral,
    Unproductive
}

/// <summary>
/// Palavra-chave -> classificação definida pela organização (ver organization_productivity_rules
/// em docs/supabase_schema.sql, editável em /productivity-rules), devolvida ao agente no
/// heartbeat de sync como parte de <see cref="OrganizationSettings.ProductivityRules"/> e
/// cacheada localmente pelo mesmo mecanismo (FileWorkPolicyProvider). Classification chega
/// como texto ("productive"/"neutral"/"unproductive"), não o enum — mesmo motivo do Type em
/// ActivitySyncEvent (ver SyncModels.cs): estabilidade do contrato JSON independente de como
/// o enum C# evoluir.
/// </summary>
public sealed record ProductivityRuleDto(string Keyword, string Classification);
