# StockWeb 開發待辦（派工 Prompt 集）

> 使用方式：每個區塊的 Prompt 為自包含內容，直接複製整段送給新的 Session 即可開工。
> 完成一區後在標題打勾並記錄 commit hash。標註「**派工於 StockDCbot**」的區塊，
> 其工作目錄是 `D:\Programs\Python\StockDCbot`（資料源與 schema 歸 Python 端管，見 AGENT.md 鐵律）。
> 本檔取代 stockweb-spec.md §9 成為現行派工來源；spec 本文仍為架構規格依據。

## 版本規劃總覽

| 版本 | 內容 | 前置 |
|---|---|---|
| 1.0 ✅ | W0-W4 + W2（骨架/儀表板/個股頁/篩選器/自選+行事曆）＋ WAL | 完成 |
| 1.1 | ~~W5 公司總覽頁~~ ✅、W-R1 方案分層重構、W6 公司資訊與法說會（前置 DC-K）、W7 持股狀態（前置 DC-L） | 順序：W-R1 → DC-K/L → W6/W7 |
| 2.0+ | W8-W14 前瞻區（見文末，屆時再細化） | 各區標註 |

---

# 1.1 版本

## ☑ DC-J：季度損益資料源（**派工於 StockDCbot**，W5 的前置）— StockDCBot commit `eff1801`

```text
你的工作目錄是 D:\Programs\Python\StockDCbot（Windows、git repo、main 分支）。
請先閱讀 AGENT.md 並遵守其行為準則。測試以 stdlib unittest 執行：
.venv\Scripts\python.exe -m unittest discover -s tests；嚴禁提交任何 .env 檔。

背景：StockWeb 網頁的公司總覽頁需要毛利率、營業利益率、EPS，資料庫目前
只有月營收（monthly_revenue），沒有季度損益資料。

需求：
1. 驗證資料源：TWSE OpenAPI 的上市公司綜合損益表端點（openapi.twse.com.tw
   的 t187ap06_L 系列，注意一般業/金融業等可能分表）與 TPEX 對應 OpenAPI。
   先以實際 HTTP 請求驗證端點與回應結構後才改程式；此類端點多為「最新一季」
   快照——歷史回填能力請如實評估並記載於 README，勿假造；找不到可用端點時
   停止並回報，嚴禁寫入未驗證的 URL。
2. 比照 fundamentals.py 月營收的慣例（獨立擷取＋正規化、UA/retry/節流/快取）
   擴充或新增模組處理季度損益。
3. storage 新增 quarterly_financials 表（主鍵 market+code+year_quarter，
   欄位：營業收入、營業毛利、營業利益、稅後淨利、EPS，仟元/元，缺漏容錯
   NULL，UPSERT 冪等——毛利率/營益率由讀取端計算，不存冗餘欄位）；
   新增查詢 get_quarterly_financials(code, quarters=20)。
4. 排程整合：比照月營收的收尾流程慣例（獨立容錯、已入庫跳過；季報公布期
   約每年 3/5/8/11 月中，判斷邏輯從寬：當季未入庫且端點已有新資料即抓）。
5. MCP server 新增 get_quarterly_financials(code, quarters=20) 工具
   （quarters 上限 40）。

驗收：解析與寫入查詢的單元測試（錄製 fixture）；排程判斷測試；既有 354+
測試全綠。完成後以 feat 前綴 commit（訊息含「DC-J」，結尾加
Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>），不要 push。
回報：端點驗證結果（含歷史回填能力如實評估）、改動清單、測試數、hash。
```

## ☑ W5：公司總覽頁（單頁看齊估值＋營收＋損益＋多週期 K 線）（commit `4764a55`，於另一台機器驗收通過）

