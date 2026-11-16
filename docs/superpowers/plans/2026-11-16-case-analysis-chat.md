# Case Analysis Chat Implementation Plan

**Goal:** 同課程提供Raw暫存完整LangChain聊天與四階段持久聊天、可重算統計及教師查閱。

**Architecture:** 共用Ollama transport；專用Raw記憶store與Stage Npgsql workflow分離。沿用Vue課內導覽、授權、CSRF與Migration。使用者已明確要求直接執行。

**Spec:** ../specs/2026-11-16-case-analysis-chat-design.md

## Tasks

- [x] 透明ConversationAsync＋LangChain Raw memory：角色／48則buffer／清除並行／隔離／容量，三profile舊payload回歸。
- [x] Stage持久workflow：037與CourseSummary、舊版prompt、先存user、租約／重試／統計；真實DB授權／失敗／並行／晚回覆測試。
- [x] 五模式Vue＋教師紀錄＋設定：Raw／Stage隔離、清除／刷新／BFcache／429恢復、重試與權限；課內導覽。
- [x] 舊036升級037、既有示範課可重入、全套build/tests及7173更新；Ollama metadata與合成傳輸、交付文件。

## Review focus

清除／過期與正在推論的晚回覆；相同requestId重送與同user重試；退選／封存／TA限制中途改動；逾長context原history不丟；Raw永不觸發DB聊天、summary或診斷捕捉。既有作業／測驗與三profilepayload不可改變。
