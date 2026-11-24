using System.Text.RegularExpressions;

namespace AiTeachingAssistant.Services;

// Source excerpts only, never a code-to-text classifier. Ambiguous/oversized
// inputs retain the existing free-excerpt contract rather than losing content.
public static class CaseClauseEvidence
{
    private static readonly Dictionary<char,char> Pairs=new(){['(']=')',['（']='）',['[']=']',['【']='】',['{']='}',['「']='」',['『']='』',['"']='"'};
    public static IReadOnlyList<string> Candidates(string source,int maximumLength)
    {
        var clauses=Clauses(source,maximumLength).Distinct(StringComparer.Ordinal).ToArray();
        return clauses.Length>1?clauses:[];
    }
    internal static IReadOnlyList<string> Clauses(string source,int maximumLength)
    {
        if(string.IsNullOrWhiteSpace(source))return [];
        var protectedPeriods=new HashSet<int>();
        foreach(Match match in Regex.Matches(source,@"\b(?:(?:Dr|Mr|Mrs|Ms|Prof|Sr|Jr|St|vs|etc|e\.g|i\.e|a\.m|p\.m)\.|(?:[a-z]\.){2,})",RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100)))
            for(var index=match.Index;index<match.Index+match.Length;index++)if(source[index]=='.')protectedPeriods.Add(index);
        var clauses=new List<string>();var closing=new Stack<char>();var start=0;
        void Add(int end){var text=source[start..end].Trim();if(text.Length>0)clauses.Add(text);}
        for(var index=0;index<source.Length;index++){
            var value=source[index];
            if(closing.Count>0&&closing.Peek()==value){closing.Pop();continue;}
            if(Pairs.TryGetValue(value,out var close)&&source.IndexOf(close,index+1)>=0){closing.Push(close);continue;}
            if(closing.Count>0||!"，,、；;：:。.!?！？\r\n".Contains(value))continue;
            var numeric=index>0&&index+1<source.Length&&char.IsDigit(source[index-1])&&char.IsDigit(source[index+1]);
            if((value is '.' or ',' or ':')&&numeric||value=='.'&&(protectedPeriods.Contains(index)||index>0&&index+1<source.Length&&char.IsLetter(source[index-1])&&char.IsLetter(source[index+1])))continue;
            // Keep compact field labels with their values: e.g. 影像：病灶 or ADL: needs help.
            if(value is ':' or '：'){
                var label=source[start..index].Trim();if(label.Length is >0 and <=8&&label.All(char.IsLetter))continue;
            }
            Add(index);start=index+1;
        }
        Add(source.Length);
        if(clauses.Count>128||clauses.Any(c=>c.Length>maximumLength))return [];
        return clauses;
    }
}
