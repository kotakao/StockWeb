# StockWeb 架構導讀（用 MVC 的語言讀懂 Blazor 專案）

> 給熟悉 ASP.NET MVC 的讀者。讀完這份文件，你應該能在 30 秒內定位任何功能的程式碼。

---

## 1. 一句話總覽

這個專案 = **MVC 的 M 和 C 照舊，V 從 .cshtml 換成 .razor**。

```
瀏覽器
  │
  ├─（頁面互動）Blazor Server：Components/Pages/*.razor ←── SignalR 長連線，事件回到伺服器執行 C#
  │                       │
  └─（HTTP API）Api/*Endpoints.cs（= Controller）
                          │
                    Services/*（純商業邏輯，可單元測試）
                          │
                    Data/*Repository.cs（Dapper SQL）
                          │
                    data/market.db（SQLite，唯讀為主；schema 歸 StockDCbot 管）
```

兩條入口最後匯流到同一套 Repository：頁面直接注入 Repository/Service 使用；`/api/*` 端點給外部或 JS 呼叫。

---

## 2. 資料夾 ↔ MVC 對照表

| 資料夾 | MVC 對應 | 內容 | 命名規則 |
|---|---|---|---|
| `Api/` | **Controller** | Minimal API 端點，一個功能族群一檔（`StockEndpoints.cs` ≈ `StockController`）；`RequestValidation.cs` 是共用的參數驗證（≈ ActionFilter） | `Map{名詞}Endpoints` 擴充方法，在 `Program.cs` 註冊 |
| `Services/` | **Controller 抽出的商業邏輯** | 純 C# 類別，不碰 HTTP 也不碰資料庫連線（`AdjustedPriceService` 前復權、`QuoteAggregator` 週/月/年 K 聚合、`ScreenerQueryBuilder` 篩選 SQL 組譯、`MarketBreadthCalculator`、`FinancialsCalculator`） | 每個都有對應的 `tests/*Tests.cs` |
| `Data/` | **Model（資料存取）** | Dapper Repository，一個介面一個實作（`IStockRepository` / `StockRepository`）；`SqliteConnectionFactory` 管唯讀/讀寫連線 | SQL 都在這層，其他層看不到 SQL |
| `Models/` | **Model（DTO/ViewModel）** | record 型別，API 回應與頁面顯示共用 | 無邏輯，純資料形狀 |
| `Components/Pages/` | **View ＋ 該頁的 Controller 程式** | 每個 .razor = 一頁；上半是 HTML 模板，`@code` 區塊是這頁的後端邏輯 | 檔名 = 路由（`Stock.razor` → `/stock/{code}`） |
| `Components/Charts/` | 共用 View 元件 | `LightweightChart.razor` 是所有圖表的唯一入口（JS Interop 包裝） | 不要繞過它直接呼叫 JS |
| `wwwroot/js/` | 前端靜態資源 | `charts.js`（interop 膠水）＋ lightweight-charts 函式庫本體 | 專案裡僅有的 JS，只服務圖表 |
| `Program.cs` | `Startup.cs` | DI 註冊 ＋ `Map*Endpoints()` ＋ Blazor 管線 | 找「誰被注入了什麼」看這裡 |

---

## 3. .razor 檔怎麼讀（對照 MVC 的心智模型）

拿 `Components/Pages/Screener.razor` 當例子，由上而下三段：

```razor
@page "/screener"                 ← ①路由（= Controller 的 [Route]）
@inject IScreenerRepository Repo  ← ②DI 注入（= Controller 建構子注入）

<button @onclick="RunAsync">查詢</button>   ← ③View：HTML + 事件綁定
@if (_rows is not null) { ...表格... }      ←   用 C# 變數直接渲染（= Razor View 的 @Model）

@code {                            ← ④這一頁的「Controller Action」們
    private List<ScreenerRow>? _rows;
    private async Task RunAsync()  ←   點按鈕→SignalR 送到伺服器→執行這個方法→
        => _rows = await Repo...   ←   改欄位→框架自動重繪上面的 HTML
}
```

與 MVC 的核心差異只有一個：**沒有「整頁重新載入」**。MVC 是「表單 POST → Action → 回傳新 View」；Blazor Server 是「事件經 SignalR 傳回伺服器 → 執行 `@code` 裡的方法 → 只更新變動的 DOM」。所以你在 `@code` 裡看到的欄位（`_rows`）就是這頁的 ViewModel，改它＝更新畫面。

