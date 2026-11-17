using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Claims;
using AiTeachingAssistant.Models;
namespace AiTeachingAssistant.Services;

public sealed record InspectedOllamaRequest(
    string Purpose,
    string Url,
    JsonElement Payload,
    int? ResponseStatusCode = null,
    JsonElement? RawResponse = null);
public sealed record OllamaInspection(string PromptVersion,IReadOnlyList<InspectedOllamaRequest> Requests);

// HTTP-request scoped. Never stored in chat history, a singleton, a file or a log.
public sealed class OllamaRequestInspection(IHttpContextAccessor accessor,LearnerExperience? experience=null)
{
    private readonly List<InspectedOllamaRequest> requests=[];
    private bool conversationEnabled;
    private string? conversationActor,conversationVersion;
    private bool ConversationAllowed=>conversationEnabled && experience?.IsPractice!=true &&
        accessor.HttpContext?.User is { Identity.IsAuthenticated:true } user &&
        conversationActor is not null && user.FindFirstValue(ClaimTypes.NameIdentifier)==conversationActor;
    private bool Allowed=>ConversationAllowed || experience?.IsPractice!=true && accessor.HttpContext?.User is { Identity.IsAuthenticated:true } user && LearnerExperience.IsStaff(user);
    public OllamaInspection? Current=>(conversationEnabled?ConversationAllowed:Allowed) && requests.Count>0?new(conversationVersion??AnalysisStageInstructions.Version,requests.ToArray()):null;

    // The controller supplies course access from the database, never from client input.
    // Conversation snapshots exist only in this HTTP scope and the authorized response.
    public void EnableConversationCapture(CaseChatAccess access,string promptVersion)
    {
        DisableConversationCapture();
        if(!access.Practice||!access.Enabled||access.Status!="active"||experience?.IsPractice==true||accessor.HttpContext?.User.Identity?.IsAuthenticated!=true)return;
        conversationActor=accessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        conversationEnabled=conversationActor is not null;conversationVersion=promptVersion;
    }
    public void DisableConversationCapture(){requests.Clear();conversationEnabled=false;conversationActor=null;conversationVersion=null;}
    public int? CaptureConversation(string serializedBody,Uri? endpoint,string purpose)=>ConversationAllowed?Capture(serializedBody,endpoint,purpose):null;
    public int? Capture(string serializedBody,Uri? endpoint,string purpose)
    {
        if(!Allowed)return null;
        var body=JsonNode.Parse(serializedBody)!;
        RemovePrivateThinking(body);
        var url=endpoint is null?"api/chat":new UriBuilder(endpoint){UserName="",Password="",Query="",Fragment=""}.Uri.ToString();
        requests.Add(new(purpose,url,JsonSerializer.SerializeToElement(body)));
        return requests.Count-1;
    }

    public void CaptureResponse(int? requestIndex,string serializedBody,int statusCode)
    {
        if(!Allowed || requestIndex is null || requestIndex<0 || requestIndex>=requests.Count)return;
        JsonElement? response=null;
        try
        {
            var body=JsonNode.Parse(serializedBody);
            if(body is not null)
            {
                RemovePrivateThinking(body);
                response=JsonSerializer.SerializeToElement(body);
            }
        }
        catch(JsonException)
        {
            // Do not expose an unparsed body because private reasoning cannot be reliably redacted.
        }
        requests[requestIndex.Value]=requests[requestIndex.Value] with
        {
            ResponseStatusCode=statusCode,
            RawResponse=response
        };
    }

    private static void RemovePrivateThinking(JsonNode node)
    {
        if(node is JsonObject obj)
        {
            obj.Remove("thinking");
            foreach(var child in obj.Select(property=>property.Value).Where(value=>value is not null).ToArray())
                RemovePrivateThinking(child!);
        }
        else if(node is JsonArray array)
        {
            foreach(var child in array.Where(value=>value is not null).ToArray())
                RemovePrivateThinking(child!);
        }
    }
}
