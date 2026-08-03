# PZ Build 42 伺服器管理器

目前版本：**v2.0.0**

專為 Project Zomboid Build 42 Stable Dedicated Server 設計的 Windows 圖形化管理工具，提供安裝導引、設定檔管理、MOD 管理、定時重啟、備份、存檔關服與診斷功能。

## 開發與定位

- 專案創建與維護：**MapleLeaf**
- 開發輔助工具：**[OpenAI Codex](https://github.com/codex)**
- 本專案由 MapleLeaf 獨立開發，並非 OpenAI、The Indie Stone、Project Zomboid、Valve 或 Steam 的官方產品，也未獲上述單位贊助、認證或背書。

Codex 的 Git 共同作者署名用於揭露 AI 協作，不代表 OpenAI 擁有、維護或擔保本專案。

## 第一次使用

1. 下載 Release ZIP 並完整解壓縮。
2. 執行 `PZServerManager.exe`，不要直接從 ZIP 內啟動。
3. 依前置導引指定或安裝 SteamCMD。
4. 透過管理器安裝 Project Zomboid Dedicated Server（App ID `380870`）。
5. 建立新的 Build 42 設定檔，或掃描並載入既有設定檔。

程式採 Windows x64 自含式發布，不需要另外安裝 .NET、Visual Studio 或開發工具。請保留 EXE 旁的 `Languages` 資料夾。

## 支援環境

- Windows Server 2022 Desktop Experience x64
- 具備桌面圖形元件的 64 位元 Windows 10／11
- Project Zomboid Build 42 Stable，Sandbox `VERSION = 6`

Windows Server Core 缺少 WPF 圖形元件，因此不支援。

## 核心功能

- 從 Valve 官方來源準備 SteamCMD，並安裝或更新 PZ Dedicated Server。
- 啟動前先讀取現有 INI 與 `SandboxVars.lua`；未讀取或檔案遭外部修改時拒絕覆寫。
- 保留 GUI 未管理的設定、原始檔案編碼與換行格式，儲存前建立備份，儲存後重新讀檔驗證。
- 提供基礎與進階圖形化設定，包括玩家、生存、世界、物資、殭屍、反作弊、MOD 與伺服器權限。
- 管理多組伺服器設定檔，可建立乾淨 B42 預設、複製並重新命名，而不複製世界或玩家資料。
- 管理 Workshop、Mod ID、依賴、載入順序、地圖與重生點；Required Items 永遠由使用者決定是否加入。
- 提供即時控制台、獨立存檔、安全關服、定時重啟、重啟前 ZIP 備份與自訂公告。
- 顯示在線玩家，檢查 Workshop 更新；有人時先公告，無人時才安全重啟更新。
- 偵測 CLI 無回應並暫停自動化，不會未經確認直接終止 PZ 或 Windows VM。
- 提供環境預檢、持久化 GUI 記錄及隱私清理診斷包。
- 支援繁體中文、英文、自訂 UTF-8 語言包與多種內建中文字型。
- 「關於此應用」安靜檢查 GitHub Release，只在該頁提示，不會自動覆蓋或執行更新。

## 重要安全原則

- 管理器只有在使用者明確按下儲存按鈕後，才會修改 PZ 設定檔。
- 既有設定必須先讀取；讀取後若被其他程式修改，必須重新讀取才能儲存。
- MOD、物品 ID、密碼、公開名稱與歡迎訊息不會由管理器擅自填入或替換。
- 強制終止只適用於管理器本次啟動且已確認卡死的 PZ 程序樹，不會操作 HOST、Proxmox NODE 或其他 VM。
- 更新伺服器、修改 MOD 或重建世界前，仍應建立可還原的 VM 快照與離線備份。

## 文件入口

- [完整使用手冊](USER_GUIDE.md)：設定、MOD、編碼、自動化、資料位置與疑難排解。
- [免責聲明](DISCLAIMER.md)：非官方定位、第三方權利與使用風險。
- [授權條款](LICENSE)：PolyForm Noncommercial License 1.0.0。

## SteamCMD 來源

自動下載使用 Valve Steam CDN 官方 HTTPS 網址：

`https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip`

管理器會檢查下載內容是否為 ZIP 且包含 `steamcmd.exe`。DNS、憑證、代理伺服器、網路設備或上游 CDN 遭劫持或竄改所造成的損害，不在本專案可控制範圍內。

## 授權

本專案原創程式碼與應用程式依 [PolyForm Noncommercial License 1.0.0](LICENSE) 提供，限非商業使用、修改與散布。商業使用、商業託管、付費整合、轉售或營利散布需要 MapleLeaf 另行授權。

內嵌字型維持各自的 SIL Open Font License 1.1，不受本專案非商用條款重新授權。
