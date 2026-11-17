# Teacher model comparison and long B feedback correction

**Goal:** 教師／授權TA可逐次選現有模型，DB沿現有model_name記錄實際模型；只修正B長文評語循環，保留標註與其他階段行為。

**Architecture:** 白名單profile resolver建立每請求獨立OllamaClient/options，不修改全域IOptions或其他使用者。正式回合在開始時鎖定runtime，user／assistant沿現有model_name、prompt_version欄位保存；重試沿原模型，切模型比較用新requestId。Raw仍不入DB，模型綁暫存session並於切換時清空，不保存scoped provider。

**Feedback scope:** B且原文至少1000字時加自然語言評語提醒；若評語出現機械重複或大量箭頭編碼配對，最多再呼叫一次feedback-only整理。只送本次原文及已驗證的分類參考，不送壞評語、舊稿或舊回饋；保留第一輪annotations，不以程式猜碼或產生教師評語。再次循環明確失敗，不保存垃圾assistant。不是臨床品質評分。

**Constraints:** 原工作區、唯一7173、保留dirty及真實作答；模型測試全合成。四profile／source precedence保持，不新增平行Ollama transport。沒有新DB欄位或Migration：已有model_name足以區分現有四個不同tag；不宣稱紀錄完整主機／採樣快照。Raw不因比較而入DB。

## Tasks

- [x] backend白名單resolver與授權；正式回合actual-model持久化／並發隔離／重試／idempotency測試。
- [x] Raw session綁model choice的純設定快照，provider仍request scoped；切換UI與學生選模阻擋。
- [x] UI下拉、保留正式草稿、Raw切模確認、歷史每輪實際模型；前端由獨立代理負責，契約先固定。
- [x] B長文機械循環守門／一次評語整理與量測合計；mock驗證annotations不變／無歷史／最多兩次／失敗不保存。
- [x] 全回歸、合成長文實呼、靜態審查、標準VS建置與唯一7173部署；更新三持久化文件及相關規格。

## 完成驗證（2026-11-17）

另依新Raw短句證據，僅Omni新增選用Qwen ChatML generate協定；其他模型不改。修正切模式／立刻選模競爭與唯讀Raw載入狀態。最終隔離DB406、Vue301、Chrome32及標準VS含Vue通過，schema刪除。實際合成Raw兩輪／B2125完整stop。唯一7173 PID7332，正式DB歷史保留，無Migration，未提交推送。設計、證據及限制見docs/ollama/model-selection.md；本輪停止，不新增評分／AI模組。
