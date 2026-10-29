using OneMonitor.Agent.Core.Services;
using Xunit;

namespace OneMonitor.Agent.Tests;

public class DeviceNameProviderTests
{
    [Fact]
    public void GetFriendlyName_CombinesMachineNameAndUserName()
    {
        var name = DeviceNameProvider.GetFriendlyName();

        Assert.Equal($"{Environment.MachineName} ({Environment.UserName})", name);
    }
}
