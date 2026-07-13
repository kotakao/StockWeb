# AGENTS.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

## 5. Always use Traditional Chinese for all responses.

---

--- project-doc ---

# StockWeb — 台股盤後研究網頁

## 專案定位

單人使用的**盤後**研究工具。資料由姊妹專案 **StockDCBot**（`D:\Programs\Python\StockDCbot`，Python Discord Bot）每交易日 17:00 後寫入共用 SQLite；本專案負責查詢與視覺化。不做即時報價、不做下單、不做推播。完整規格見 `D:\Programs\Python\StockDCbot\docs\stockweb-spec.md`，開發派工見本 repo 的 `todolist.md`。

## 技術棧

- **.NET 8 Blazor Server**，方案分四專案，依賴方向單向往下
  `Web → Data → Services → Models`（編譯期強制）：
  - `src/StockWeb.Web`（Blazor Server 主專案，Components/ 與 Api/ 同專案；
    AssemblyName 與 RootNamespace 維持 `StockWeb`）
  - `src/StockWeb.Data`（Repository 與連線工廠；讀取端計算沿用 Services）
  - `src/StockWeb.Services`（純函數計算，僅參考 Models）
  - `src/StockWeb.Models`（DTO record，無任何參考）
- **Dapper + Microsoft.Data.Sqlite**（`StockWeb.Data` 連線工廠：唯讀 `Mode=ReadOnly` 與讀寫兩種，讀寫連線 `busy_timeout=5000`）
- **TradingView Lightweight Charts**（`Components/Charts/LightweightChart.razor` JS Interop 包裝 + `wwwroot/js/charts.js`）
- 測試：**xUnit**（`tests/StockWeb.Tests`），Data 層以「測試建立的暫時 SQLite 檔＋最小 schema」fixture

## 鐵律（與 StockDCBot 的資料庫共用契約，違反即為錯誤）

1. **Schema 由 Python 端（StockDCbot/storage.py）獨占管理**。本專案嚴禁對共用資料庫 CREATE/ALTER/DROP 任何資料表；需要新表或新欄位時，回到 StockDCBot 開派工。（測試 fixture 中建表僅限測試用暫時檔，不在此限。）
2. 對 market 資料表（daily_quotes/institutional/margin/valuation/market_daily/dividend_events/monthly_revenue 等）**一律唯讀**；可寫入的表僅限 todolist 派工中明確列入白名單者（目前：`watchlist`，網頁端 user_id 固定用 `0`，上限 20 檔與 Bot 一致）。
3. 資料庫絕對路徑在 `appsettings.json` 的 `DbPath`；嚴禁提交任何含機密的設定或 `.env` 類檔案。
4. 兩專案依賴 SQLite **WAL 模式**（由 Bot 端負責開啟）。

## 工程慣例

- API 參數驗證沿用 `Api/RequestValidation.cs` 共用 helper：code 為 4-6 位英數、days 上限 252、months 上限 60、日期須合法；錯誤一律回 400 + 訊息物件，不回 500。
- 還原價（前復權）計算在 `Services/AdjustedPriceService`，公式以 StockDCbot `analysis.adjust_history` 為準，**兩邊單元測試案例數字必須一致**；改公式時兩專案要同步。
- 頁面各卡片/區塊獨立容錯：單一資料來源不足時該區顯示原因，不影響其他區塊（比照 StockDCBot 報告小節慣例）。
- 純邏輯放 `Services/`（不依賴 ASP.NET 型別、可單元測試）；SQL 一律參數化，嚴禁字串拼接使用者輸入。
- 圖表一律重用 `LightweightChart` 包裝元件，不引入第二套圖表庫。

## 建置與測試

```powershell
dotnet build          # 應無警告無錯誤
dotnet test           # 全數通過方可 commit
dotnet run --project src/StockWeb.Web
```

- 完成每個功能區後：全測試綠 → 以 `feat:`/`fix:`/`chore:` 前綴 commit（訊息含功能區編號，結尾加 `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`），**不要 push**（push 由管理流程統一執行）。
- 實機驗證後請關閉殘留的 `dotnet run`/StockWeb 程序，避免鎖住 build 輸出。
