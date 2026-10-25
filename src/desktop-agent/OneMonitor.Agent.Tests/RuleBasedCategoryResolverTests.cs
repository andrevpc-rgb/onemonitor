using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Core.Services;
using Xunit;

namespace OneMonitor.Agent.Tests;

public class RuleBasedCategoryResolverTests
{
    [Fact]
    public void Resolve_MatchesByProcessName_CaseInsensitive()
    {
        var resolver = new RuleBasedCategoryResolver(new List<CategoryDefinition>
        {
            new("Desenvolvimento", new List<CategoryRule> { new("devenv") })
        });

        Assert.Equal("Desenvolvimento", resolver.Resolve("DevEnv", "Solution - Visual Studio"));
    }

    [Fact]
    public void Resolve_FallsBackToDefaultCategory_WhenNoRuleMatches()
    {
        var resolver = new RuleBasedCategoryResolver(new List<CategoryDefinition>
        {
            new("Desenvolvimento", new List<CategoryRule> { new("devenv") })
        }, defaultCategory: "Outros");

        Assert.Equal("Outros", resolver.Resolve("notepad", "untitled - Notepad"));
    }

    [Fact]
    public void Resolve_WindowTitleContains_IsCaseInsensitiveSubstringMatch()
    {
        var resolver = new RuleBasedCategoryResolver(new List<CategoryDefinition>
        {
            new("Desenvolvimento", new List<CategoryRule> { new("chrome", "github") })
        });

        Assert.Equal("Desenvolvimento", resolver.Resolve("chrome", "andrevpc/onemonitor - GitHub"));
    }

    [Fact]
    public void Resolve_RuleWithWindowTitleFilter_DoesNotMatchWhenTitleDiffers()
    {
        var resolver = new RuleBasedCategoryResolver(new List<CategoryDefinition>
        {
            new("Desenvolvimento", new List<CategoryRule> { new("chrome", "GitHub") })
        }, defaultCategory: "Outros");

        Assert.Equal("Outros", resolver.Resolve("chrome", "Gmail - Caixa de entrada"));
    }

    [Fact]
    public void Resolve_MoreSpecificCategoryDeclaredFirst_WinsOverGenericOne()
    {
        var resolver = new RuleBasedCategoryResolver(new List<CategoryDefinition>
        {
            new("Desenvolvimento", new List<CategoryRule> { new("chrome", "GitHub") }),
            new("Navegação", new List<CategoryRule> { new("chrome") })
        });

        Assert.Equal("Desenvolvimento", resolver.Resolve("chrome", "andrevpc/onemonitor - GitHub"));
        Assert.Equal("Navegação", resolver.Resolve("chrome", "YouTube"));
    }
}
