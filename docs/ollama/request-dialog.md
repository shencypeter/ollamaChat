# 回覆效能與實際 Ollama 請求

## 2026-11-17 本人失敗請求與原因

本課teacher／TA自己當次Stage／Raw錯誤，會顯示固定診斷碼、固定說明及可用的HTTP狀態；「查看失敗請求」直接開既有dialog的Ollama頁籤，顯示當次真正送出的payload。輸入預檢尚未送模型時沒有payload，不重建。沒有回覆量測時仍明示無量測。

完成後重新檢查課程角色，學生POV、只修課的教授、學生及已降權者均不提供；前端同步清除／隱藏。沿只留HTTPscope及頁面記憶、不入歷史／DB／log／storage的限制；不回傳任意上游error body、stack、inner exception或thinking。服務已知錯誤只轉固定分類，未知原因明說未取得，不把所有失敗叫模型拒答；舊failed紀錄不憑粗略failure_code重建詳細原因。

模式列已改為可鍵盤操作的頁籤，教師紀錄亦為授權頁籤；本次Vue288、Chrome30、完整隔離DB394通過，含新失敗payload與降權回歸。

更新：2026-11-17。課內「查看回覆效能」新增「統計／Ollama請求」分頁，供本課教師及TA檢視本人新回覆的實際請求。

## 操作

1. 重新整理，以教師／助教模式進入課內AI助教。
2. 在Raw或四階段送出新訊息，回覆後直接點「查看 Ollama 請求」；也可點「查看回覆效能 → Ollama請求」。僅本課教師／TA本人模式提供。
3. 查看實際模型、端點、依序訊息、format及options。單次「完整請求JSON／複製JSON」是實際HTTP body的payload，不是診斷wrapper；多次請求則明確標示payload陣列。
4. 選另一回饋輪次會顯示該筆回覆的擷取。舊紀錄、重新整理或離開模式後明示不可用，不以目前設定冒充原請求。

這是應用程式送到`/api/chat`的JSON，不是模型服務內部模板展開後的token紀錄，不包含私人thinking或HTTP憑證標頭。

2026-11-17修正：四階段payload只含目前階段system與本次user作答，舊草稿／AI回覆留DB而不再送入模型；Raw多輪不變。階段提示仍含共用分類代碼表，以辨識跨階段內容。長度預檢413拒絕時沒有送到Ollama的payload，不能假造。舊回覆／頁面重新整理後請送出新作答再查看。新的直接入口及兩則stage訊息已以桌面／手機淺深色測試，截圖更新。

## 授權與生命週期

實際課程教師／TA本人可檢視；平台教授若在該課僅為學生，仍不得擷取。學生、學生視角預覽及教師查閱他人學生歷程不提供此請求。模型呼叫前查課程資格、明確啟用request-scoped conversation capture，完成後重查staff角色；降權／退選不回傳。

擷取僅在本次HTTP scope及Vue頁面記憶體，不寫DB、Session、Redis、瀏覽器storage、一般log或diagnostics；模式、帳號、角色、學生視角、Raw清除／失效及頁面離開時釋放。Stage歷程／效能持久化不改，Raw仍無應用system／持久化／正式統計。冪等重送若未呼叫模型，不產生新擷取。

URL去掉使用者、密碼、query及fragment，payload遞迴移除thinking；conversation不擷取模型response body。三個profile、傳輸JSON與公開既有欄位不改，無Migration。

## 程式及驗證

- `Services/OllamaClient.cs`在真正序列化的HTTP body上擷取，沒有另組推測請求。
- `Services/OllamaRequestInspection.cs`只在request scope接受明確課程授權；Controller只在本人成功send／retry／Raw結果追加選用requestInspection。
- `CaseAnalysisChat.vue`按replyTo/requestId或Raw sessionId＋assistant位置配對，不把擷取放進messages／下一輪模型輸入。
- `ChatPerformanceDialog.vue`安全文字顯示、JSON複製、native dialog及頁籤鍵盤操作。

一般後端318通過／53外部DB條件略過，另隔離DB完整371/371且schema已刪；Vue273、Chrome22及標準VS含Vue通過。測試包含HTTP實際序列化比對、教師／TA／學生、教授在課內為學生、預覽、完成後降權、冪等重送、歷史不洩漏、DB無request wrapper、逐回覆及Raw清除。模型使用HTTP mock，本輪未實呼叫LLM、不宣稱教學品質驗收；原服務端可能留存資料的限制不變。

以下均為合成API資料，非正式病例／學生原文：

![桌面請求分頁](request-dialog-desktop.png)

![手機深色請求分頁](request-dialog-mobile.png)
