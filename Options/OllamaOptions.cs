using System.ComponentModel.DataAnnotations;

namespace AiTeachingAssistant.Options;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    [Required, Url]
    public string BaseUrl { get; set; } = "http://localhost:11434";

    [Required]
    public string Model { get; set; } = "qwen3:8b";

    [Range(5, 600)]
    public int TimeoutSeconds { get; set; } = 180;

    public string SystemPrompt { get; set; } = "You are a helpful teaching assistant.";
}
