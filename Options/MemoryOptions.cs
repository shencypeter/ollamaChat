using System.ComponentModel.DataAnnotations;

namespace AiTeachingAssistant.Options;

public sealed class MemoryOptions
{
    public const string SectionName = "Memory";

    [RegularExpression("^(Window|Buffer|None|Summary)$", ErrorMessage = "Memory strategy must be Window, Buffer, None, or Summary.")]
    public string Strategy { get; set; } = "Window";

    [Range(1, 50)]
    public int WindowSize { get; set; } = 6;

    [Range(1, 50)]
    public int SummaryRecentTurns { get; set; } = 3;

    [Range(200, 12000)]
    public int SummaryMaxCharacters { get; set; } = 2000;

    [Range(2, 200)]
    public int MaxStoredMessages { get; set; } = 40;
}
