# 2026-11-17 輸入截斷診斷

使用者懷疑Taipei模型限制輸入長度。本輪只診斷，不切換模型、不修改四份profile、遠端Modelfile或正式Prompt，不重啟7173，不寫入DB。以下全部為新合成資料，未重送真實學生作答。

## 方法

直接使用各profile的端點及模型呼叫`/api/chat`，單一system＋單一user；文件首／中／尾各放不同測試key，中間填固定規則產生的archive rows。輸出schema只有head／middle／tail三個string，並未洩漏預期答案。固定temperature=0、seed=42、num_predict=128；只變動文件長度及num_ctx。同步讀`/api/ps`確認載入context。探針暫存在忽略的`.codex-build/context-probe.cjs`，未進應用程式或持久化聊天服務。

這是傳輸／容量實驗，不是臨床評量，也不是公平速度比較。測試未經過應用程式的12,000-byte輸入預檢，故刻意超長資料不代表正式頁面可接受。

## 觀察

| 模型 | rows／user bytes | 請求num_ctx | prompt_eval_count | 結果 |
| --- | --- | --- | --- | --- |
| Taipei Omni | 30／1547 | 16384 | 679 | 三個key全對，stop |
| Taipei Omni | 500／23167 | 4096 | 2050 | 三個key未正確回傳，stop |
| Taipei Omni | 500／23167 | 16384 | 10079 | 三個key全對，stop |
| Taipei Omni | 1000／46167 | 16384 | 8194 | 三個key未正確回傳，stop |
| Taipei Breeze | 30／1547 | 16384 | 765 | 三個key全對，stop |
| Taipei Breeze | 500／23167 | 4096 | 2051 | 三個key未正確回傳，stop |
| Taipei Breeze | 500／23167 | 16384 | 11105 | 生成無法解析，128輸出tokens／length，不能用此回覆判定key可讀性 |
| Taipei Breeze | 1000／46167 | 16384 | 8195 | 三個key未正確回傳，stop |
| IAN02 Llama3 | 500／23167 | 16384 | 6588 | 三個key全對，stop |
| IAN02 Llama3 | 1000／46167 | 16384 | 13088 | 三個key全對，stop |
| IAN02 Llama3 | 1500／69167 | 16384 | 8194 | 首／中key遺失，尾key正確，stop |

上述每輪`/api/ps`均回報與請求相同的context_length。Taipei version=0.40.11，IAN02=0.40.16（沿實際服務回傳值，未推定對應upstream commit）。不同模型tokenizer不同，相同bytes不能直接比較容量；1000 rows於Llama3仍在容量內，故另增1500 rows測溢位。

另用現行`OllamaClient.CaseFeedbackAsync`及真正教授B階段Prompt／compact schema，傳全新合成骨折病程：promptTokens=2375、responseTokens=747、done_reason=stop、約8.3秒、完整JSON及1筆逐字引用通過，但仍含無據分類碼。此結果不支持「所有分類失準都是輸入超限」。未讀取或保存真實案例內容。

## 結論與限制

- Omni不固定卡在原生num_ctx4096：請求16384時已實際處理10079 tokens，並找回首／中／尾key。模型原生預設與載入容量不可混為一談。
- 輸入超過指定容量時，兩台服務均出現回報token量降至約半個context、前文key遺失而HTTP200／done_reason=stop的現象，與伺服器端截斷相符。不能靠done_reason判定輸入完整；length主要辨識本次輸出達上限。
- 並無證據顯示Taipei獨有一個低於請求值的固定硬上限；也無證據能還原使用者先前失敗的實際上游封包。未取得host／proxy／runner log，因此不宣稱已定位實際截斷程式行或所有情境都不會截斷。
- Taipei模板僅System＋Prompt（Breeze另有INST），與IAN02的角色模板不同，仍是遵循問題的獨立待查因素；本輪未修改模板，亦未證明模板為根因。
- 正式四階段只送目前phase system與本次作答，DB舊稿不重送。CaseChatBudget保守預檢超量即413，沒有自行裁字；它是UTF-8 bytes估計而非模型tokenizer，亦非伺服器端無截斷的形式保證。Raw完整多輪政策維持。
- 本輪沒有增加context、放寬輸入預檢或自動重送。分類品質與輸入／輸出容量須分開驗收。

官方[context說明](https://docs.ollama.com/context-length)建議核對實際載入容量；[Chat API](https://docs.ollama.com/api/chat)定義prompt_eval_count與done_reason。[目前upstream實作](https://github.com/ollama/ollama/blob/main/server/prompt.go)含超限截斷邏輯，但不能視為兩台服務版本的逐行證據。
