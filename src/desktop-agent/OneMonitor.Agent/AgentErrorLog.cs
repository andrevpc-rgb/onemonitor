namespace OneMonitor.Agent;

/// <summary>
/// Log de erros, melhor esforço — não havia nenhuma infraestrutura de logging no projeto até
/// aqui. Existe para o caso que motivou esta classe: em produção, um ciclo de
/// <see cref="OneMonitor.Agent.Core.Services.ActivityTracker.Tick"/> começou a falhar
/// silenciosamente (o processo continuava "Responding" no Gerenciador de Tarefas — o ícone da
/// bandeja respondia normalmente — mas nenhuma atividade nova era gravada nem sincronizada por
/// horas) e não havia nenhum rastro para diagnosticar o que aconteceu. Sem isso, o único
/// sintoma visível é "o dispositivo está offline no painel", que não diz nada sobre a causa.
/// </summary>
internal static class AgentErrorLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OneMonitor", "agent-error.log");

    public static void Record(string context, Exception ex)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Evita crescimento sem limite: reinicia o arquivo se passar de ~1 MB, em vez de
            // implementar rotação de verdade — este log é só para diagnóstico pontual, não
            // para auditoria de longo prazo.
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 1_000_000)
            {
                File.Delete(LogPath);
            }

            File.AppendAllText(
                LogPath,
                $"{DateTimeOffset.UtcNow:O} [{context}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Melhor esforço: um log que falha não pode derrubar o agente nem mascarar o erro
            // original (que já foi tratado por quem chamou Record).
        }
    }
}
