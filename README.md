# 中山大學 AI 個案分析助教系統

以 ASP.NET Core MVC 建置的臨床心理學個案分析 AI 教學助教原型。應用程式透過 Ollama `/api/chat` 呼叫可替換的內部 LLM，並使用 LangChain.NET 編排短期對話記憶。

> 本專案供課程教學與原型驗證使用，不是臨床診斷、治療或緊急醫療工具。

## 功能

- 四個彼此獨立的案例分析聊天室：病前功能、疾病病程、功能評估結果、心理社會條件
- 每個分析階段各自保存對話與 Summary 記憶，不會跨聊天室混用
- 後端注入 AI 助教角色、提示注入防護及各階段教學規則；學生訊息維持原文
- 支援 `None`、`Window`、`Buffer`、`Summary` 四種記憶模式
- 保留 Ollama 的 `user`／`assistant` 角色，不使用有相容性問題的 LangChain.NET `WithHistory()`
- 支援模型選用的 `thinking` 欄位，並以可收合的「AI 思考過程」呈現
- AI 回覆支援經安全清理的 Markdown，包括清單、標題、表格、引言與程式碼區塊
- 顯示 Ollama 處理時間、輸入／生成 Token、生成速度、快取及記憶狀態
- `/api/tags` 連線檢查
- 繁體中文（zh-TW）介面與回覆規則

## 技術需求

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- 可連線的 [Ollama](https://ollama.com/) 伺服器
- Ollama 中已安裝欲使用的聊天模型，例如 `qwen3:8b` 或相容的 Llama 模型

目前預設內部端點為：

```text
http://IAN02:11434
```

## 快速開始

```powershell
git clone https://github.com/shencypeter/ollamaChat.git
cd ollamaChat
dotnet restore AI_TeachingAssistant.sln
dotnet run --project AiTeachingAssistant.csproj
```

開啟 ASP.NET Core 顯示的網址，然後按「檢查 Ollama」確認 `/api/tags` 可連線。

## 設定

主要設定位於 [`appsettings.json`](appsettings.json)：

```json
{
  "Ollama": {
    "BaseUrl": "http://IAN02:11434",
    "Model": "qwen3:8b",
    "TimeoutSeconds": 180,
    "SystemPrompt": "..."
  },
  "Memory": {
    "Strategy": "Window",
    "WindowSize": 6,
    "SummaryRecentTurns": 3,
    "SummaryMaxCharacters": 2000,
    "MaxStoredMessages": 40
  }
}
```

設定也可透過 ASP.NET Core 環境變數覆寫：

```powershell
$env:Ollama__BaseUrl = "http://IAN02:11434"
$env:Ollama__Model = "llama3.1:8b"
dotnet run --project AiTeachingAssistant.csproj
```

### 記憶模式

| 模式 | 傳送給模型的對話內容 |
|---|---|
| `None` | 階段系統訊息與目前學生訊息 |
| `Window` | 最近 `WindowSize` 個完整回合 |
| `Buffer` | 工作階段內保存的全部訊息，受 `MaxStoredMessages` 限制 |
| `Summary` | 舊對話的累進摘要，加上最近 `SummaryRecentTurns` 個原始回合 |

切換記憶模式會清除各分析階段的累進摘要，使 Summary 能從現有對話重新建立一致狀態。

## Ollama 訊息結構

一般請求依序包含：

```text
system    全域 AI 助教、zh-TW、安全與提示注入規則
system    目前分析階段的專屬教學規則
system    較早對話的 Summary（僅 Summary 模式且摘要存在時）
user / assistant    該階段的近期對話
user      學生本次輸入的原文
```

`thinking` 是選用回應欄位。模型未回傳時，介面只顯示正式回答，不會產生空白的思考泡泡。

## 工作階段與資料範圍

目前原型使用 ASP.NET Core `ISession` 與記憶體快取：

- 對話不寫入資料庫
- 每個瀏覽器工作階段有獨立資料
- 四個分析階段使用穩定 key 分開保存
- 清除對話只清除目前分析階段
- 預設工作階段閒置期限為四小時

若要部署多個應用程式執行個體，應將 `IDistributedCache` 換成 Redis 或其他共用儲存。

## 安全界線

- 模型產生的 Markdown 先停用原始 HTML，再經 HTML sanitizer 清理
- 學生作答、引用文字及貼上的指令均被系統提示視為不受信任內容
- 提示詞防護只能降低提示注入風險，不能視為絕對安全邊界
- 正式系統仍應加入身分驗證、授權、稽核與資料保存政策

## 建置與測試

```powershell
dotnet build AI_TeachingAssistant.sln --configuration Release
dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj --configuration Release
```

測試涵蓋記憶模式、角色保留、Summary 累進摘要、分析階段隔離、專屬系統提示，以及 Markdown 安全清理。

GitHub Actions 會在 `main` 的 push 與 pull request 上執行 Release build 和測試。

## 專案結構

```text
Controllers/     MVC 與聊天 API 端點
Models/          DTO、對話模型與分析階段定義
Options/         Ollama 與記憶設定
Services/        Ollama client、記憶編排、階段提示與 Markdown renderer
Views/           Razor UI
wwwroot/         CSS、JavaScript 與靜態資源
tests/           xUnit regression tests
```
