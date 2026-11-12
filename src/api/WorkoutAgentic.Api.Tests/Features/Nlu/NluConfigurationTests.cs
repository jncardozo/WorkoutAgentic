using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WorkoutAgentic.Api.Features.Nlu;

namespace WorkoutAgentic.Api.Tests.Features.Nlu;

public sealed class NluConfigurationTests
{
    [Theory]
    [InlineData(null, "alias-key", "alias-key")]
    [InlineData("", "alias-key", "alias-key")]
    [InlineData("  ", "alias-key", "alias-key")]
    [InlineData("section-key", "alias-key", "section-key")]
    [InlineData("section-key", null, "section-key")]
    [InlineData(null, null, "")]
    public void ResolveOptions_UsesAliasOnlyWithoutSectionKey(string? section, string? alias, string expected)
    {
        // Same keys produced by .NET environment configuration; no real process
        // variables or credentials are changed by the test.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = section,
            ["GEMINI_API_KEY"] = alias,
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddNluCaptura();
        using var provider = services.BuildServiceProvider();

        Assert.Equal(expected, provider.GetRequiredService<IOptions<GeminiOptions>>().Value.ApiKey);
    }
}
