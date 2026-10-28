using OneMonitor.Agent.Core.Abstractions;
using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Core.Services;

/// <summary>
/// Guarda a política só em memória — usado como default de <see cref="ActivityTracker"/>
/// quando nenhum provider é passado (ex.: testes que não se importam com a política de
/// expediente) e como base de <see cref="OneMonitor.Agent.Infrastructure.Settings.FileWorkPolicyProvider"/>.
/// </summary>
public class StaticWorkPolicyProvider : IWorkPolicyProvider
{
    public StaticWorkPolicyProvider(OrganizationSettings initial) => Current = initial;

    public OrganizationSettings Current { get; private set; }

    public void Update(OrganizationSettings settings) => Current = settings;
}
