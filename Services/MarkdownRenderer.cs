using Ganss.Xss;
using Markdig;

namespace AiTeachingAssistant.Services;

public interface IMarkdownRenderer
{
    string ToHtml(string markdown);
}

public sealed class MarkdownRenderer : IMarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .DisableHtml()
        .Build();

    public string ToHtml(string markdown)
    {
        var html = Markdown.ToHtml(markdown, Pipeline);
        return new HtmlSanitizer().Sanitize(html);
    }
}
