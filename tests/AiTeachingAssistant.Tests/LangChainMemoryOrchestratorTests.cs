using AiTeachingAssistant.Models;
using AiTeachingAssistant.Options;
using AiTeachingAssistant.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;
using System.Text.Json;

namespace AiTeachingAssistant.Tests;

public class LangChainMemoryOrchestratorTests
{
    [Fact]
    public async Task WindowMemory_PreservesTheRolesAndContentOfTheConversation()
    {
        var (orchestrator, _, _) = CreateOrchestrator("Window", windowSize: 6);
        var history = Conversation(
            ("user", "早安你好..."),
            ("assistant", "早安！你好！..."),
            ("user", "對了 我的名字叫Peter, 男性, 處女座."),
            ("assistant", "嗨Peter！..."));

        var result = await orchestrator.BuildContextAsync(history, "我剛剛跟你說我是什麼星座？", AnalysisStage.Premorbid);

        Assert.Collection(result.Messages,
            stage =>
            {
                Assert.Equal("system", stage.Role);
                Assert.Contains("病前功能", stage.Content);
            },
            message => AssertMessage(message, "user", "早安你好..."),
            message => AssertMessage(message, "assistant", "早安！你好！..."),
            message => AssertMessage(message, "user", "對了 我的名字叫Peter, 男性, 處女座."),
            message => AssertMessage(message, "assistant", "嗨Peter！..."),
            message => AssertMessage(message, "user", "我剛剛跟你說我是什麼星座？"));
    }

    [Fact]
    public async Task WindowMemory_KeepsOnlyTheConfiguredNumberOfTurnPairs()
    {
        var (orchestrator, _, _) = CreateOrchestrator("Window", windowSize: 1);
        var history = Conversation(
            ("user", "old question"),
            ("assistant", "old answer"),
            ("user", "latest question"),
            ("assistant", "latest answer"));

        var result = await orchestrator.BuildContextAsync(history, "current question", AnalysisStage.Premorbid);

        Assert.Collection(result.Messages,
            stage => Assert.Contains("病前功能", stage.Content),
            message => AssertMessage(message, "user", "latest question"),
            message => AssertMessage(message, "assistant", "latest answer"),
            message => AssertMessage(message, "user", "current question"));
    }

    [Fact]
    public async Task BufferMemory_KeepsAllMessagesWithTheirOriginalRoles()
    {
        var (orchestrator, _, _) = CreateOrchestrator("Buffer", windowSize: 1);
        var history = Conversation(("user", "first"), ("assistant", "second"));

        var result = await orchestrator.BuildContextAsync(history, "third", AnalysisStage.Premorbid);

        Assert.Equal(["system", "user", "assistant", "user"], result.Messages.Select(message => message.Role));
        Assert.Contains("病前功能", result.Messages[0].Content);
        Assert.Equal(["first", "second", "third"], result.Messages.Skip(1).Select(message => message.Content));
    }

    [Fact]
    public async Task NoneMemory_SendsOnlyTheCurrentMessage()
    {
        var (orchestrator, _, _) = CreateOrchestrator("None");
        var history = Conversation(("user", "old"), ("assistant", "old response"));

        var result = await orchestrator.BuildContextAsync(history, "current", AnalysisStage.Premorbid);

        Assert.Collection(result.Messages,
            stage => Assert.Contains("病前功能", stage.Content),
            message => AssertMessage(message, "user", "current"));
    }

    [Fact]
    public async Task SummaryMemory_IsScopedToTheSelectedAnalysisStage()
    {
        var (orchestrator, store, _) = CreateOrchestrator("Summary", summaryRecentTurns: 1);
        var premorbidHistory = new List<ChatMessage>();
        await AddTurn(
            "病前功能資料只應留在這個聊天室",
            "收到病前功能資料。",
            premorbidHistory,
            orchestrator);

        var illnessCourse = await orchestrator.BuildContextAsync(
            [],
            "這是疾病病程的第一則訊息",
            AnalysisStage.IllnessCourse);

        Assert.Collection(illnessCourse.Messages,
            stage =>
            {
                Assert.Equal("system", stage.Role);
                Assert.Contains("疾病病程", stage.Content);
                Assert.DoesNotContain("病前功能資料只應留在這個聊天室", stage.Content);
            },
            current => AssertMessage(current, "user", "這是疾病病程的第一則訊息"));
        Assert.NotNull(store.GetSummaryMemory(AnalysisStage.Premorbid));
        Assert.NotNull(store.GetSummaryMemory(AnalysisStage.IllnessCourse));
    }

