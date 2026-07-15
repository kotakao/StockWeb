# StockWeb — 台股盤後研究網頁

單人使用的**盤後**研究工具（.NET 8 Blazor Server）。資料由姊妹專案 **StockDCBot**（Python Discord Bot）每交易日 17:00 後寫入共用 SQLite（`market.db`），本專案負責查詢與視覺化。不做即時報價、不做下單、不做推播。

- 行為準則與兩專案資料契約：[AGENT.md](AGENT.md)
- 架構導讀（用 MVC 的語言讀懂 Blazor 專案）：[docs/architecture.md](docs/architecture.md)
- 開發派工與版本紀錄：[todolist.md](todolist.md)
- 完整規格：StockDCBot repo 的 `docs/stockweb-spec.md`

## 頁面功能

| 路由 | 頁面 | 功能 |
|---|---|---|
| `/` | 市場儀表板 | 指數與成交金額、漲跌家數／A/D Line、法人近 20 日累積、融資餘額趨勢 |
| `/stock/{code}` | 公司總覽 | 多週期 K 線（日/週/月/年，還原/未還原切換）＋法人與融資券疊加；頁首指標卡（本益比/淨值比/殖利率/營收 YoY/毛利率/營益率/EPS）；月營收與季度損益趨勢；估值歷史；近期新聞（Google News RSS 中/英頁籤）與法人說明會場次 |
| `/screener` | 條件選股 | 基本面/籌碼面/估值面複合條件橫斷面查詢，結果可加自選 |
| `/watchlist` | 觀察名單狀態板 | 自選清單（與 Bot `/watch` 共用）＋選填持股（股數/成本）：現值、未實現損益、當日損益、聚合列；快速掃視訊號（近 5 日法人方向、融資 5 日方向、接近 52 週還原高/低、未來 14 日除權息或法說會） |
| `/calendar` | 行事曆 | 月曆檢視除權息與法說會事件（不同顏色），自選股高亮 |
| `/coverage` | 資料覆蓋範圍 | 各表資料起訖日、缺口日期、最後更新時間 |

## 架構

方案為四個專案，依賴方向由編譯期單向強制 **Web → Data → Services → Models**：

```
瀏覽器
  │
  ├─（頁面互動）Blazor Server：StockWeb.Web/Components/Pages/*.razor ←─ SignalR 長連線
  │                       │
  └─（HTTP API）StockWeb.Web/Api/*Endpoints.cs（Minimal API）
                          │
              StockWeb.Data/*Repository.cs（Dapper SQL；讀取端計算呼叫 Services）
                    │                    │
                    │           StockWeb.Services/*（純商業邏輯，可單元測試）
                    │                    │
                    │           StockWeb.Models/*（DTO record，無任何參考）
                    │
              data/market.db（SQLite WAL，唯讀為主；schema 歸 StockDCBot 管）
```

- **讀取端計算**：Repository 直接呼叫 Services 純函數——前復權（`AdjustedPriceService`，公式與 StockDCBot `analysis.adjust_history` 對齊）、週/月/年 K 聚合（`QuoteAggregator`，先還原再聚合）、篩選 SQL 組譯（`ScreenerQueryBuilder`）、損益與訊號計算（`HoldingsCalculator`／`WatchlistSignalCalculator`）等。
- **圖表**：TradingView Lightweight Charts，統一經 `Components/Charts/LightweightChart.razor`（JS Interop 包裝）＋ `wwwroot/js/charts.js`；這是專案裡唯一的 JS 邊界。
- **新聞**：`Api/NewsService` 以 5 秒逾時 HttpClient 抓 Google News RSS，IMemoryCache 快取 30 分鐘；僅顯示標題/來源/時間/外部連結（著作權紅線），不寫入任何資料庫。

### 與 StockDCBot 的資料契約（鐵律摘要，詳見 AGENT.md）