---

## 4. 三條追蹤路線（實際檔案，照著讀一遍就熟了）

### 路線 A：篩選器（最短，先讀這條）
1. `Components/Pages/Screener.razor` — 條件表單與結果表格
2. `Services/ScreenerQueryBuilder.cs` — 條件 → 參數化 SQL（純函數，看測試 `ScreenerQueryBuilderTests` 最快）
3. `Data/ScreenerRepository.cs` — 執行 SQL
4. （外部入口）`Api/ScreenerEndpoints.cs` — `POST /api/screener` 的 Controller 版同一流程

### 路線 B：個股頁 K 線（最核心，含 JS 邊界）
1. `Components/Pages/Stock.razor` — 週期/還原切換按鈕在這
2. `Data/StockRepository.cs` — 撈日 K 原始列
3. `Services/AdjustedPriceService.cs` — 前復權（公式與 StockDCbot `analysis.adjust_history` 對齊）
4. `Services/QuoteAggregator.cs` — 日 K → 週/月/年 K 聚合（先還原、再聚合）
5. `Components/Charts/LightweightChart.razor` ＋ `wwwroot/js/charts.js` — C#→JS 的唯一邊界：C# 把序列化好的資料丟給 JS 畫圖，JS 不含任何商業邏輯

### 路線 C：自選股（唯一的寫入路徑）
1. `Components/Pages/Watchlist.razor`
2. `Data/WatchlistRepository.cs` — 注意這裡用 `CreateReadWrite()` 連線（其他 Repository 都是 `CreateReadOnly()`），這就是 AGENT.md 鐵律「唯一可寫 watchlist」在程式碼裡的樣子
3. `Api/WatchlistEndpoints.cs` — GET/POST/DELETE

---

## 5. 「我要加一個功能」的固定動線

以加一個新查詢頁為例，永遠是同一套順序（由下而上）：

1. `Models/` 加 DTO record
2. `Data/` 加 `IXxxRepository` + 實作（SQL 在此）＋ `tests/XxxRepositoryTests`（用 `TestDatabase.cs` 的暫時 SQLite fixture）
3. 需要計算邏輯 → `Services/` 加純類別＋測試
4. `Api/` 加 `XxxEndpoints.cs`，在 `Program.cs` 註冊 DI 與 `MapXxxEndpoints()`
5. `Components/Pages/` 加 `Xxx.razor`（`@page` 路由 + `@inject` + `@code`）
6. `Components/Layout/MainLayout.razor` 加導覽連結

讀程式碼時反向走：**從 .razor 的 `@inject` 看它依賴誰，一路往下追到 SQL**。

---

## 6. 常見疑問

- **為什麼沒有 Controllers/ 資料夾？** Minimal API 把 Controller 寫成擴充方法（`Api/*Endpoints.cs`），一樣是「路由→驗證→呼叫 Service/Repository→回傳」，只是少了類別儀式。把 `Api/` 資料夾當成 Controllers/ 看即可。
- **頁面按鈕按下去，網路頁籤裡為什麼沒有 HTTP 請求？** 走 SignalR WebSocket（F12 的 WS 頁籤），這是 Blazor Server 的正常行為。
- **JS 到底有多少？** 只有圖表：`charts.js` 一檔膠水 + 函式庫本體。其餘互動全是 C#。
- **哪些程式碼有測試？** `Services/` 與 `Data/` 全覆蓋（xUnit，`dotnet test`）；.razor 頁面層不做單元測試（薄薄一層綁定，行為靠端點與服務層測試保障）。
- **資料庫 schema 在哪？** 不在本專案。`market.db` 的建表在 StockDCbot 的 `storage.py`（鐵律：本專案禁 DDL）；測試 fixture 的迷你 schema 在 `tests/TestDatabase.cs`（僅供測試）。

---

## 7. 頁面路由總表

| 路由 | 檔案 | 功能 |
|---|---|---|
| `/` | `Pages/Dashboard.razor` | 市場儀表板 |
| `/stock/{code}` | `Pages/Stock.razor` | 公司總覽（K 線/籌碼/營收/損益/估值） |
| `/screener` | `Pages/Screener.razor` | 條件選股 |
| `/watchlist` | `Pages/Watchlist.razor` | 自選股 |
| `/calendar` | `Pages/Calendar.razor` | 除權息行事曆 |
| `/coverage` | `Pages/Coverage.razor` | 資料覆蓋範圍 |
