namespace OneMonitor.Agent.Core.Models;

/// <summary>
/// Parâmetros de expediente e do pop-up de justificativa, definidos pela organização na
/// tela /settings do dashboard web e devolvidos ao agente no payload de resposta do
/// heartbeat de sync (ver HttpSyncApiClient e docs/supabase_schema.sql ->
/// organization_settings). Só os campos que o agente de fato usa — mandatory/variable
/// break minutes existem no banco para o cálculo de "Jornada Líquida" do dashboard web,
/// não para o agente.
/// </summary>
public sealed record OrganizationSettings(
    TimeOnly WorkStartTime,
    TimeOnly WorkEndTime,
    bool EnableIdlePopup,
    int PopupIdleThresholdMinutes,
    string? OrganizationName = null,
    string? LogoUrl = null,
    IReadOnlyList<ProductivityRuleDto>? ProductivityRules = null)
{
    /// <summary>
    /// Política usada quando não há organização (sincronização desabilitada) ou antes do
    /// primeiro heartbeat bem-sucedido ocorrer — sem restrição de horário/dia, preservando
    /// o comportamento do pop-up de justificativa de antes desta política existir.
    /// </summary>
    public static OrganizationSettings Unrestricted { get; } =
        new(TimeOnly.MinValue, TimeOnly.MaxValue, true, 0);

    /// <summary>Fallback razoável quando a organização existe mas ainda não configurou nada.</summary>
    public static OrganizationSettings Default { get; } =
        new(new TimeOnly(8, 0), new TimeOnly(18, 0), true, 15);
}
