namespace OneMonitor.Agent.Core.Services;

/// <summary>Disparado quando a atividade sai de Idle para qualquer outro tipo (usuário voltou).</summary>
public sealed class IdleReturnedEventArgs : EventArgs
{
    public IdleReturnedEventArgs(long recordId, DateTimeOffset idleStart, DateTimeOffset idleEnd)
    {
        RecordId = recordId;
        IdleStart = idleStart;
        IdleEnd = idleEnd;
    }

    /// <summary>Id do registro de inatividade já persistido, usável em SetJustification.</summary>
    public long RecordId { get; }

    public DateTimeOffset IdleStart { get; }

    public DateTimeOffset IdleEnd { get; }

    public TimeSpan Duration => IdleEnd - IdleStart;
}
