# Chat tabs, safe teacher diagnostics and Chinese input capacity

**Goal:** 將五個聊天模式及授權的紀錄入口改頁籤；教師本人當次失敗可看安全原因／實際請求；修正2125中文字被byte/token混用擋下。

**Scope:** 沿現有CaseAnalysisChat／OllamaClient與capture授權，無新聊天模組或資料表；原工作區及唯一7173。保留dirty變更與所有歷史，不推送。

**Design:** 固定診斷碼／固定說明／HTTP狀態，不回傳任意上游本文或inner exception。控制器完成後重查課程teacher／TA，排除學生POV／平台教授但僅修課及降權；診斷與失敗payload只在HTTP／頁面，不寫DB/log。未知原因明說未知，不把驗證失敗叫模型拒答。

**Capacity:** byte安全上限和估計token分開；只有Omni profile改24KB byte cap、常用CJK估2token／字、512token傳輸預留。ASCII及其他Unicode沿UTF8保守估算，其他profile預設維持原算法。不是模型tokenizer的精確保證，仍預留輸出、拒絕過長輸入、不裁字、不加歷史。

**Checks:**

- [x] 先以合成2125字五段文重現413，分離byte／token後通過；長ASCII仍拒絕。
- [x] 先確認UI缺tabs與診斷測試失敗，完成role=tablist/tab/tabpanel、manual activation、左右/Home/End焦點及未儲存提醒。
- [x] 教師／TA／學生／降權後資料庫授權回歸及已存在封包／隱私測試。
- [x] 補瀏覽器失敗dialog／頁籤測試，完整前後端、模型合成長文、部署驗證及文件更新。

**Risks:** 不還原舊失敗詳情、不保證推測出模型拒答動機；估算不是遠端tokenizer。保留12KB的其他模型與正式提示版本，不為容量改提示。無Migration。

**Evidence:** Vue288、Chrome30、完整隔離DB394及036→039升級指紋通過、schema刪。真正Provider2125字合成文原文完整、2messages、HTTP200／prompt4031／response567／stop。靜態審查無可行動問題。標準VS含Vue成功、唯一7173 PID36752、部署24k／2／512核對，頁面200／匿名401、index-5twnEPzG.js。使用者另回報重複長評語，僅讀metadata4015prompt／1762response／stop、feedback600／有效ann5，不是硬容量失敗；未重送病例或擅自新增分段管線。
