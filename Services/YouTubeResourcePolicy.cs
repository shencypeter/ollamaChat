using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;

namespace AiTeachingAssistant.Services;

public static class YouTubeResourcePolicy
{
    public static string VideoId(string? value)
    {
        if(!Uri.TryCreate(value?.Trim(),UriKind.Absolute,out var url) || url.Scheme is not ("https" or "http") || !url.IsDefaultPort || url.UserInfo.Length>0)
            throw new ArgumentException("請輸入有效的 YouTube 影片網址。");
        var host=url.IdnHost.ToLowerInvariant();
        var parts=url.AbsolutePath.Split('/',StringSplitOptions.RemoveEmptyEntries);
        string? id=null;
        if(host=="youtu.be" && parts.Length==1)id=parts[0];
        else if(host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com" or "youtube-nocookie.com" or "www.youtube-nocookie.com")
        {
            if(parts.Length==1 && parts[0]=="watch")
            {
                var query=QueryHelpers.ParseQuery(url.Query);
                if(query.TryGetValue("v",out var values) && values.Count==1)id=values[0];
            }
            else if(parts.Length==2 && parts[0] is "embed" or "shorts" or "live")id=parts[1];
        }
        if(id==null || !Regex.IsMatch(id,@"\A[A-Za-z0-9_-]{11}\z"))throw new ArgumentException("請使用單一 YouTube 影片網址，不能只提供播放清單或其他網站。");
        return id;
    }
}
