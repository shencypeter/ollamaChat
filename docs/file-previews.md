# 檔案預覽（2026-10-30）

課程資源圖片顯示 180×120 縮圖，點擊展開；PNG、JPEG、GIF、WebP 支援。PDF 於按下「預覽 PDF」後載入原生閱讀器，PDF 報告檢閱同樣適用；保留下載按鈕及不支援瀏覽器的提示。

## API 與權限

- GET /api/courses/{course}/resources/{id}/preview：有效課程成員與資源班別限制同下載；練習模式只共用。讀取不增加下載計數。
- GET /api/reports/{id}/submissions/{submission}/preview：同原報告下載，只限本人或所屬課程教師／助教；practice 不讀正式學生報告，保留 report.previewed 稽核。
- 資源列表 previewType 由副檔名及前 16-byte header 推導，與實際回傳再次核對；不依賴上傳端宣稱 MIME。拒絕 HTML／SVG、偽裝格式與不支援檔案 inline，415 提示下載。
- API 回應 no-store、nosniff；預覽不提供公開靜態網址。Vue 以原 api helper 帶 Cookie／學生體驗 header fetch，使用暫時 blob URL，目標變更或元件卸載即釋放。不持久快取預覽。

## 實作界線

圖片為原檔在前端限制尺寸，尚未產生壓縮縮圖；每份仍受原 20 MB 限制，圖片多時可能增加流量。PDF 使用瀏覽器內建閱讀器，不產生首頁圖或逐頁 PNG；Chrome 已實際顯示合成 PDF 內容。手機／停用 PDF 閱讀器／損壞檔案可能不能內嵌閱讀，始終保留下載備援。若需所有裝置一致的 PDF 縮圖，可另評估上傳後背景產生衍生圖，並沿用原檔權限；本輪沒有新增此管線。

無遷移、無模型請求、舊檔適用。驗證：後端 157、前端 33、Vite build／publish 通過；focused studio-files.spec.js 於 5107 與最終 5097 各 1 項通過，合成 QA 已清理。

## 2026-10-30 文件名稱

上傳資源可填200字內文件名稱，資源卡片可事後編輯，空白回原檔名。名稱與原始file_name分開，下載檔名與檔案內容不變。教師／助教限定，學生只讀，practice／封存拒寫；PUT /api/courses/{course}/resources/{id}/title帶title及expectedVersion，過期409。014新增display_title／title_version，舊檔保留回退行為。逐列未存名稱與版本不被其他列刷新覆蓋。

本輪167後端（DB啟用）、43前端、build／publish，最終5097 titles／files Chrome/API共2通過；001–014重跑略過，合成QA已清理。
