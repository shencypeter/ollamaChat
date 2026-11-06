using System.Security.Claims;
using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.AspNetCore.Http;

namespace AiTeachingAssistant.Tests;
public class AccountSessionIsolationTests
{
    [Fact] public void TeachingAndPracticeChatUseDifferentMemoryWithoutDeletingEither()
    {
        var accessor=new HttpContextAccessor{HttpContext=new DefaultHttpContext{Session=new TestSession(),User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"teacher"),new Claim(ClaimTypes.Role,PortalRoles.Administrator),new Claim("can_teach","True")],"test"))}};
        var experience=new LearnerExperience(accessor);var store=new ChatSessionStore(accessor,Microsoft.Extensions.Options.Options.Create(new MemoryOptions()),experience);
        store.SaveMessages(AnalysisStage.GeneralChat,[new("user","teaching-only",DateTimeOffset.UtcNow)]);
        accessor.HttpContext.Request.Headers[LearnerExperience.Header]="student";Assert.Empty(store.GetMessages(AnalysisStage.GeneralChat));
        store.SaveMessages(AnalysisStage.GeneralChat,[new("user","practice-only",DateTimeOffset.UtcNow)]);
        accessor.HttpContext.Request.Headers.Remove(LearnerExperience.Header);Assert.Equal("teaching-only",Assert.Single(store.GetMessages(AnalysisStage.GeneralChat)).Content);
    }
    [Fact]
    public void ChatMemoryAndSettingsAreScopedToAuthenticatedAccountEvenWithSharedBrowserSession()
    {
        var shared=new TestSession();var accessor=new HttpContextAccessor();
        void Actor(string id)=>accessor.HttpContext=new DefaultHttpContext{Session=shared,User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,id)},"test"))};
        Actor("professor");var store=new ChatSessionStore(accessor,Microsoft.Extensions.Options.Options.Create(new MemoryOptions{Strategy="Window"}));
        store.SetMemoryStrategy("Buffer");store.SaveMessages(AnalysisStage.GeneralChat,new[]{new ChatMessage("user","Professor-only message",DateTimeOffset.UtcNow)});
        store.SaveSummaryMemory(AnalysisStage.GeneralChat,new SummaryMemoryState("Professor-only summary",[]));
        Actor("student");Assert.Empty(store.GetMessages(AnalysisStage.GeneralChat));Assert.Null(store.GetSummaryMemory(AnalysisStage.GeneralChat));Assert.Equal("Window",store.GetMemoryStrategy());
        store.SetMemoryStrategy("None");store.Clear(AnalysisStage.GeneralChat);
        Actor("professor");Assert.Equal("Professor-only message",Assert.Single(store.GetMessages(AnalysisStage.GeneralChat)).Content);Assert.Equal("Professor-only summary",store.GetSummaryMemory(AnalysisStage.GeneralChat)!.Summary);Assert.Equal("Buffer",store.GetMemoryStrategy());
    }
    private sealed class TestSession:ISession
    {
        private readonly Dictionary<string,byte[]> values=[];
        public bool IsAvailable=>true;public string Id=>"shared";public IEnumerable<string> Keys=>values.Keys;
        public void Clear()=>values.Clear();public Task CommitAsync(CancellationToken ct=default)=>Task.CompletedTask;public Task LoadAsync(CancellationToken ct=default)=>Task.CompletedTask;
        public void Remove(string key)=>values.Remove(key);public void Set(string key,byte[] value)=>values[key]=value;
        public bool TryGetValue(string key,out byte[] value)=>values.TryGetValue(key,out value!);
    }
}
