using System.Text;
using System.Text.RegularExpressions;

namespace AiTeachingAssistant.Services;

public static class ResourceTagPolicy
{
    public const int MaxTags=10, MaxTagLength=30;

    public static string[] Normalize(IEnumerable<string?>? values)
    {
        var result=new List<string>();
        foreach(var value in values??[])
        {
            if(value is null)throw new ArgumentException("標籤不能為 null。");
            var tag=Regex.Replace(value.Normalize(NormalizationForm.FormKC).Trim(),@"\s+"," ");
            if(tag.Length==0)continue;
            if(tag.Contains(',')||tag.Contains(';'))throw new ArgumentException("標籤內不能含逗號或分號，請拆成不同標籤。");
            if(tag.Length>MaxTagLength || tag.Any(char.IsControl))throw new ArgumentException("每個標籤最多 30 字，且不能含控制字元。");
            var week=Regex.Match(tag,@"^(?:第\s*0*(\d{1,2})\s*[週周]|week\s*0*(\d{1,2}))$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
            if(week.Success && int.TryParse(week.Groups[1].Success?week.Groups[1].Value:week.Groups[2].Value,out var number) && number>0)
                tag=$"第{number}週";
            if(!result.Contains(tag,StringComparer.OrdinalIgnoreCase))result.Add(tag);
            if(result.Count>MaxTags)throw new ArgumentException("每份教材最多 10 個標籤。");
        }
        return result.ToArray();
    }
}
