using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AiTeachingAssistant.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CaseClassificationRuleDefinition(string Code,string Label,string Phase,string Description,IReadOnlyList<string> Keywords,bool Enabled);
public sealed record CaseClassificationRuleSet(int Version,IReadOnlyList<CaseClassificationRuleDefinition> Definitions,DateTimeOffset? UpdatedAt,Guid? UpdatedBy);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CaseClassificationRulesInput
{
    [Range(0,int.MaxValue)] public int ExpectedVersion {get;set;}
    [Required,MinLength(1),MaxLength(128)] public IReadOnlyList<CaseClassificationRuleDefinition> Definitions {get;set;}=[];
}
