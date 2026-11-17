using System.ComponentModel.DataAnnotations;

namespace AiTeachingAssistant.Options;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";
    public static IReadOnlyList<string> ProfileNames { get; } = Array.AsReadOnly(new[] { "breeze", "llama3", "qwen", "qwen25-omni" });
    public string Profile { get; set; } = "breeze";
    public bool MergeAssessmentSystemMessages { get; set; } = true;
    public bool RepeatAssessmentRequirements { get; set; } = true;
    [Range(1, 16384)] public int? CaseFeedbackMaxTokens { get; set; }
    public bool CompactCaseFeedback { get; set; }
    [Range(1,3)] public int CjkContextTokensPerCharacter { get; set; } = 3;
    [Range(16,4096)] public int ContextTokenOverhead { get; set; } = 16;
    [RegularExpression("^(chat|qwen-chatml-generate)$")] public string ConversationTransport {get;set;}="chat";

    [Required, Url]
    public string BaseUrl { get; set; } = "http://localhost:11434";

    [Required]
    public string Model { get; set; } = "qwen3:8b";

    [Range(5, 600)]
    public int TimeoutSeconds { get; set; } = 180;

    public AiTeachingAssistant.Models.AssessmentSampling AssessmentSampling { get; set; } = new();

    public string SystemPrompt { get; set; } = "You are a helpful teaching assistant.";
}
