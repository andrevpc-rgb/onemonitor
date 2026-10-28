using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Infrastructure.Settings;
using Xunit;

namespace OneMonitor.Agent.Tests;

public class FileWorkPolicyProviderTests : IDisposable
{
    private readonly string _filePath = Path.Combine(Path.GetTempPath(), $"onemonitor-test-{Guid.NewGuid():N}.json");

    [Fact]
    public void Current_WhenNoFileExists_ReturnsTheFallback()
    {
        var fallback = OrganizationSettings.Default;
        var provider = new FileWorkPolicyProvider(_filePath, fallback);

        Assert.Equal(fallback, provider.Current);
    }

    [Fact]
    public void Update_PersistsToDisk_AndANewInstanceLoadsIt()
    {
        var provider = new FileWorkPolicyProvider(_filePath, OrganizationSettings.Default);
        var updated = new OrganizationSettings(new TimeOnly(9, 0), new TimeOnly(17, 30), false, 20);

        provider.Update(updated);

        Assert.Equal(updated, provider.Current);
        Assert.True(File.Exists(_filePath));

        var reloaded = new FileWorkPolicyProvider(_filePath, OrganizationSettings.Default);
        Assert.Equal(updated, reloaded.Current);
    }

    [Fact]
    public void Constructor_WithCorruptedFile_FallsBackWithoutThrowing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, "{ isto não é json válido");

        var fallback = OrganizationSettings.Default;
        var exception = Record.Exception(() => new FileWorkPolicyProvider(_filePath, fallback));

        Assert.Null(exception);
        Assert.Equal(fallback, new FileWorkPolicyProvider(_filePath, fallback).Current);
    }

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
