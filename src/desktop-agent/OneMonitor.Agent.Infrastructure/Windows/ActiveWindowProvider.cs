using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using OneMonitor.Agent.Core.Abstractions;
using OneMonitor.Agent.Core.Models;

namespace OneMonitor.Agent.Infrastructure.Windows;

/// <summary>
/// Lê apenas o nome do processo e o título da janela em primeiro plano via
/// GetForegroundWindow/GetWindowText/GetWindowThreadProcessId. Nunca lê o conteúdo da janela.
/// </summary>
[SupportedOSPlatform("windows")]
public class ActiveWindowProvider : IActiveWindowProvider
{
    public ActiveWindowSnapshot? GetActiveWindow()
    {
        var handle = NativeMethods.GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        var title = ReadWindowTitle(handle);

        NativeMethods.GetWindowThreadProcessId(handle, out var processId);
        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return new ActiveWindowSnapshot(process.ProcessName, title);
        }
        catch (ArgumentException)
        {
            // Processo já terminou entre a leitura do handle e a consulta.
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string ReadWindowTitle(IntPtr handle)
    {
        var length = NativeMethods.GetWindowTextLength(handle);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new StringBuilder(length + 1);
        NativeMethods.GetWindowText(handle, buffer, buffer.Capacity);
        return buffer.ToString();
    }
}
