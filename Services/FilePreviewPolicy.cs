namespace AiTeachingAssistant.Services;

public static class FilePreviewPolicy
{
    // Both extension and signature must agree. Never render HTML or SVG uploads inline.
    public static string? ContentType(string name, ReadOnlySpan<byte> bytes)
    {
        var extension=Path.GetExtension(name).ToLowerInvariant();
        if(extension==".pdf" && bytes.StartsWith("%PDF-"u8))return "application/pdf";
        if(extension==".png" && bytes.StartsWith(new byte[]{137,80,78,71,13,10,26,10}))return "image/png";
        if(extension is ".jpg" or ".jpeg" && bytes.StartsWith(new byte[]{255,216,255}))return "image/jpeg";
        if(extension==".gif" && (bytes.StartsWith("GIF87a"u8)||bytes.StartsWith("GIF89a"u8)))return "image/gif";
        if(extension==".webp" && bytes.Length>=12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8,4).SequenceEqual("WEBP"u8))return "image/webp";
        return null;
    }
}
