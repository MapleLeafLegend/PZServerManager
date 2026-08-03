# PZ 伺服器管理器

目前版本：**v2.0.0**

開發與協作：

- 專案創建與維護：**MapleLeaf**
- AI 開發協作：**[OpenAI Codex](https://github.com/codex)**

授權：**PolyForm Noncommercial License 1.0.0**（僅限非商業用途；此為
source-available 授權，不是 OSI 開放原始碼授權）。完整條款請見
[`LICENSE`](LICENSE)，風險與第三方權利說明請見 [`DISCLAIMER.md`](DISCLAIMER.md)。

前置安裝精靈會顯示下載百分比、已執行時間、目前步驟與 SteamCMD 即時輸出；作業期間會鎖定衝突操作，完成或失敗時會保留明確結果。

適用於 Windows Server 2022 Desktop Experience x64，以及具備桌面圖形元件的 64 位元 Windows。Server Core 因缺少 WPF 圖形元件而不支援。

## 第一次使用

1. 將 ZIP 完整解壓縮。
2. 執行 `PZServerManager.exe`。
3. 依畫面導引準備 SteamCMD 並安裝 Project Zomboid Dedicated Server。

程式為自含式發布，不需要另外安裝 .NET、Visual Studio 或其他開發工具。請勿直接在 ZIP 壓縮檔內執行 EXE。
請保留 EXE 旁的 `Languages` 資料夾；其中包含繁中、英文與自行翻譯用的 UTF-8 JSON 範本。

## 主要功能

- 從 Valve 官方來源下載 SteamCMD，或指定既有的 `steamcmd.exe`
- 安裝及更新 PZ Dedicated Server（Steam App ID `380870`）
- 尋找既有 PZ 安裝、資料目錄及伺服器設定
- 「設定檔管理」整合掃描與重新整理，可建立不含目前 GUI／名稱／密碼／MOD 的乾淨 Build 42 Stable `VERSION = 6` 設定檔
- 可複製設定檔並重新命名；不複製世界、角色或玩家資料庫，自動更新 `ResetID`，並可清除密碼與避開其他設定檔的連接埠
- 讀取現有 INI 與 `SandboxVars.lua` 後再開放修改
- 未讀取或檔案被外部修改時拒絕覆寫
- 保留 GUI 未管理的設定及原始檔案編碼
- 支援 UTF-8、UTF-8 BOM、Big5、UTF-16 LE／BE
- 強制 Big5 時會拒絕誤讀有效 UTF-8，並提供已落盤亂碼的可逆修復模式
- 儲存後直接從磁碟逐欄核對所有 GUI 管理值；缺少欄位、寫入不同值或重讀不同值均不回報成功
- 儲存前顯示待寫入差異摘要；GUI 顯示尚未儲存狀態，外部修改衝突仍會阻止覆寫
- 切換資料目錄或設定檔名稱後，讀取與儲存固定使用同一組路徑，不會被舊 Manager 設定切回其他伺服器
- 同一 INI 若存在重複設定鍵，儲存時會將所有同名鍵同步為同一值，避免 GUI 與遊戲讀到不同資料
- 自動編碼依序辨識 BOM、嚴格 UTF-8、Big5，不受 Windows 顯示語言影響
- 設定寫入前自動建立 `.manager-backup`
- 即時控制台、管理指令及獨立立即存檔
- 指令改用非阻塞背景佇列；PZ 標準輸入停止讀取時，GUI 仍可操作
- PZ 回報 `SERVER STARTED` 後才開始 CLI 健康檢查，避免模組／世界載入期間誤報
- `players` 必須實際回傳人數才算健康；第一次逾時於 30 秒後複查，連續兩次逾時會顯示紅色警報並暫停全部自動化
- 伺服器運行中仍可即時關閉定時重啟、Workshop 檢查與公告，也可用單一按鈕停止全部自動化
- 手動輸入 `quit` 會取消本次工作階段的排程與自動重開，避免關服後又被倒數流程拉起
- CLI 卡死時不會自動殺程序；紅色警報中僅在使用者再次確認後，才可終止這次由管理器啟動的 PZ 程序樹
- 主控台批次更新與容量上限，避免大量關服日誌造成介面凍結
- GUI 記錄按日持久化並可自訂保留天數；可搜尋記錄、暫停自動捲動及開啟記錄資料夾
- 可執行 SteamCMD、PZ 目錄、資料目錄寫入、連接埠、磁碟與記憶體環境預檢
- 可匯出隱私清理診斷包；密碼、Steam64 ID、IP、MAC、Windows 使用者路徑及 MOD 訂閱清單不會被放入診斷包
- 「診斷與記錄」會在啟動及進入頁面時自動刷新目前環境摘要，列出 SteamCMD、PZ 啟動器、目前 INI／Sandbox VERSION、編碼、連接埠、磁碟與記憶體狀態；手動檢查不會建立缺少的資料目錄或修改 PZ 設定檔
- `save` 後 `quit` 的安全關服
- 自訂倒數公告、排程重啟及重啟前 ZIP 備份
- 排程總覽集中顯示定時重啟、玩家查詢與 Workshop 檢查；歡迎訊息可附加下次定時重啟時間，不包含 MOD 更新重啟
- 僅支援目前 Build 42 Stable Sandbox `VERSION = 6`；舊版檔案只讀不覆寫
- Build 42 Stable *LootNew 小數物資倍率
- MultiplierConfig.Global 全域經驗倍率
- HoursForLootRespawn 由 SandboxVars 讀寫
- 創角額外點數及固定出生物品設定；物品 ID 只驗證、不自行增刪或替換
- Build 42 首次建立資料庫時檢查管理員密碼
- 偵測預設角色資料庫初始化錯誤並提供非破壞性排查指引
- 啟動批次檔意外停止或停在 PAUSE 時，不依賴 UI 執行緒即可回收外殼程序
- 舊設定若保存了不相符的編碼模式，啟動時自動回復「自動」，不再於顯示視窗前退出
- Workshop 只需輸入數字 ID；管理器會列出 Steam「Required Items」候選，由使用者勾選後才加入，不會把作者列出的可選功能強制安裝
- MOD 清單可依 Workshop／Mod ID、名稱、狀態、依賴與地圖搜尋及篩選，並可批次勾選／取消目前結果
- 「檢查全部」會重新取得所有 Steam 中繼資料；「檢查本次新增」使用本機快取，只查詢新加入的 Workshop，減少重複等待
- Workshop 依賴頁面採節流與隨機間隔；Steam 回傳 429 時依 `Retry-After`／指數退避重試，持續受限便停止本次後續網路查詢並沿用成功快取，不會把既有依賴誤存成空白
- MOD 表格保留目前 INI 載入順序，會從同一模組的 root／common／42.x 變體中選取目前 Build 42 版本，不混入舊 B41 Mod ID
- 依 `require`、`loadBefore`、`loadAfter`、`loadModBefore`、`loadModAfter` 排序；硬依賴與非強制作者排序目標分開標示
- 同一 Workshop 含多個 Mod ID 時逐項列出，不會再把本體、互斥版本與相容補丁全部強制啟用
- 可辨識同一 Workshop 內「一般版／Aiming Requirement」等互斥 Mod ID，同時勾選時拒絕套用
- 自訂地圖必須同時具有 `map.info` 與實際 world cell 資料才會列入，避免把語言包的地圖標註誤判成世界地圖
- 地圖若包含 `spawnpoints.lua`，可另外勾選加入角色建立時的重生區域；管理器只維護 `_spawnregions.lua` 中有明確標記的區塊
- 依實際 Lua／資料檔結構標示「純客戶端候選」、「伺服器／雙端必需」或「需人工確認」
- SteamCMD 檢查／修復使用完整非同步等待與例外隔離，不因外部程序異常關閉 GUI
- 即使 Sandbox 不相容，仍優先由正常 INI 回填基礎欄位，避免 Manager 舊亂碼覆蓋畫面
- 啟動時依 VM 內 INI 的實際編碼設定 JVM `file.encoding` 與管理指令串流，不修改 HOST／NODE 或 VM 全域語系
- 含 `�` 或連續 `???` 的不可逆文字會拒絕覆寫，要求由備份還原或重新輸入
- 儀表板顯示目前在線玩家；啟動後立即查詢，之後依自訂間隔（預設 30 分鐘）以官方 `players` 控制台指令更新
- `players` 查詢間隔可於啟動前設定為 1–1440 分鐘；伺服器運行期間欄位會鎖定，停止後自動恢復
- 不使用 Build 42 已知有 Unicode 封包問題的 RCON `players`，同時只允許一個短時間查詢
- Workshop 更新檢查與等待玩家離線公告預設皆開啟，且各自有獨立時間；預設分別為 5 與 30 分鐘，公告是檢查功能的子選項
- 以 Valve Workshop 公開 API 的 `time_updated` 對照本機 `appworkshop_108600.acf`，缺少可靠本機紀錄時不會猜測或重啟
- 偵測到模組更新且無玩家時走既有安全存檔／關服／重啟流程；有人時立即公告一次，後續依自訂間隔公告，直到下一次確認無人才重啟
- 啟動驗證不再顯示「已套用 Mod／尚未寫入 INI」成功視窗；自動重啟不重新套用 GUI，任何啟動阻擋只寫入主控台與狀態，不會等待人工按鈕
- 詳細設定新增 Build 42 帳號、地圖玩家可見度、重生、派系、安全屋、PVP 傷害、速限與登入負載選項
- 「玩家與生存」頁可讀寫 Build 42 飢餓／口渴／疲勞、耐力恢復、營養、受傷、骨折、衣物劣化、多重攻擊、背後受擊、血液量及車禍傷害；既有檔案值優先，缺欄位才使用官方預設
- 世界、玩家、殭屍、Build 42 反作弊及 MOD／其他 Sandbox 的重點設定會直接分流至對應設定頁；設定檔實際存在的其餘鍵仍完整保留於「設定檔管理」總表，不以管理器猜測值取代
- 重點設定表直接顯示目前值、可靠預設、允許範圍／編號選項及原始 Build 42 註解；只寫入使用者實際變更的列，儲存後逐列由磁碟驗證
- 設定區分成「基礎設定」與「進階設定」兩個大分類，各自最多六個子分類；左側分類導覽不再占用設定區高度
- 世界詳細、玩家詳細、殭屍詳細、反作弊與模組設定各自使用獨立頁面；模組管理再拆成 Workshop／依賴、伺服器模組、地圖／重生點
- 詳細頁優先顯示繁中名稱、繁中選項與繁中說明，並保留原始設定鍵供核對；第三方模組無法可靠辨識的欄位不會由管理器猜測翻譯
- 修正 Sandbox 鍵原值未變時被誤判成不存在、反覆插入重複鍵的問題；儲存受管理鍵時會保留第一筆並清理同區段的重複鍵，避免遊戲與 GUI 讀到不同值
- 進階伺服器新增真正的 `SafehouseAllowRespawn` 安全屋重生開關；與死亡位置重生及分割畫面／Remote Play 重生分開管理
- 原創應用程式 Logo 與多尺寸 Windows ICO，套用於 EXE、視窗及標題區
- 內建 Noto Sans TC、Noto Serif TC、霞鶩文楷 TC，並保留系統微軟正黑體；可即時切換且記住選擇
- 儀表板可切換繁體中文／English；`Languages` 資料夾採 UTF-8 JSON 語言包，可自行新增翻譯
- 「關於此應用」介面顯示目前版本、創建者 MapleLeaf、支援平台與免責聲明
- 「關於此應用」會安靜檢查 GitHub Release；只有該頁的白／紅狀態點提示更新，可由使用者明確按下後下載 ZIP，絕不自動覆蓋或執行
- 公開名稱、說明、加入密碼及歡迎訊息均不由管理器預填；現有 INI 有值時只按原內容載入

原生 `SpawnItems` 會套用於每次建立新角色，包含死亡後重建；Build 42 原生設定無法將首次創角與復活物品分開。

Build 42 範例出生物品為 `Base.BaseballBat,Base.WaterBottle,Base.Chocolate`。`Base.WaterBottleFull` 是已移除的 Build 41 ID，管理器會拒絕寫入但不會擅自替換使用者內容。

## MOD 管理與「白名單」判定

1. 在「伺服器設定 → 基礎設定 → 模組 → Workshop／依賴」貼上新的 Workshop 數字 ID，按「加入並解析」。成功後輸入欄會自動清空；這不會清除既有伺服器模組。
2. 需要重查既有項目時按「重新檢查全部」，只重新讀取已下載內容則按「重新掃描已加入」。Steam Required Items 收在預設摺疊的候選區，永遠由使用者決定是否加入。
3. 到「伺服器模組」直接勾選要啟用的 Mod ID；取消勾選就是停用。選取列後，依賴、版本與作者排序規則會顯示在右側詳情，不必橫向尋找大量欄位。
4. 不再使用某個來源時，選取其列並按「移出選取來源」。Workshop 來源會一併移出其專屬 Mod ID、地圖與重生點；本機 Mod 可獨立移出。相依項目會先列出並停用，但管理器不會刪除本機下載檔案。
5. 「自動排序 MOD／地圖」會依 `mod.info` 的硬依賴與作者排序規則排列；缺少的 `loadModBefore/loadModAfter` 目標只提示，不會被誤當成必裝依賴。
6. 在地圖清單勾選要加入 `Map=` 的地圖；若偵測到 `spawnpoints.lua`，可再選擇是否加入玩家重生選單。
7. 按「套用勾選與順序」及「套用地圖與重生點」都只更新待儲存值。最後按頁面下方「儲存全部設定」，才會寫入 `WorkshopItems`、`Mods`、`Map` 與管理器控制的重生區域區塊。

Project Zomboid Dedicated Server 沒有一個可由管理器安全寫入、又能完整封鎖所有純客戶端修改的獨立「MOD 白名單」欄位。因此表格的「純客戶端候選」只代表本機掃描未發現 server/shared Lua、地圖或遊戲資料；它是審核提示，不會偽造設定。檔案無法判定或含素材時會保守標成「需人工確認」，Lua 校驗維持啟用。

管理器不修改第三方 MOD 內容。MOD 自己加入的 Sandbox 欄位會由 PZ 產生並保留；需要調整尚未圖形化的 MOD 專用選項時，可在「原始設定檔」檢查 `_SandboxVars.lua`，仍須明確按下儲存才會寫入。

## 介面語言與字型

- 儀表板的語言下拉選單可即時切換繁體中文與 English。
- `Languages` 內每個 JSON 都會自動成為選項；複製 `en-US.json`、修改 `code`、`displayName` 及翻譯值即可新增語言。
- 左側繁中原文與 `{0}`、`{1}`、`<LINE>` 等佔位符不可改名；缺少翻譯時安全回退繁中。
- 伺服器原始輸出、設定值、玩家與模組名稱不會翻譯，避免改變實際資料。
- 基礎設定可選 Noto Sans TC、Noto Serif TC、霞鶩文楷 TC 或系統微軟正黑體 UI；前三者已嵌入 EXE，不需要安裝到 Windows。

## 設定與資料位置

- 預設 Manager 設定：EXE 同一資料夾的 `manager-settings.json`
- 可選 Manager 設定：`%LOCALAPPDATA%\PZServerManager\manager-settings.json`
- PZ 設定：所選資料目錄下的 `Server`
- 世界存檔：所選資料目錄下的 `Saves\Multiplayer`
- ZIP 備份：所選資料目錄下的 `Backups`
- GUI 管理器記錄：優先使用 EXE 旁的 `Logs`；若該目錄不可寫則使用 `%LOCALAPPDATA%\PZServerManager\Logs`
- Workshop 中繼資料快取：`%LOCALAPPDATA%\PZServerManager\workshop-metadata-cache.json`

管理器只會在使用者明確按下設定儲存按鈕後修改 PZ 設定檔。若設定檔已存在，必須先讀取目前內容；讀取後若其他程式又修改檔案，管理器會要求重新讀取。

## SteamCMD 來源與免責

自動下載使用 Valve Steam CDN 官方 HTTPS 網址：

`https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip`

管理器會確認下載內容是 ZIP 且包含 `steamcmd.exe`。若 DNS、Windows 憑證信任鏈、代理伺服器、網路設備或上游 CDN 遭劫持或竄改，第三方攻擊造成的異常或損失不由本管理器作者負責。請保持 Windows 更新並使用可信任的網路環境。

## 網路與權限

- Windows 帳號必須能寫入 SteamCMD、PZ 安裝及資料目錄。
- 防火牆與路由器必須允許設定的 UDP 連接埠，預設為 `16261` 與 `16262`。
- 管理器不會自行強制終止 PZ。安全關服逾時時會暫停自動化並顯示人工確認按鈕；強制終止可能遺失最近一次成功存檔後的進度。

## 輸入驗證

數字格式錯誤時會列出確切欄位、目前內容及允許範圍；小數可使用 `.` 或 `,`。GUI 標示每個受管理數值的 Build 42 預設與範圍；「設定檔管理」頁會顯示目前 INI 與 Sandbox 的全部鍵值，優先呈現檔案內的官方註解、預設與上下限，再使用內建 Build 42 參考值補足；遊戲未註明的值會明確標示，不會猜測。

## 疑難排解

- 看不到主要介面：先依前置導引完成 SteamCMD 與 PZ Server 安裝。
- `Roles.getDefaultForUser() is null`：確認管理員密碼不為空白，改用全新的設定檔名稱測試乾淨 B42 資料庫，再以空白 `WorkshopItems`／`Mods` 測試純原版。管理器不會自動刪除既有資料庫。
- 無法儲存設定：先按「從目前檔案讀取 GUI」。
- 文字亂碼：選擇正確編碼後重新讀取；切換編碼本身不會寫入 PZ 設定。
- 若檔案是有效 UTF-8，管理器會拒絕以 Big5 強制誤讀。只有確認亂碼曾被存回 UTF-8 時，才使用「修復：UTF-8 曾被 Big5 誤讀後存檔」；含 `?`／`�` 的遺失字元仍需由備份還原。
- CLI／關服逾時：LOG 持續輸出不代表遊戲主迴圈正常。管理器以 `players` 的實際結果判定；連續兩次未回覆時先保持程序並停止自動化。確認無法恢復後，才使用警報中的「強制終止卡死程序」。此操作只終止管理器啟動的 PZ 程序樹，不會關閉 Windows VM。
- Build 42 若在玩家斷線或快速重連後反覆卡住，可先將 `PauseEmpty`（無玩家時暫停世界）設為 `false` 觀察；管理器只顯示風險提示，不會擅自更改官方預設或既有值。
- 設定損壞：使用「檢查／還原設定備份」還原 `.manager-backup`。

## MOD 管理資料與操作規則

- 「新增 Workshop ID」只保存本次輸入；成功下載的項目才加入伺服器清單，失敗項目留在輸入欄重試。輸入欄永遠不再充當 `WorkshopItems=` 的資料來源。
- 管理器分別讀取 `WorkshopItems=`、`Mods=` 與本機 Build 42 `mod.info`。既有 Mod ID 會從 PZ Server／SteamCMD 下載目錄反查 Workshop；真正的手動 Mod 顯示「本機模組／無 Workshop ID」。
- 尚未下載、下載不完整或缺少 B42 `mod.info` 的既有 Workshop 會留在清單並顯示原因，不會消失或清空其他設定。
- Steam Required Items 只列為候選，由使用者決定是否加入；`mod.info require=` 才作為硬依賴。新加入的 Mod ID 不會自動啟用。
- 勾選／取消勾選只控制待寫入 `Mods=` 的 Mod ID。停用依賴來源時會連帶停用下游 Mod 及其地圖／重生點，但不移除 Workshop 或刪除下載檔案。
- 不同 Workshop 提供相同 Mod ID 時會分別顯示來源，但只允許一個啟用來源；移出主要來源後，仍存在的替代來源會重新接手。
- 「移出選取來源」會先列出受影響的相依 Mod。Workshop 來源會一併移出其專屬 Mod、地圖與重生點；本機 Mod 可獨立移出，兩者都不刪除實體檔案。
- 自動排序處理 `require`、`loadBefore`、`loadAfter`、`loadModBefore` 與 `loadModAfter`；循環規則會保留原相對順序並標示需確認。
- Workshop ID、Mod ID、來源、版本、依賴與作者排序規則顯示在右側詳情；切換語言不會清除動態資料。
- 所有變更先保留為 GUI 待套用狀態。只有明確儲存後才以原編碼及備份機制同次寫入 `WorkshopItems=`、`Mods=`、`Map=` 與重生點，並從磁碟逐欄讀回驗證。

## 授權

本專案原創程式碼與應用程式依 [PolyForm Noncommercial License 1.0.0](LICENSE)
提供，只允許非商業使用、修改與散布。商業使用、商業託管、付費整合、轉售或
營利散布需要 MapleLeaf 的另外授權。

內嵌的 Noto CJK 與 LXGW WenKai TC 字型維持各自的 SIL Open Font License
1.1，不受本專案非商用限制重新授權；相關原文位於 `Assets/Fonts`，發布包則
放在 `Licenses` 資料夾。
