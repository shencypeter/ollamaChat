using AiTeachingAssistant.Services;

namespace AiTeachingAssistant.Tests;

public class MarkdownRendererTests
{
    [Fact]
    public void ToHtml_RendersTeachingFeedbackMarkdown()
    {
        var renderer = new MarkdownRenderer();

        var html = renderer.ToHtml("""
            **總結提醒**：
            - 需區分客觀功能衰退與主觀感受。
            - 建議補充個案的壓力源。
            """);

        Assert.Contains("<strong>總結提醒</strong>", html);
        Assert.Contains("<ul>", html);
        Assert.Contains("<li>", html);
    }

    [Fact]
    public void ToHtml_DoesNotRenderRawHtml()
    {
        var renderer = new MarkdownRenderer();

        var html = renderer.ToHtml("<script>alert('no')</script><b>unsafe</b> [link](javascript:alert('no'))");

        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<b>", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
    }
}
