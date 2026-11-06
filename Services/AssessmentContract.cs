using System.Text.Json;
using AiTeachingAssistant.Models;

namespace AiTeachingAssistant.Services;

public static class AssessmentContract
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    public static string BuildInput(string answer, string? before, string? caseFacts) =>
        JsonSerializer.Serialize(new
        {
            caseFacts = string.IsNullOrWhiteSpace(caseFacts) ? null : caseFacts.Trim(),
            answers = before is null
                ? new[] { new { version = "current", text = answer } }
                : new[] { new { version = "before", text = before }, new { version = "after", text = answer } }
        }, JsonOptions);

    public static JsonElement Schema(bool compare, bool? caseAvailable = null)
    {
        object Text() => new { type = "string", minLength = 1 };
        object Obj(Dictionary<string, object> properties) => new
        {
            type = "object", properties, required = properties.Keys.ToArray(), additionalProperties = false
        };
        var finding = Obj(new() { ["claim"] = Text(), ["evidence"] = Text(), ["explanation"] = Text() });
        var fields = new Dictionary<string, object>
        {
            ["version"] = new { type = "string", @enum = compare ? new[] { "before", "after" } : new[] { "current" } },
            ["caseEvidenceAvailable"] = caseAvailable.HasValue
                ? new { type = "boolean", @enum = new[] { caseAvailable.Value } } : (object)new { type = "boolean" }
        };
        foreach (var name in new[] { "supportedClaims", "partiallySupportedClaims", "unsupportedClaims", "unverifiedClaims", "importantOmissions" })
            fields[name] = caseAvailable == false && name is "supportedClaims" or "partiallySupportedClaims" or "importantOmissions"
                ? new { type = "array", items = finding, maxItems = 0 } : (object)new { type = "array", items = finding };
        fields["reasoningQuality"] = Text();
        fields["score"] = new { type = "integer", minimum = 1, maximum = 4 };
        fields["justification"] = Text();
        return JsonSerializer.SerializeToElement(Obj(new()
        {
            ["evaluations"] = new { type = "array", minItems = compare ? 2 : 1, maxItems = compare ? 2 : 1, items = Obj(fields) },
            ["comparisonFeedback"] = compare ? new { type = "string", minLength = 1 } : (object)new { type = "string", @enum = new[] { "" } }
        }));
    }
}
