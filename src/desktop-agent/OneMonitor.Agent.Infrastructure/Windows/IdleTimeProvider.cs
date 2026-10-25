using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using OneMonitor.Agent.Core.Abstractions;

namespace OneMonitor.Agent.Infrastructure.Windows;

/// <summary>
/// Calcula o tempo decorrido desde o último input de teclado/mouse via GetLastInputInfo,
/// que expõe apenas um timestamp do sistema — nunca a tecla ou botão pressionado.
/// </summary>
[SupportedOSPlatform("windows")]
public class IdleTimeProvider : IIdleTimeProvider
{
    public TimeSpan GetIdleTime()
    {
        var info = new NativeMethods.LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>()
        };

        if (!NativeMethods.GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        // Aritmética sem sinal: continua correta mesmo quando o contador do Windows
        // "dá a volta" (a cada ~49.7 dias de uptime), pois ambos os valores vêm da
        // mesma base de 32 bits.
        var idleMilliseconds = unchecked((uint)Environment.TickCount - info.dwTime);
        return TimeSpan.FromMilliseconds(idleMilliseconds);
    }
}
