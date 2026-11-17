namespace AiTeachingAssistant.Services;

public sealed record CaseChatFailure(string Code,string Reason,int? UpstreamStatus=null)
{
    public static CaseChatFailure FromHttp(int status,string body)
    {
        // Classify known upstream failures; never return its arbitrary error text,
        // which may contain a prompt, credentials, paths or model internals.
        string? error=null;
        if(body.Length<=16384)try{
            using var json=System.Text.Json.JsonDocument.Parse(body,new(){MaxDepth=8});
            if(json.RootElement.ValueKind==System.Text.Json.JsonValueKind.Object&&json.RootElement.TryGetProperty("error",out var value)&&value.ValueKind==System.Text.Json.JsonValueKind.String)error=value.GetString();
        }catch(System.Text.Json.JsonException){}
        bool Has(string value)=>error?.Contains(value,StringComparison.OrdinalIgnoreCase)==true;
        if(Has("model")&&(Has("not found")||Has("does not exist")))return new("model_missing","模型服務回報找不到設定的模型；請確認該主機已安裝指定模型。",status);
        if(Has("out of memory")||Has("failed to allocate")||Has("unable to allocate"))return new("provider_memory","模型服務回報記憶體配置失敗；可能無法載入模型或建立所需脈絡。",status);
        if((Has("context length")||Has("context window"))&&(Has("exceed")||Has("too long")))return new("provider_context","模型服務回報輸入超過其脈絡容量；應用程式估算不等於服務端實際token數。",status);
        if(Has("timed out")||Has("timeout"))return new("provider_timeout","模型服務回報內部處理逾時，沒有取得完整回覆。",status);
        return new("provider_http",$"模型服務回傳 HTTP {status}，未提供可安全顯示的進一步原因；不能據此斷定模型拒答。",status);
    }
}

public sealed class CaseChatException(int statusCode, string message,CaseChatFailure? diagnostic=null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public CaseChatFailure? Diagnostic { get; } = diagnostic;
}