    [Theory]
    [InlineData(AnalysisStage.IllnessCourse, "只有第 1 類可以視為學生作答的遺漏")]
    [InlineData(AnalysisStage.FunctionalAssessment, "不存在的測驗結果")]
    [InlineData(AnalysisStage.Psychosocial, "不得把相關性描述為已確定的因果關係")]
    public async Task StageContext_UsesTheConfiguredTeachingInstruction(
        AnalysisStage stage,
        string expectedInstruction)
    {
        var (orchestrator, _, _) = CreateOrchestrator("None");

        var result = await orchestrator.BuildContextAsync([], "學生原始作答", stage);

        Assert.Equal("system", result.Messages[0].Role);
        Assert.Contains(expectedInstruction, result.Messages[0].Content);
        AssertMessage(result.Messages[1], "user", "學生原始作答");
    }

    [Fact]
    public async Task SummaryMemory_RetainsAgedFactWithoutSendingOriginalMessageVerbatim()
    {
        var (orchestrator, store, summarizer) = CreateOrchestrator("Summary", summaryRecentTurns: 1);
        var history = new List<ChatMessage>();

        await AddTurn("你好 千問 我是 Peter, 處女座 男性", "你好 Peter，很高興認識你。", history, orchestrator);
        await AddTurn("今天天氣如何？", "今天是晴天。", history, orchestrator);
        await AddTurn("請給我一道數學題。", "請計算 12 x 8。", history, orchestrator);

        var result = await orchestrator.BuildContextAsync(history, "還記得我的星座嗎？", AnalysisStage.Premorbid);

        Assert.DoesNotContain(result.Messages, message => message.Content.Contains("你好 千問 我是 Peter"));
        Assert.Collection(result.Messages,
            stage =>
            {
                Assert.Equal("system", stage.Role);
                Assert.Contains("病前功能", stage.Content);
            },
            summary =>
            {
                Assert.Equal("system", summary.Role);
                Assert.Contains("Summary of earlier conversation:", summary.Content);
                Assert.Contains("Peter", summary.Content);
                Assert.Contains("處女座", summary.Content);
            },
            recentUser => AssertMessage(recentUser, "user", "請給我一道數學題。"),
            recentAssistant => AssertMessage(recentAssistant, "assistant", "請計算 12 x 8。"),
            current => AssertMessage(current, "user", "還記得我的星座嗎？"));
        Assert.True(result.Diagnostics.SummaryPresent);
        Assert.Equal(2, result.Diagnostics.RecentMessagesSent);
        Assert.True(result.Diagnostics.SummaryCharacters > 0);
        Assert.NotNull(store.SummaryMemory);
        Assert.True(summarizer.SummaryCalls >= 2);
        Assert.Contains(summarizer.ExistingSummaries.Skip(1), summary => summary.Contains("Peter") && summary.Contains("處女座"));

        var handler = new CapturingHttpHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://ollama.test/") };
        var ollamaClient = new OllamaClient(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(new OllamaOptions
            {
                BaseUrl = "http://ollama.test",
                Model = "qwen3:8b",
                SystemPrompt = "Normal teaching system prompt"
            }),
            NullLogger<OllamaClient>.Instance);

        _ = await ollamaClient.ChatAsync(result.Messages, CancellationToken.None);

        using var payload = JsonDocument.Parse(Assert.IsType<string>(handler.RequestBody));
        var payloadMessages = payload.RootElement.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(["system", "system", "system", "user", "assistant", "user"],
            payloadMessages.Select(message => message.GetProperty("role").GetString()));
        Assert.Equal("Normal teaching system prompt", payloadMessages[0].GetProperty("content").GetString());
        Assert.Contains("病前功能", payloadMessages[1].GetProperty("content").GetString());
        Assert.Contains("Peter", payloadMessages[2].GetProperty("content").GetString());
        Assert.Contains("處女座", payloadMessages[2].GetProperty("content").GetString());
        Assert.DoesNotContain("你好 千問 我是 Peter", handler.RequestBody);
    }

