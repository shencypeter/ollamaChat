namespace AiTeachingAssistant.Services;

public static class ResourceFileNamePolicy
{
    public static string SafeName(string name)
    {
        var basename=name.Replace('\\','/').Split('/').Last();
        return new string(basename.Where(c=>!char.IsControl(c)&&c is not (>= '\u202a' and <= '\u202e') and not (>= '\u2066' and <= '\u2069')).ToArray()).Trim();
    }
}
