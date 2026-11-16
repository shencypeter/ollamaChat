using System.ComponentModel.DataAnnotations;

namespace AiTeachingAssistant.Options;

public sealed class CaseChatOptions
{
    public const string SectionName = "CaseChat";
    [Range(256, 1000000)] public int MaxContextBytes { get; set; } = 12000;
    [Range(1, 8000)] public int MaxMessageCharacters { get; set; } = 8000;
    [Range(1, 16000)] public int MaxResponseCharacters { get; set; } = 16000;
    [Range(1, 120)] public int RawIdleMinutes { get; set; } = 20;
    [Range(1, 20)] public int MaxRawSessionsPerUser { get; set; } = 3;
    [Range(1, 2000)] public int MaxRawSessions { get; set; } = 200;
}