```text
你的工作目錄是 D:\Programs\CSharp\StockWeb（Windows、git repo、main 分支）。
請先閱讀 AGENT.md（鐵律與工程慣例）與
D:\Programs\Python\StockDCbot\docs\stockweb-spec.md。
前置：DC-J 已完成（quarterly_financials 表存在且有資料）；不滿足請停止並回報。

需求：將 /stock/{code} 個股頁擴充為公司總覽頁：
1. 頁首指標卡列（一眼看完）：公司名稱與代號、本益比、股價淨值比、殖利率
   （valuation 最新日）、月營收 YoY、累計營收 YoY（monthly_revenue 最新月）、
   毛利率、營業利益率、EPS（quarterly_financials 最新季，比率由營收/毛利/
   營益計算）。每張卡顯示資料日期；個別資料缺漏時該卡顯示「—」與原因
   tooltip，不影響其他卡。
2. K 線週期切換：日/週/月/年。週/月/年 K 由日 K 在伺服器端聚合
   （Services 純類別：開=首日開、收=末日收、高=區間最高、低=區間最低、
   量=加總；週以 ISO 週、月以日曆月、年以日曆年分組；還原/未還原切換
   與聚合正確組合——先還原再聚合）。歷史不足時如實顯示可用範圍
   （目前資料庫約有一年日 K，年 K 會很短，屬預期）。
3. API：/api/stocks/{code}/quotes 增加 period=daily|weekly|monthly|yearly
   參數（預設 daily）；新增 /api/stocks/{code}/financials?quarters=20
   端點（quarters 上限 40，驗證沿用既有 helper）。
4. 新增季度損益區塊：營收/毛利率/營益率/EPS 的季度趨勢圖（既有
   LightweightChart 元件）。

驗收：聚合邏輯單元測試（跨週/跨月/跨年邊界、還原+聚合組合、不足一根的
殘週）；指標卡計算測試；dotnet test 全綠。完成後以 feat 前綴 commit
（訊息含「功能區 W5」，結尾加
Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>），不要 push。
```

## ☐ W-R1：方案分層重構（單專案 → 四專案）——排程於 W6 之前執行

```text
你的工作目錄是 D:\Programs\CSharp\StockWeb（Windows、git repo、main 分支、
.NET 8 Blazor Server）。請先閱讀 AGENT.md（鐵律與工程慣例）與
docs/architecture.md（現行結構導讀）。

背景：目前為單一專案 src/StockWeb，分層靠資料夾約定維持。使用者習慣
多專案分層方案，且希望依賴方向由編譯期強制。

需求：將方案重構為四個專案，依賴方向單向往下（Web → Services → Data → Models）：
1. src/StockWeb.Models（class library）：現 Models/ 的全部 DTO。無任何專案參考。
2. src/StockWeb.Data（class library）：現 Data/ 的全部 Repository 與
   SqliteConnectionFactory。僅參考 Models。Dapper 與 Microsoft.Data.Sqlite
   套件參考移到此專案。
3. src/StockWeb.Services（class library）：現 Services/ 的全部純邏輯類別。
   參考 Data 與 Models。
4. src/StockWeb.Web（Blazor Server 主專案，原 StockWeb 改名或重建擇一，
   保留 launchSettings 與 appsettings）：Components/、Api/（含
   RequestValidation，共用驗證 helper 留在 Web，不另建 Common 專案）、
   wwwroot/、Program.cs。參考 Services、Data、Models。
5. 命名空間隨專案名調整（StockWeb.Models 等）；檔案搬移盡量用 git mv
   保留歷史。
6. tests/StockWeb.Tests 維持單一測試專案，參考上述四專案，測試程式
   只允許改 using/命名空間，任何斷言與測試邏輯不得更動。
7. 這是純結構重構：嚴禁任何邏輯修改、重新命名類別、順手優化或
   「改善」程式碼（AGENT.md Surgical Changes 從嚴適用）。行為必須零變化。
8. 同步更新文件：docs/architecture.md 的資料夾對照表與路徑（§2、§4 追蹤
   路線的檔案路徑）、AGENT.md 技術棧一節的專案結構描述、
   D:\Programs\Python\StockDCbot\docs\stockweb-spec.md §5 的方案結構
   （spec 在另一個 repo，該檔更新後請於回報中註明，由管理流程在
   StockDCbot 端 commit，你不要在該 repo 執行 git 操作）。

驗收：dotnet build 無警告無錯誤；dotnet test 全數通過且測試數量不得少於
重構前（先記錄重構前數量並於回報中對照）；dotnet run --project
src/StockWeb.Web 可啟動且六個頁面路由不變。完成後以 refactor 前綴 commit
（訊息含「W-R1」，結尾加
Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>），不要 push。
回報：專案結構樹、重構前後測試數對照、文件更新清單、commit hash。
```

