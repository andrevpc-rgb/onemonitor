using OneMonitor.Agent.Core.Models;
using OneMonitor.Agent.Core.Services;
using Xunit;

namespace OneMonitor.Agent.Tests;

public class ProductivityClassifierTests
{
    [Theory]
    [InlineData("chrome", "YouTube - Google Chrome")]
    [InlineData("Spotify", "Tocando agora")]
    public void Classify_ReturnsUnproductive_ForDefaultKeywords(string processName, string windowTitle)
    {
        Assert.Equal(ProductivityClass.Unproductive, ProductivityClassifier.Classify(processName, windowTitle, null));
    }

    [Theory]
    [InlineData("EXCEL", "planilha.xlsx")]
    [InlineData("chrome", "e-CAC - Google Chrome")]
    public void Classify_ReturnsProductive_ForDefaultKeywords(string processName, string windowTitle)
    {
        Assert.Equal(ProductivityClass.Productive, ProductivityClassifier.Classify(processName, windowTitle, null));
    }

    [Fact]
    public void Classify_ReturnsNeutral_WhenNothingMatches()
    {
        Assert.Equal(ProductivityClass.Neutral, ProductivityClassifier.Classify("devenv", "Solution - Visual Studio", null));
    }

    [Fact]
    public void Classify_OrganizationRule_TakesPriorityOverDefaultKeyword()
    {
        // "Claude" não bate em nenhuma palavra-chave padrão; a organização decidiu que é produtivo.
        var rules = new[] { new ProductivityRuleDto("claude", "productive") };

        Assert.Equal(
            ProductivityClass.Productive,
            ProductivityClassifier.Classify("chrome", "Claude - Google Chrome", rules));
    }

    [Fact]
    public void Classify_OrganizationRule_CanOverrideDefaultUnproductiveKeyword()
    {
        // A organização decidiu que Spotify é produtivo para ela (ex.: equipe de música).
        var rules = new[] { new ProductivityRuleDto("spotify", "productive") };

        Assert.Equal(
            ProductivityClass.Productive,
            ProductivityClassifier.Classify("Spotify", "Tocando agora", rules));
    }

    [Fact]
    public void Classify_UnproductiveOrganizationRule_TakesPriorityOverProductiveRule()
    {
        // Mesma prioridade do TS (classifyProductivity): improdutivo é checado antes de produtivo.
        var rules = new[]
        {
            new ProductivityRuleDto("app", "productive"),
            new ProductivityRuleDto("jogo", "unproductive"),
        };

        Assert.Equal(
            ProductivityClass.Unproductive,
            ProductivityClassifier.Classify("app-jogo", "", rules));
    }

    [Fact]
    public void Classify_IsCaseInsensitive()
    {
        Assert.Equal(ProductivityClass.Unproductive, ProductivityClassifier.Classify("Chrome", "YOUTUBE - Google Chrome", null));
    }
}
