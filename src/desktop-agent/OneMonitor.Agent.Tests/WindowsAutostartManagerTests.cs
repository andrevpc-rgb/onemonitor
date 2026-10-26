using Microsoft.Win32;
using OneMonitor.Agent.Core.Abstractions;
using OneMonitor.Agent.Infrastructure.Windows;
using Xunit;

namespace OneMonitor.Agent.Tests;

/// <summary>
/// Testa contra o HKCU de verdade (é por usuário, não exige administrador) — usa um nome de
/// valor dedicado para os testes, nunca "OneMonitor", e limpa no Dispose mesmo se um teste falhar.
/// </summary>
public class WindowsAutostartManagerTests : IDisposable
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string _testValueName = $"OneMonitorTests_{Guid.NewGuid():N}";

    [Fact]
    public void IsEnabled_WhenNoRegistryValueExists_ReturnsFalse()
    {
        var manager = CreateManager();

        Assert.False(manager.IsEnabled);
    }

    [Fact]
    public void Enable_CreatesRegistryValueWithTheGivenCommand()
    {
        var manager = CreateManager(() => "\"C:\\OneMonitor\\OneMonitor.Agent.exe\"");

        manager.Enable();

        Assert.True(manager.IsEnabled);
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        Assert.Equal("\"C:\\OneMonitor\\OneMonitor.Agent.exe\"", key?.GetValue(_testValueName));
    }

    [Fact]
    public void Disable_RemovesTheRegistryValue()
    {
        var manager = CreateManager();
        manager.Enable();
        Assert.True(manager.IsEnabled);

        manager.Disable();

        Assert.False(manager.IsEnabled);
    }

    [Fact]
    public void Disable_WhenNotEnabled_DoesNotThrow()
    {
        var manager = CreateManager();

        var exception = Record.Exception(() => manager.Disable());

        Assert.Null(exception);
    }

    [Fact]
    public void SyncTo_WithTrueAndNotEnabled_Enables()
    {
        var manager = CreateManager();

        manager.SyncTo(true);

        Assert.True(manager.IsEnabled);
    }

    [Fact]
    public void SyncTo_WithFalseAndEnabled_Disables()
    {
        var manager = CreateManager();
        manager.Enable();

        manager.SyncTo(false);

        Assert.False(manager.IsEnabled);
    }

    [Fact]
    public void SyncTo_IsIdempotent_DoesNotThrowWhenCalledRepeatedlyWithSameValue()
    {
        var manager = CreateManager();

        var exception = Record.Exception(() =>
        {
            manager.SyncTo(true);
            manager.SyncTo(true);
            manager.SyncTo(false);
            manager.SyncTo(false);
        });

        Assert.Null(exception);
        Assert.False(manager.IsEnabled);
    }

    [Fact]
    public void BuildAutostartCommand_WhenProcessIsDotnet_WrapsDllPath()
    {
        var command = WindowsAutostartManager.BuildAutostartCommand(
            @"C:\Program Files\dotnet\dotnet.exe",
            @"C:\OneMonitor\bin");

        Assert.Equal("\"C:\\Program Files\\dotnet\\dotnet.exe\" \"C:\\OneMonitor\\bin\\OneMonitor.Agent.dll\"", command);
    }

    [Fact]
    public void BuildAutostartCommand_WhenProcessIsThePublishedExe_UsesItDirectly()
    {
        var command = WindowsAutostartManager.BuildAutostartCommand(
            @"C:\OneMonitor\OneMonitor.Agent.exe",
            @"C:\OneMonitor");

        Assert.Equal("\"C:\\OneMonitor\\OneMonitor.Agent.exe\"", command);
    }

    private IAutostartManager CreateManager(Func<string>? commandProvider = null) =>
        new WindowsAutostartManager(_testValueName, commandProvider ?? (() => "test-command"));

    public void Dispose()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(_testValueName, throwOnMissingValue: false);
    }
}
