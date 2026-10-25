namespace OneMonitor.Agent.Infrastructure.Categorization;

/// <summary>Formas de serialização JSON de categories.json, separadas dos modelos de domínio.</summary>
internal sealed class CategoryRuleDto
{
    public string ProcessName { get; set; } = string.Empty;

    public string? WindowTitleContains { get; set; }
}

internal sealed class CategoryDto
{
    public string Name { get; set; } = string.Empty;

    public List<CategoryRuleDto> Rules { get; set; } = new();
}

internal sealed class CategoryConfigFile
{
    public string? DefaultCategory { get; set; }

    public List<CategoryDto> Categories { get; set; } = new();
}
