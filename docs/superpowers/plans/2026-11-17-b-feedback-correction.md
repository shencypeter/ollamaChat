# B-phase Feedback Correction Implementation Plan

**Goal:** 修正B大階段／子碼混淆、強迫挑錯及未來計畫誤當現在功能的回饋指引。

**Architecture:** 保留完整教授v3規則及共用HTTP傳輸；只在正式B的system追加簡短對照，B的user格式提醒取消強迫提出修改。使用stage-aware提示版本記錄實際變更；A/C/D及Studio原文／版本、Raw、四份profile及資料庫結構保持。

**Spec:** 本輪使用者「開始修正」，及docs/ai-tuning/03-evaluation-results.md的B短例診斷。

**Constraints:** 保留原文驗證、授權、不自動改碼／status或生成學生答案；只用合成模型測試。沿使用者指定的原工作區及唯一7173，保留dirty變更，不另建執行環境或提交推送。

## Steps

- [x] 擴充封包回歸：B有ABCD／子碼層級及時點對照、原文不變、A/C/D與Raw不增加B指引；B版本分開，現有預算可容納。
- [x] 加入B guide及B輸出提醒，workflow採VersionFor(stage)；先跑相關測試，再完整後端／隔離DB驗證既有歷史與新版本記錄。
- [x] 真正Provider重測七例並加入不同措辭／新情境，記錄剩餘失敗，不把格式通過當品質通過。
- [x] 正常VS建置，核對唯一7173後重啟並驗證，更新規格／交接／歷程與Ollama／評估說明。重啟清Raw，正式歷史保留；舊B版本失敗訊息不冒稱沿原提示重試。

## Review focus

同階段子碼不能要求換階段；純A放B仍要指出；混合A/B不可概括錯置；未來治療目標不是現在能力；來源／原文／模型比較與版本相容性不能因提示調校而失效。

## Evidence ledger

臨時hierarchy、ChatML、raw generate、簡化rules與user例子均有回歸，未採用。system短例＋簡短user提醒改善同階段判定及重複，但細碼／混合時點仍不足，正式變更須保留此限制並以新例驗證。

正式B增加annotations-first生成順序，沒有改欄位／驗證；仍單次呼叫。25項重點／329非DB＋56條件略過，其後完整隔離DB385/385及036→039升級指紋通過。B7704 bytes，原byte預檢不放寬，755中文字預檢通過。最終11次合成完整JSON，仍有細碼／混合時點／職災及status錯誤，不稱教學品質通過。metadata不可變trigger曾擋下測試的UPDATE，改為直接INSERT舊資料fixture，未改產品保護。

靜態審查無可行動問題；Chrome請求／標註11/11與標準VS含Vue通過。唯一7173已更新PID27984，qwen25-omni、部署Bv4／Av3、頁面200／匿名AI401核對。無Migration或正式資料變更，文件同步、未提交推送。
