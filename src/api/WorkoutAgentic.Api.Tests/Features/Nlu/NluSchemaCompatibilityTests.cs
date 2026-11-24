using System.Text.Json.Nodes;
using WorkoutAgentic.Api.Features.Nlu;

namespace WorkoutAgentic.Api.Tests.Features.Nlu;

public sealed class NluSchemaCompatibilityTests
{
    [Fact]
    public void ProviderSchema_UsesRequiredNullableKeysWithoutExpandingNumericOrArrayBounds()
    {
        // Regression: Gemini rejected the deployed schema with 400 INVALID_ARGUMENT
        // (too many states). Bounds remain enforced by the capture service.
        CheckCompatibility(NluSchema.ComoJson());
    }

    [Fact]
    public void ProviderSchema_AbsentTrainingValuesRemainNullableIncludingOneRepMax()
    {
        var schema = NluSchema.ComoJson();
        var root = schema["properties"]!;
        var detail = root["series"]!["items"]!["properties"]!;
        foreach (var name in new[] { "repeticiones", "peso", "unidad", "comentarios", "observaciones", "es_rm" })
        {
            Assert.True(root[name]!["nullable"]!.GetValue<bool>());
            Assert.True(detail[name]!["nullable"]!.GetValue<bool>());
        }
    }

    private static void CheckCompatibility(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var bound in new[] { "maxItems", "minItems", "minimum", "maximum" })
                Assert.False(obj.ContainsKey(bound), $"Avoid {bound}: it expands the provider's generation states.");
            if (obj["properties"] is JsonObject properties)
            {
                var required = obj["required"]!.AsArray().Select(p => p!.GetValue<string>()).Order().ToArray();
                Assert.Equal(properties.Select(p => p.Key).Order().ToArray(), required);
            }
            foreach (var property in obj) CheckCompatibility(property.Value);
        }
        else if (node is JsonArray array)
            foreach (var item in array) CheckCompatibility(item);
    }
}