    private static async Task AddTurn(
        string userContent,
        string assistantContent,
        List<ChatMessage> history,
        LangChainMemoryOrchestrator orchestrator)
    {
        _ = await orchestrator.BuildContextAsync(history, userContent, AnalysisStage.Premorbid);
        var user = new ChatMessage("user", userContent, DateTimeOffset.UtcNow);
        var assistant = new ChatMessage("assistant", assistantContent, DateTimeOffset.UtcNow);
        history.Add(user);
        history.Add(assistant);
        await orchestrator.CommitTurnAsync(AnalysisStage.Premorbid, user, assistant);
    }

    private static (LangChainMemoryOrchestrator Orchestrator, FakeSessionStore Store, FakeOllamaClient Ollama)
        CreateOrchestrator(string strategy, int windowSize = 6, int summaryRecentTurns = 3)
    {
        var store = new FakeSessionStore(strategy);
        var ollama = new FakeOllamaClient();
        var options = Microsoft.Extensions.Options.Options.Create(new MemoryOptions
        {
            Strategy = strategy,
            WindowSize = windowSize,
            SummaryRecentTurns = summaryRecentTurns,
            SummaryMaxCharacters = 2000,
            MaxStoredMessages = 40
        });
        return (new LangChainMemoryOrchestrator(
            options,
            store,
            ollama,
            NullLogger<LangChainMemoryOrchestrator>.Instance), store, ollama);
    }

    private static IReadOnlyList<ChatMessage> Conversation(params (string Role, string Content)[] messages) =>
        messages.Select(message => new ChatMessage(message.Role, message.Content, DateTimeOffset.UtcNow)).ToList();

    private static void AssertMessage(OllamaMessage actual, string role, string content)
    {
        Assert.Equal(role, actual.Role);
        Assert.Equal(content, actual.Content);
    }

    private sealed class FakeSessionStore(string strategy) : IChatSessionStore
    {
        private readonly Dictionary<AnalysisStage, SummaryMemoryState> _summaries = [];
        public SummaryMemoryState? SummaryMemory => GetSummaryMemory(AnalysisStage.Premorbid);
        public string Strategy { get; private set; } = strategy;
        public AnalysisStage ActiveStage { get; private set; } = AnalysisStage.Premorbid;
        public IReadOnlyList<ChatMessage> GetMessages(AnalysisStage stage) => [];
        public void SaveMessages(AnalysisStage stage, IEnumerable<ChatMessage> messages) { }
        public AnalysisStage GetActiveStage() => ActiveStage;
        public void SetActiveStage(AnalysisStage stage) => ActiveStage = stage;
        public string GetMemoryStrategy() => Strategy;
        public void SetMemoryStrategy(string value) { Strategy = value; _summaries.Clear(); }
        public SummaryMemoryState? GetSummaryMemory(AnalysisStage stage) =>
            _summaries.GetValueOrDefault(stage);
        public void SaveSummaryMemory(AnalysisStage stage, SummaryMemoryState state) =>
            _summaries[stage] = state;
        public void ClearSummaryMemory(AnalysisStage stage) => _summaries.Remove(stage);
        public void Clear(AnalysisStage stage) => _summaries.Remove(stage);
    }

    private sealed class FakeOllamaClient : IOllamaClient
    {
        public int SummaryCalls { get; private set; }
        public List<string> ExistingSummaries { get; } = [];

        public Task<(string Content, string? Thinking, RequestMetrics Metrics)> ChatAsync(
            IReadOnlyList<OllamaMessage> messages,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string> SummarizeAsync(
            string existingSummary,
            IReadOnlyList<ChatMessage> messagesToIncorporate,
            int maxCharacters,
            CancellationToken cancellationToken)
        {
            SummaryCalls++;
            ExistingSummaries.Add(existingSummary);
            var transcript = string.Join(" ", messagesToIncorporate.Select(message => message.Content));
            var importantFact = (existingSummary + " " + transcript).Contains("Peter")
                ? "使用者名叫 Peter，是男性，星座是處女座。"
                : existingSummary;
            return Task.FromResult(string.Join(" ", new[] { importantFact, transcript }
                .Where(value => !string.IsNullOrWhiteSpace(value))));
        }

        public Task<OllamaHealthResult> CheckHealthAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new OllamaHealthResult(true, "ok", [], 0));
    }

    private sealed class CapturingHttpHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            const string response = """
                {"model":"qwen3:8b","message":{"role":"assistant","content":"處女座"},"done":true,"done_reason":"stop","total_duration":1,"load_duration":0,"prompt_eval_count":1,"prompt_eval_cached_count":0,"prompt_eval_duration":1,"eval_count":1,"eval_duration":1}
                """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        }
    }
}
