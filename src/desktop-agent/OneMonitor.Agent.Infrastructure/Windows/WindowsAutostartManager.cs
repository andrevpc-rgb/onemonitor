using System.Runtime.Versioning;
using Microsoft.Win32;
using OneMonitor.Agent.Core.Abstractions;

namespace OneMonitor.Agent.Infrastructure.Windows;

/// <summary>
/// Liga/desliga a inicialização automática via HKCU\Software\Microsoft\Windows\CurrentVersion\Run
/// — por usuário, sem precisar de privilégio de administrador (ao contrário de HKLM).
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsAutostartManager : IAutostartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _valueName;
    private readonly Func<string> _commandProvider;

    public WindowsAutostartManager(string valueName, Func<string> commandProvider)
    {
        _valueName = valueName;
        _commandProvider = commandProvider;
    }

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(_valueName) is not null;
        }
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(_valueName, _commandProvider(), RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(_valueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// Monta o comando a registrar a partir do processo em execução — lê o ambiente real.
    /// </summary>
    public static string BuildAutostartCommand()
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Não foi possível determinar o caminho do processo atual.");

        return BuildAutostartCommand(processPath, AppContext.BaseDirectory);
    }

    /// <summary>
    /// Lógica pura (testável sem depender do processo real em execução), cobrindo os dois
    /// modos de deployment deste projeto: build de desenvolvimento (UseAppHost=false — o
    /// processo é "dotnet.exe", então o Run precisa apontar "dotnet.exe" + o caminho da DLL)
    /// e publish self-contained (o processo já é o próprio OneMonitor.Agent.exe).
    /// </summary>
    public static string BuildAutostartCommand(string processPath, string baseDirectory)
    {
        if (string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var dllPath = Path.Combine(baseDirectory, "OneMonitor.Agent.dll");
            return $"\"{processPath}\" \"{dllPath}\"";
        }

        return $"\"{processPath}\"";
    }
}