## ☐ DC-K：法說會資料源（**派工於 StockDCbot**，W6 的前置）

```text
你的工作目錄是 D:\Programs\Python\StockDCbot（Windows、git repo、main 分支）。
請先閱讀 AGENT.md 並遵守其行為準則。測試以 stdlib unittest 執行：
.venv\Scripts\python.exe -m unittest discover -s tests；嚴禁提交任何 .env 檔。

背景：StockWeb 需要顯示個股的近期法人說明會時間與內容連結。

需求：
1. 驗證資料源：TWSE OpenAPI 的上市公司法人說明會一覽表（t187ap38_L 系列）
   與 TPEX 對應端點。先以實際 HTTP 請求驗證後才改程式；找不到可用端點時
   停止並回報，嚴禁寫入未驗證的 URL。
2. storage 新增 investor_conferences 表（主鍵 market+code+conference_date，
   欄位：時間、地點/方式、簡報或影音連結、訊息摘要，缺漏容錯 NULL，
   UPSERT 冪等）；查詢 get_investor_conferences(code, limit=10) 與
   get_upcoming_conferences(days=30)（全市場近期場次）。
3. 排程整合：比照既有收尾流程慣例每日更新（獨立容錯、失敗僅記 log）。
4. MCP server 新增 get_investor_conferences(code) 工具。

驗收：解析/寫入/查詢單元測試（錄製 fixture）；既有測試全綠。完成後以
feat 前綴 commit（訊息含「DC-K」，結尾加
Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>），不要 push。
```

## ☐ W6：公司近期資訊（新聞）與法說會

```text
你的工作目錄是 D:\Programs\CSharp\StockWeb（Windows、git repo、main 分支）。
請先閱讀 AGENT.md（鐵律與工程慣例）與
D:\Programs\Python\StockDCbot\docs\stockweb-spec.md。
前置：DC-K 已完成（investor_conferences 表存在）；W5 已完成（總覽頁佈局）。

需求：在 /stock/{code} 公司總覽頁新增「近期資訊」區塊：
1. 新聞來源用 Google News RSS（不需 API key）：
   - 中文：https://news.google.com/rss/search?q={公司名稱 OR 代號}&hl=zh-TW&gl=TW&ceid=TW:zh-Hant
   - 英文：https://news.google.com/rss/search?q={公司英文名或代號}&hl=en-US&gl=US&ceid=US:en
   先以實際請求驗證 RSS 結構後才實作解析。中/英文以頁籤切換。
2. 伺服器端 NewsService 抓取＋解析（System.ServiceModel.Syndication 或
   XDocument 皆可），以 IMemoryCache 快取 30 分鐘（同代號重複瀏覽不重打）；
   逾時 5 秒、失敗時區塊顯示原因不影響頁面其他部分。
3. 呈現規範（著作權紅線）：只顯示標題、來源媒體、發布時間與外部連結
   （新分頁開啟），嚴禁抓取或轉載內文全文；每語言最多 10 則。
4. 法說會區塊：讀 investor_conferences（唯讀），顯示該公司近期與未來場次
   （時間/方式/連結）；另在 /calendar 行事曆頁把未來 30 日全市場法說會
   與除權息事件並列（不同顏色標記）。
5. API：GET /api/stocks/{code}/news?lang=zh|en 與
   GET /api/stocks/{code}/conferences、GET /api/calendar/conferences?month=。
6. 新聞屬即時外部資料，不寫入任何資料庫（快取僅在記憶體）——鐵律不變。

驗收：RSS 解析單元測試（以錄製的 XML fixture，含空結果與畸形項目）；
快取行為測試；dotnet test 全綠。完成後以 feat 前綴 commit（訊息含
「功能區 W6」，結尾加
Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>），不要 push。
```

## ☐ DC-L：持股資料表（**派工於 StockDCbot**，W7 的前置）

