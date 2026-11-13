namespace AiTeachingAssistant.Services;

// Used only by Development seed/maintenance tools on provenance-verified demo rows.
public static class DemoPresentationText
{
    public static string Clean(string field,string value)
    {
        var text=value;
        if(field is "feedback" or "published_feedback")
        {
            const string prefix="Codex 示範批改：";
            if(text.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))text=text[prefix.Length..];
            const string suffix=" 此結果為展示用教師草稿，請授課教師確認後再發布。";
            if(text.EndsWith(suffix,StringComparison.Ordinal))text=text[..^suffix.Length];
        }
        if(field=="file_name"&&text.Equals("codex-demo-submission.pdf",StringComparison.OrdinalIgnoreCase))return "作業報告.pdf";
        if(field=="comment"&&text.StartsWith("示範互評：",StringComparison.Ordinal))text=text[5..];
        if(field=="comment"&&text.StartsWith("示範：",StringComparison.Ordinal))text=text[3..];
        if(field=="explanation"&&text.StartsWith("示範：",StringComparison.Ordinal))text=text[3..];
        return text;
    }
}
