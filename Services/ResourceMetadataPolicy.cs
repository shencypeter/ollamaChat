using System.Globalization;
namespace AiTeachingAssistant.Services;
public static class ResourceMetadataPolicy
{
    public static DateOnly? Date(string? value)
    {
        if(string.IsNullOrWhiteSpace(value))return null;
        if(value.Length!=10||!DateOnly.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))throw new ArgumentException("授課日期須為有效的 YYYY-MM-DD。");
        return date;
    }
    public static string Summary(string? value)
    {
        if(value?.Length>10000)throw new ArgumentException("教師摘要最多 10,000 字。");
        return value?.Trim()??"";
    }
    public static string Order(string? value)=>value switch
    {
        null or "" or "uploaded_desc"=>"created_at DESC,id DESC",
        "teaching_desc"=>"teaching_date DESC NULLS LAST,created_at DESC,id DESC",
        "teaching_asc"=>"teaching_date ASC NULLS LAST,created_at DESC,id DESC",
        _=>throw new ArgumentException("教材排序無效。")
    };
}
