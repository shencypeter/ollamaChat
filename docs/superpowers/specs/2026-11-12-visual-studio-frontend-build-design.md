# Visual Studio 前端自動建置設計

## 目的

讓開發者從乾淨的 `main` clone 直接使用 Visual Studio Build／F5 時，自動取得與 Vue 原始碼一致的 `wwwroot/portal`，避免 Git 忽略的舊 bundle 被 ASP.NET 繼續提供。

## 採用方案

- 在 `AiTeachingAssistant.csproj` 建立共用 Vue MSBuild target，於一般 `Build` 前執行。
- `ClientApp/package-lock.json` 比 `ClientApp/node_modules/.package-lock.json` 新，或安裝標記不存在時，執行 `npm ci --no-audit --no-fund`。
- 每次一般 Build 執行 `npm run build`，由 Vite 清空並重建 `wwwroot/portal`。
- `dotnet publish` 沿用同一建置 target，只保留既有的發布檔案收集步驟，不重複執行 npm build。
- 保留 `SkipFrontendBuild=true`，供已明確準備好前端資源的隔離測試／發布流程使用。
- `wwwroot/portal` 維持 Git ignore；不提交平台相關的雜湊 bundle。
- 不啟動 Vite dev server，不增加 5173 或其他執行環境；網站仍由 ASP.NET 在 7173 提供。

## 未採用方案

- 每次 `npm ci`：正確但會讓每次 F5 都完整重裝依賴。
- Visual Studio/Vite proxy：支援 HMR，但需要第二個程序與 port，不符合目前單一 7173 環境。
- 提交 `wwwroot/portal`：容易產生大型雜湊檔衝突，且仍可能忘記重建。

## 失敗行為與驗收

- 缺少 Node/npm 或 Vue build 失敗時，整個 Visual Studio Build 必須失敗，不得悄悄啟動舊 bundle。
- 從沒有 `wwwroot/portal` 的乾淨來源執行一般 `dotnet build`，必須產生 Vue index 與 bundle。
- 第二次 build 不應無條件重跑 `npm ci`，但仍重建 Vue bundle。
- `dotnet publish` 必須只執行一次 Vue build並包含新資源。
