PZ Server Manager 語言包
========================

1. 複製 en-US.json，改成新的檔名。
2. 修改 code（不可與其他語言重複）與 displayName（下拉選單顯示名稱）。
3. translations 左側是程式原始繁中文字串，請勿修改；只翻譯右側內容。
4. 保留 {0}、{1}、<LINE> 等佔位符，檔案必須儲存為 UTF-8。
5. 回到儀表板按「重新載入翻譯」，不必重開程式。

缺少的翻譯會安全回退成繁體中文。伺服器主控台輸出、設定值、玩家名稱、
模組名稱與 Project Zomboid 自己產生的訊息不會被翻譯或改寫。

Language pack notes
===================

Copy en-US.json, assign a unique code and displayName, then translate values only.
Keep placeholders such as {0}, {1}, and <LINE>. Save the file as UTF-8 and use
"Reload translations" on the Dashboard. Missing entries fall back to Traditional
Chinese. Raw server output and user/server data are intentionally left untouched.
