# 測試範圍與日常執行

測試數不是開發目標。整個平台涵蓋課程、學期、教材、作業、成績、授權及AI；四模型、四階段、角色及邊界輸入的參數化案例，會由runner展開成多項結果。不要將總數理解為同一介面有數百個獨立流程。

## 日常修改：先跑受影響範圍

在`ClientApp`執行：

```powershell
npm run test:case-chat
npm run test:e2e:case-preview
npm run build
```

第一項只驗`CaseAnalysisChat`元件，涵蓋分頁、草稿、角色、診斷及送出狀態；第二項只跑教師學生預覽的桌面／手機流程（同一案例中走完A–D），不是全部瀏覽器回歸。

修改其他Vue元件可直接指定檔案，例如：

```powershell
npm test -- src/components/PhaseAnnotatedAnswer.test.js
```

只改後端一項邏輯時，可選對應class，例如：

```powershell
dotnet test tests/AiTeachingAssistant.Tests/AiTeachingAssistant.Tests.csproj --filter "FullyQualifiedName~CaseChatCapacityTests"
```

若目前shell只有Node、沒有npm，對應CLI可直接執行：

```powershell
node node_modules/vitest/vitest.mjs run src/components/CaseAnalysisChat.test.js
node node_modules/@playwright/test/cli.js test studio-case-chat.spec.js --grep "teacher student preview"
node node_modules/vite/bin/vite.js build
```

## 合併／發布及共用邏輯變更：完整回歸

保留原`npm test`、`npm run test:e2e`與後端完整測試。授權、成績發布、Migration、持久化或共用LLM封包變更應涵蓋相關整合測試；資料庫案例必須使用獨立測試schema，不能對正式課程跑寫入測試。runner列為skipped不算已通過。

## 如何精簡

- 同一行為的輸入變化優先用參數化測試或在一個主要流程中驗證，不為每項文字／按鈕另建瀏覽器流程。
- 合併重複setup與相同風險的重複斷言，保留有不同失敗原因的案例。
- 保留角色隔離、跨課程／帳號存取、未發布成績、重複提交及歷史不可變性等保障。
- 以捕捉回歸的能力及執行耗時判斷是否值得保留，沒有必須達成500或600項的門檻。

這次只新增一個元件工作流程與兩種瀏覽器尺寸的預覽案例；既有完整回歸沒有為降低總數而刪除。
