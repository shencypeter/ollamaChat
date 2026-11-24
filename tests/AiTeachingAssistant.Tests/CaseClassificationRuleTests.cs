using System.Text.Json;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Services;

namespace AiTeachingAssistant.Tests;

public sealed class CaseClassificationRuleTests
{
    [Fact] public void CourseCatalogControlsSchemaAndValidationWithoutChangingStaticDefaults()
    {
        var definitions=CaseClassificationRules.Defaults.Concat([new CaseClassificationRuleDefinition("C7","新增功能","C","合成定義",["提示詞"],true)]).ToArray();
        definitions=definitions.Select(d=>d.Code=="C2"?d with{Enabled=false}:d).ToArray();
        var enabled=CaseClassificationRules.Enabled(definitions);
        var schema=CaseAnnotationContract.SchemaFor(true,original:"合成原文",definitions:enabled);
        var codes=schema.GetProperty("properties").GetProperty("annotations").GetProperty("items").GetProperty("properties").GetProperty("codes").GetProperty("items").GetProperty("enum").EnumerateArray().Select(x=>x.GetString()).ToArray();
        Assert.Contains("C7",codes);Assert.DoesNotContain("C2",codes);
        using var entries=JsonDocument.Parse("""[{"text":"合成原文","occurrence":0,"codes":["C7","C2"],"explanation":"","status":"unverified"}]""");
        Assert.Equal(new[]{"C7"},Assert.Single(CaseAnnotationContract.Validate("合成原文",entries.RootElement,enabled)).Codes);
        Assert.False(CaseCodingCatalog.IsValid("C7"));
        Assert.Contains("C2",CaseAnnotationContract.AnnotationSchema.GetProperty("properties").GetProperty("codes").GetProperty("items").GetProperty("enum").EnumerateArray().Select(x=>x.GetString()));
    }
    [Fact] public void DisabledParentDisablesDescendantsButPreservesMetadata()
    {
        var definitions=CaseClassificationRules.Defaults.Select(d=>d.Code=="A1"?d with{Enabled=false}:d).ToArray();
        Assert.DoesNotContain(CaseClassificationRules.Enabled(definitions),d=>d.Code.StartsWith("A1",StringComparison.Ordinal));
        Assert.True(definitions.Single(d=>d.Code=="A1a").Enabled);
    }
    [Fact] public void SettingsPromptRetainsTeachingRulesAndTreatsKeywordsAsHints()
    {
        var definitions=CaseClassificationRules.Defaults.Concat([new CaseClassificationRuleDefinition("D6","新增資源","D","合成說明",["資源詞"],true)]).ToArray();
        var prompt=CaseClassificationRules.Prompt(AnalysisStage.Psychosocial,3,definitions);
        Assert.Contains("不給分、不判通過、不解鎖",prompt);Assert.Contains("D6",prompt);Assert.Contains("合成說明",prompt);Assert.Contains("資源詞",prompt);
        Assert.Contains("不是確定性比對",prompt);Assert.Contains("A2c",prompt);
        Assert.Equal(CaseChatPrompts.Get(AnalysisStage.Psychosocial),CaseClassificationRules.Prompt(AnalysisStage.Psychosocial,0,CaseClassificationRules.Defaults));
        Assert.DoesNotContain("caseRules",JsonSerializer.Serialize(new OllamaMessage("user","合成"){CaseRules=definitions}));
    }
    [Theory]
    [InlineData("A2c","A",true)][InlineData("B2c","B",true)][InlineData("C7","D",true)]
    [InlineData("C7a","C",true)][InlineData("C0","C",true)][InlineData("C7<script>","C",true)]
    [InlineData("C7\n","C",true)][InlineData("C7\r\n","C",true)]
    public void InvalidCodesOrForbiddenCodesCannotBeEnabled(string code,string phase,bool enabled)
    {
        var definitions=CaseClassificationRules.Defaults.Concat([new CaseClassificationRuleDefinition(code,"合成",phase,"",[],enabled)]).ToArray();
        Assert.Equal(400,Assert.Throws<CaseChatException>(()=>CaseClassificationRules.Validate(definitions,CaseClassificationRules.Defaults)).StatusCode);
    }
    [Fact] public void ExistingCodesCannotBeDeletedAndAtLeastOneCodeRemainsAcrossAllPhases()
    {
        Assert.Throws<CaseChatException>(()=>CaseClassificationRules.Validate(CaseClassificationRules.Defaults.Where(d=>d.Code!="C2").ToArray(),CaseClassificationRules.Defaults));
        Assert.Throws<CaseChatException>(()=>CaseClassificationRules.Validate(CaseClassificationRules.Defaults.Select(d=>d with{Enabled=false}).ToArray(),CaseClassificationRules.Defaults));
        Assert.DoesNotContain(CaseClassificationRules.Enabled(CaseClassificationRules.Validate(CaseClassificationRules.Defaults.Select(d=>d.Phase=="C"?d with{Enabled=false}:d).ToArray(),CaseClassificationRules.Defaults)),d=>d.Phase=="C");
    }
}