```text
你的工作目錄是 D:\Programs\Python\StockDCbot（Windows、git repo、main 分支）。
請先閱讀 AGENT.md 並遵守其行為準則。測試以 stdlib unittest 執行：
.venv\Scripts\python.exe -m unittest discover -s tests；嚴禁提交任何 .env 檔。

背景：StockWeb 要讓使用者記錄持股（股數/成本）以檢視觀察名單的持股狀態。
依兩專案契約，schema 由本專案管理；此表建立後將列入 StockWeb 的可寫白名單。

需求：
1. storage 新增 holdings 表（主鍵 user_id+code，欄位：shares 股數、
   avg_cost 平均成本、updated_at，UPSERT 冪等）；查詢 get_holdings(user_id)、
   寫入 upsert_holding(user_id, code, shares, avg_cost)、刪除
   delete_holding(user_id, code)。純資料層，本專案的 Discord 指令暫不使用
   此表（未要求勿加）。
2. MCP server 新增唯讀 get_holdings(user_id=0) 工具。

驗收：holdings 寫入/查詢/刪除/冪等單元測試；既有測試全綠。完成後以
feat 前綴 commit（訊息含「DC-L」，結尾加
Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>），不要 push。
```

## ☐ W7：觀察名單持股狀態板

```text
你的工作目錄是 D:\Programs\CSharp\StockWeb（Windows、git repo、main 分支）。
請先閱讀 AGENT.md（鐵律與工程慣例）與
D:\Programs\Python\StockDCbot\docs\stockweb-spec.md。
前置：DC-L 已完成（holdings 表存在）。完成本區後，AGENT.md 鐵律第 2 條的
可寫白名單需補上 holdings（本區內一併修改該行文字）。

需求：擴充 /watchlist 頁為「觀察名單狀態板」，讓使用者一眼確認持股狀態：
1. 每檔觀察股可選填持股資訊（股數、平均成本，寫入 holdings 表，
   user_id 固定 0；未填視為純觀察）。
2. 清單每列顯示：最新收盤與漲跌%、（有持股者）現值、未實現損益與報酬率、
   當日損益；聚合列顯示組合總市值、總損益、當日變動。
3. 狀態欄位（快速掃視訊號，重用既有資料）：近 5 日法人淨買賣方向、
   融資餘額 5 日變化方向、是否接近 52 週高/低（±5% 內，用還原價）、
   未來 14 日內有無除權息或法說會。
4. API：GET/PUT/DELETE /api/holdings（寫入僅限 holdings 表；觸及其他表
   即為錯誤）；狀態板查詢彙總為單一端點 GET /api/watchlist/status
   （一次查詢組合，避免 N+1）。
5. 排序與高亮：預設按當日損益排序；損益紅漲綠跌（台股慣例）。

驗收：損益/報酬率計算單元測試（含零成本、無持股、除權息後成本情境的
邊界）；狀態欄各訊號測試；holdings 寫入白名單約束測試；dotnet test 全綠。
完成後以 feat 前綴 commit（訊息含「功能區 W7」，結尾加
Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>），不要 push。
```

---

# 2.0 以後（前瞻區——屆時派工前先依當時程式碼現況修訂 Prompt）

## ☐ W8：使用者資料分離（webapp.db）

```text
背景：筆記/標註/UI 偏好等純網頁資料不該進共用 market.db（schema 歸 Python 管，
且 Bot 不需要這些資料）。
需求：StockWeb 建立自有 SQLite（data/webapp.db，本專案擁有 schema，鐵律第 1 條
修訂為「market.db 的 schema 歸 Python 端；webapp.db 歸本專案」）；遷移入口：
個股筆記（每檔股票的自由文字＋時間戳）、K 線標註（日期+價位+文字）、
UI 偏好（預設週期、深色模式）。既有 watchlist/holdings 仍留在 market.db
（與 Bot 共用）。驗收：兩庫並存的連線工廠測試；dotnet test 全綠。
```

## ☐ W9：產業與同業比較（前置：**派工於 StockDCbot** 的產業分類資料源）

