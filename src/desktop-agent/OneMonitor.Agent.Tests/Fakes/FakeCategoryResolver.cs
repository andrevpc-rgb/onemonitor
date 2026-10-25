using OneMonitor.Agent.Core.Abstractions;

namespace OneMonitor.Agent.Tests.Fakes;

public class FakeCategoryResolver : ICategoryResolver
{
    private readonly Dictionary<string, string> _categoryByProcess;
    private readonly string _defaultCategory;

    public FakeCategoryResolver(Dictionary<string, string>? categoryByProcess = null, string defaultCategory = "Outros")
    {
        _categoryByProcess = categoryByProcess ?? new Dictionary<string, string>();
        _defaultCategory = defaultCategory;
    }

    public string Resolve(string processName, string windowTitle) =>
        _categoryByProcess.TryGetValue(processName, out var category) ? category : _defaultCategory;
}
