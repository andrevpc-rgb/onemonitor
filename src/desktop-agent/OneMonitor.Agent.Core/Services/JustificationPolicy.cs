using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Core.Services;

/// <summary>
/// Decide se o pop-up de justificativa (<see cref="JustificationPromptForm"/>, na Agent)
/// deve aparecer para um retorno de inatividade específico, de acordo com a política da
/// organização (ver <see cref="OrganizationSettings"/>). Pura e testável sem WinForms.
///
/// Note que a duração relevante (<see cref="IdleReturnedEventArgs.Duration"/>) já chega
/// corretamente limitada ao fim do expediente quando aplicável: <see cref="ActivityTracker"/>
/// fecha o registro de inatividade exatamente no fim do expediente (ver seu Tick()), então
/// um evento cujo início está dentro do expediente nunca tem duração "vazando" para a
/// madrugada/dia seguinte — não há necessidade de recortar a duração aqui também.
/// </summary>
public static class JustificationPolicy
{
    public static bool ShouldPrompt(IdleReturnedEventArgs idleReturned, OrganizationSettings settings)
    {
        if (!settings.EnableIdlePopup)
        {
            return false;
        }

        if (idleReturned.Duration < TimeSpan.FromMinutes(settings.PopupIdleThresholdMinutes))
        {
            return false;
        }

        // Horário/dia local de quem está sendo monitorado — work_start_time/work_end_time
        // são definidos em termos de relógio de parede, não UTC.
        var idleStartLocal = idleReturned.IdleStart.ToLocalTime();

        if (idleStartLocal.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return false;
        }

        var timeOfDay = TimeOnly.FromDateTime(idleStartLocal.DateTime);
        if (timeOfDay < settings.WorkStartTime || timeOfDay >= settings.WorkEndTime)
        {
            return false;
        }

        return true;
    }
}