```text
前置 DC 派工：TWSE/TPEX 公開的產業分類資料（上市公司基本資料 t187ap03_L 含
產業別欄，需實際驗證）入庫 company_profile 表（market+code，產業別、名稱、
英文簡稱）。
Web 需求：個股頁新增「同業比較」區塊——同產業個股的估值（PE/PB/殖利率）、
月營收 YoY、季度毛利率對比表；產業頁 /industry/{id} 列出成分股與產業彙總
指標；Screener 增加產業別條件。驗收：比較查詢與排序測試全綠。
```

## ☐ W10：警示中心（網頁內通知歷史）

```text
背景：市場異常（market_alerts）與自選股警示目前只推 Discord，網頁看不到歷史。
需求：讀取 StockDCbot 每日 JSON 報告（reports/YYYY-MM/*.json 的
sections.market_alerts）彙整為 /alerts 頁：可依日期/類型篩選的警示歷史時間軸；
Dashboard 加最近警示卡。純唯讀，不新增資料表。若屆時需要網頁端主動通知
（瀏覽器通知/Email），另開派工評估。驗收：JSON 解析容錯測試（缺檔/舊格式）。
```

## ☐ W11：篩選條件歷史回測

```text
前置：market.db 已累積 ≥2 年日線與法人資料（屆時確認），除權息還原完備。
需求：Screener 條件組合的簡易回測——選定歷史日期執行同一組條件，追蹤命中
個股其後 N 日（5/20/60）還原價報酬分布（中位數/勝率/最大回撤），與大盤
同期對比；結果頁含分布圖與逐股明細。計算放 Services 純類別（可測試），
長運算用背景執行＋進度回報。明確不做：交易成本模擬、部位管理、參數優化
（防過擬合，保持「條件驗證」定位）。驗收：報酬計算與邊界（除牌股/資料
不足）測試全綠。
```

## ☐ W12：報表匯出

```text
需求：Screener 結果、觀察名單狀態板、個股總覽（含季度損益）可匯出 CSV 與
xlsx（ClosedXML 或 EPPlus 擇一，註明授權考量）；檔名含日期；數字格式與
頁面一致（千分位/百分比）。驗收：匯出內容與頁面資料一致性測試。
```

## ☐ W13：部署硬化

```text
需求：Windows 服務化（發佈 self-contained + Windows Service 或工作排程器，
擇一並記載於 README）、機器重開自動啟動、HTTPS（自簽或內網憑證）、
appsettings 的環境分層（Production 設定不含開發路徑）、market.db 每日備份
腳本（可與 StockDCbot 端協調，擇一邊實作避免重複）。驗收：重開機後服務
自動恢復、備份檔可還原驗證。
```

## ☐ W14：SQL Server 遷移（條件觸發，勿提前執行）

```text
觸發條件（見 stockweb-spec.md §2，遇其一才啟動）：跨機器部署／多人使用／
需要 SSMS 級管理。
屆時範圍：兩專案同步遷移——StockDCbot storage.py（pyodbc、INSERT OR REPLACE
改 MERGE）、StockWeb 連線工廠與 SQL 方言差異盤點、資料搬遷腳本（SQLite→
SQL Server，含驗證筆數一致）、雙寫過渡期方案評估。此為大工程，啟動前先
回到管理 session 重新細化派工。
```

---

## 已完成（記錄用）

- ✅ 功能區 W0 方案骨架與資料層 — commit `a4c6d91`
- ✅ 功能區 W1 市場儀表板 — commit `0dba153`（補強 `1d16d9a`）
- ✅ 功能區 W3 條件選股 — commit `298db62`
- ✅ 功能區 W4 自選股與除權息行事曆 — commit `f580864`
- ✅ 功能區 W2 個股頁（補派）— commit `b827d5e`
- ✅ 前置作業 WAL（StockDCbot 端）— StockDCbot commit `9d950b7`
- ✅ DC-J 季度損益資料源 — StockDCBot commit `eff1801`
- ✅ 功能區 W5 公司總覽頁 — commit `4764a55`（於另一台機器驗收）
- ✅ DC-J 季度損益資料源（StockDCbot 端，W5 前置）— StockDCBot commit `eff1801`