1. `market.db` 的 schema 由 StockDCBot（`storage.py`）獨占管理，本專案嚴禁 DDL。
2. market 資料表一律唯讀；可寫白名單目前僅 `watchlist` 與 `holdings`（網頁端 `user_id=0`）。
3. 資料庫絕對路徑放 `appsettings.json` 的 `DbPath`；唯讀查詢用 `Mode=ReadOnly` 連線，寫入用獨立讀寫連線（`busy_timeout=5000`）。
4. 兩專案依賴 SQLite WAL 模式（由 Bot 端開啟）。

## API 端點一覽

全部回 JSON；參數驗證共用 `Api/RequestValidation.cs`（code 為 4-6 位英數、`days` 上限 252、`months` 上限 60、`quarters` 上限 40、日期須合法），錯誤一律回 400＋訊息物件。

| 端點 | 說明 |
|---|---|
| `GET /api/market/daily?days=` | 大盤日線序列（market_daily） |
| `GET /api/market/breadth?days=` | A/D Line、量能溫度等寬度序列 |
| `GET /api/stocks/{code}/quotes?days=&adjusted=&period=` | 個股 OHLCV；`adjusted` 前復權、`period=daily\|weekly\|monthly\|yearly` |
| `GET /api/stocks/{code}/institutional?days=` | 逐日三大法人 |
| `GET /api/stocks/{code}/margin?days=` | 逐日融資券 |
| `GET /api/stocks/{code}/valuation?days=` | 估值歷史（本益比/殖利率/淨值比） |
| `GET /api/stocks/{code}/revenue?months=` | 月營收與 YoY |
| `GET /api/stocks/{code}/financials?quarters=` | 季度損益（營收/毛利率/營益率/EPS） |
| `GET /api/stocks/{code}/dividends` | 除權息事件 |
| `GET /api/stocks/{code}/news?lang=zh\|en` | 近期新聞（RSS，記憶體快取） |
| `GET /api/stocks/{code}/conferences` | 該公司法人說明會場次 |
| `POST /api/screener` | 複合條件篩選（body 為條件物件） |
| `GET/POST /api/watchlist`、`DELETE /api/watchlist/{code}` | 自選股清單 |
| `GET /api/watchlist/status` | 觀察名單狀態板彙總（單一端點，避免 N+1） |
| `GET/PUT /api/holdings`、`DELETE /api/holdings/{code}` | 持股（僅寫 holdings 表） |
| `GET /api/calendar/dividends?month=` | 該月除權息行事曆 |
| `GET /api/calendar/conferences?month=` | 該月法說會行事曆 |
| `GET /api/coverage` | 各表起訖日/筆數/缺口 |

## 建置、測試與執行

```powershell
dotnet build                          # 應無警告無錯誤
dotnet test                           # 全數通過方可 commit（現行基準 208 個）
dotnet run --project src/StockWeb.Web
```

- 執行前確認 `src/StockWeb.Web/appsettings.json` 的 `DbPath` 指向本機 StockDCBot 的 `data/market.db`。
- `global.json` 釘選 .NET SDK 8.0.x（`rollForward: latestFeature`）。
- 測試：xUnit（`tests/StockWeb.Tests`）。`Services/` 與 `Data/` 全覆蓋；Data 層測試使用「暫時 SQLite 檔＋最小 schema」fixture（`TestDatabase.cs`，建表僅限測試檔，正式程式禁 DDL）。

## 專案結構

```
StockWeb/
├─ src/
│  ├─ StockWeb.Models/      # DTO record；無任何專案參考
│  ├─ StockWeb.Services/    # 純商業邏輯（前復權/聚合/組譯/損益訊號）；僅參考 Models
│  ├─ StockWeb.Data/        # Dapper Repository 與連線工廠；參考 Services + Models
│  └─ StockWeb.Web/         # Blazor Server 主專案：Api/、Components/、wwwroot/
└─ tests/
   └─ StockWeb.Tests/       # xUnit；參考上述四專案
```
