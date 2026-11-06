# Swagger API 文件與測試介面

2026-11-06 已加入 Swashbuckle.AspNetCore 9.0.6，支援目前 .NET 8 控制器 API。僅在 Development 啟用；正式環境 `/swagger` 與子路徑回 404，不由 Vue fallback 偽裝成文件頁。

- Swagger UI：`/swagger`（例如 `https://localhost:7173/swagger` 或 `http://localhost:5084/swagger`）。
- OpenAPI JSON：`/swagger/v1/swagger.json`。
- 現有執行程序需要重新建置／啟動才會載入新增服務。既有 http／https launch profile 已使用 Development，不需修改啟動設定。

## 操作方式

可先在同一網站正常登入，再開 Swagger；或展開 Auth 的 `POST /api/auth/login`，填入本人的登入資料後按 Try it out／Execute。HttpOnly 登入 Cookie 由瀏覽器保存並自動帶入，不需在 Authorize 手動貼 Cookie，也沒有新增 JWT 或另一套登入。

Swagger UI 對 POST／PUT／PATCH／DELETE 等寫入操作，會先以同一 Cookie 工作階段呼叫 `/api/auth/csrf`，再附 `X-CSRF-TOKEN`。每次重新取得 token，以處理登入／登出後身分變化。GET／HEAD／OPTIONS 不額外請求 token；token 取得失敗或 API 請求跨網站時停止送出，不放寬伺服器防偽驗證。

文件依控制器自動產生、可搜尋；需要登入的端點標示 Cookie 驗證與 401／403。Try it out 是實際 API 操作，仍依目前帳號、有效課程、正式／練習、期限及封存政策授權，不因開發文件取得管理權。API 文件不內嵌真實帳密、初始化碼、連線字串或學生內容。

資源與 PDF 上傳的 IFormFile 移除多餘的 FromForm 標記，沿用 ASP.NET Core 對檔案的自動表單綁定，產生 multipart/form-data／binary 欄位；文字表單欄位保留原標記及驗證。不改檔案大小、格式、課程或所有權限制。

## 本輪驗證與部署狀態

- 專案 build／publish 成功，0 警告／錯誤；成品 `.codex-build/swagger-release`。
- 新增 SwaggerDocumentationTests，直接產生全部控制器文件並驗證主要路徑、登入標示、匿名登入及資源／PDF multipart 欄位。本環境 VSTest 無法連線 testhost，改以同程序執行該測試方法，已通過；未宣稱完整後端回歸通過。
- 以同程序 RequestDelegate 驗證 Development 的 UI、初始化 JS、OpenAPI JSON 皆成功，實際 92 個 API 路徑；Production 相同三路徑 404。未開放網路連線或使用真實學生資料。
- Node 檢查實際 interceptor 程式：GET 不取 token、各種寫入取得新 token、同源 Cookie、原 body 不變、跨源與 token 失敗拒絕，皆通過。
- 本環境不能管理原使用者服務或連線既有本機預覽，故未更新／重啟 5097 或 Visual Studio 服務，沒有完成真實瀏覽器登入／Try it out 回歸。啟動本輪專用前景程序僅驗證可啟動後已終止，未修改原服務／Data Protection 設定。未更改資料庫或呼叫模型。

參考：[Microsoft .NET 8 Swashbuckle 文件](https://learn.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-swashbuckle?view=aspnetcore-8.0)、[套件版本與 .NET 8 依賴](https://www.nuget.org/packages/Swashbuckle.AspNetCore/9.0.6)、[檔案表單標記說明](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/2996)。
