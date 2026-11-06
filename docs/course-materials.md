# 課程教材：標籤與 YouTube 回放

更新：2026-11-06。

## 操作與權限

在課程「教學資源／講義」選擇上傳檔案或 YouTube 課程回放，填寫名稱與教材標籤。教師／助教可新增、改名與修改標籤；學生只能查閱自己課程的教材。封存課程不接受新增或編輯。每個課程獨立，沒有跨課共用資源。

標籤可自訂週次、主題，每份最多 10 個、每個最多 30 字。編輯器以逗號、分號或換行分隔，會去除空白及不分大小寫的重複標籤；第１周、Week 01 統一為第1週。週次由授課者指定，不以檔案上傳日期推算。學生可按週次／主題、未標記及名稱／檔名／標籤搜尋；清單保留上傳日期與由新到舊排序。既有教材預設無標籤。

標籤與名稱各有獨立版本，過期修改回傳409，不覆蓋他人變更；未存標籤離頁有提醒。標籤與改名不改原始檔案、下載次數或既有學習時間。

## YouTube 回放

接受單一 YouTube 影片的 watch、youtu.be、embed、shorts 或 live 網址；只儲存經白名單驗證的11碼影片ID，使用官方 IFrame API 及 youtube-nocookie.com 播放器，禁止任意HTML或外站嵌入。伺服器不下載、代理或複製 YouTube 影片。回放沒有檔案下載次數；回放網址指向的影片ID不可覆寫，換影片須新增教材，避免沿用另一影片的時間。

學生點「觀看課程回放／計時」進入平台播放器，正常播放自動開始；教師一般預覽不計入正式學生時間。影片須開放嵌入及符合 YouTube 觀看權限；載入失敗、私有／下架／限制嵌入會顯示錯誤且不累計。另開 YouTube 網站觀看不納入本平台紀錄。

## 逐字稿

新增YouTube教材時可貼上逐字稿，也可在回放頁「課程回放逐字稿」由本課教師／助教新增、修改或清除。最多100,000字，保留換行與文字時間標記，純文字呈現，不執行HTML；學生同頁唯讀。獨立版本、過期修改409、保留未存草稿與離頁提醒，封存／跨課／學生不允許寫入。尚無自動轉錄、YouTube字幕擷取或時間標記同步跳播。

## 有效時間與統計

沿用017教材計時：10秒心跳、以伺服器經過時間和影片位置共同設上限；超25秒中斷不計，背景、暫停、緩衝停止；支援0.25至4倍速，切換速度關閉原片段並另起計時，快轉跳過不計，重看可累積。暫停／結束時保存有效尾段並關閉session；整秒採無條件捨去，避免未滿一秒提前解鎖。單人跨分頁／裝置僅一個有效計時，正式與本人練習隔離。

依使用者2026-11-06確認，**影片門檻採有效影片內容時間**：2倍速實際看10分鐘，計入20分鐘；0.5倍速看10分鐘計入5分鐘。實際觀看時間、內容時間、各播放速度分段統計分開保存／呈現。內容時間依有效實際秒數乘該片段速度，仍受伺服器經過時間×速度及影片位置上限，不將快轉跳過計入；非影片講義仍以實際時間計入。既有1倍速紀錄等量保留，舊已開始測驗的依據不改。

教材卡片「查看學習時間」僅供本課教師／助教，顯示有效學生數、已有紀錄人數、實際及內容合計時間、逐位學生實際／內容時間、各播放速度的觀看／內容時間與最近觀看；未觀看者列0，不列教師預覽或練習時間。學生只能讀本人時間。回放可作為既有測驗的教材最低時間門檻，未達標仍由伺服器拒絕開始及作答。

這是平台活動紀錄，不是 YouTube 官方觀看數、注意力／理解證明或不重複觀看比例。瀏覽器活動與影片位置訊號有其限制；沒有將歷史播放或站外觀看回填成時數。

## 資料與 API

018新增 course_resources.tags／tags_version；019新增 resource_type／youtube_video_id，舊檔案維持file，YouTube使用空bytes與固定ID。020新增逐字稿／版本及session固定播放速度／內容毫秒；既有017保存計時與測驗門檻，原1倍速紀錄內容時間等量承接，沒有新造舊觀看事件。無成績重算或真實教材搬移。

- GET /api/courses/{course}/resources：可加q、tag，均限本課。
- POST /api/courses/{course}/resources：multipart追加重複tags欄位。
- PUT /api/courses/{course}/resources/{id}/tags：tags陣列與expectedVersion。
- POST /api/courses/{course}/resources/youtube：title、url、tags、選填transcript。
- GET /api/courses/{course}/resources/{id}/learning/statistics：本課教師／助教正式統計。
- GET／PUT /api/courses/{course}/resources/{id}/transcript：讀取／修改逐字稿，寫入帶transcript與expectedVersion。
- learning sessions開始及心跳帶playbackRate（預設1，0.25–4），單session固定速度；心跳close關閉分段。回應同時包含accumulatedSeconds（實際）與contentSeconds（內容）。
- 測驗learningGate的accumulatedSeconds為門檻計入秒數，影片採內容、文件採實際；本人歷程原實際時數不混入倍速換算時間。

播放器事件依據：[YouTube 官方 IFrame API 文件](https://developers.google.com/youtube/iframe_api_reference)。
