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

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

## 5.Always use Traditional Chinese for all responses.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.

--- project-doc ---

# 台灣股市每日自動分析報告產生器 — 系統 Prompt（Discord Bot 版）

---

## 🎯 任務目標

請幫我用 **Python** 撰寫一個 **Discord Bot**，用於抓取台灣股市資料並自動產出分析報告。  
Bot 須具備以下兩種觸發模式：

- **自動模式**：每個交易日 **17:00** 自動抓取資料，並將報告發送至指定的 Discord 頻道
- **手動模式**：使用者在 Discord 頻道中按下「**📊 取得資料**」按鈕，立即觸發抓取並回傳報告

報告內容供 AI 進行資料確認與分析使用。

---

## 🤖 Discord Bot 架構規格

### Bot 基本設定

- 使用 **discord.py 2.x**（支援 Slash Command 與 UI Components）
- Bot Token 與頻道 ID 統一存放於 `.env` 檔案
- Bot 啟動後自動在指定頻道發送「控制面板」訊息，內含互動按鈕

### 控制面板訊息（Bot 啟動後自動發送）

Bot 啟動後，於指定頻道發送一則**常駐控制面板 Embed**，包含：

```
📈 台灣股市資料抓取系統
─────────────────────────
🕔 每日 17:00 自動推播
📅 最後更新：YYYY/MM/DD HH:MM

[📊 取得資料]  [📅 指定日期]  [ℹ️ 系統狀態]
```

### 按鈕功能定義

| 按鈕 | 功能說明 |
|------|----------|
| 📊 取得資料 | 立即抓取今日股市資料並產出報告，發送至頻道 |
| 📅 指定日期 | 彈出 Modal 輸入框，讓使用者輸入日期（格式 YYYYMMDD），抓取指定日期資料 |
| ℹ️ 系統狀態 | 顯示 Bot 運作狀態、排程下次執行時間、快取狀況 |

### 報告發送方式

由於 Discord 單則訊息有 **2000 字元上限**，報告須拆分為多則 **Embed 訊息**依序發送：

| Embed 編號 | 對應區塊 |
|------------|----------|
| Embed 1 | 【A】大盤總覽 |
| Embed 2 | 【B】上市強勢 / 弱勢個股 |
| Embed 3 | 【C】三大法人買賣超 |
| Embed 4 | 【D】融資融券變化 |
| Embed 5 | 【E】投顧分析輔助資料（依設定規模顯示） |

每則 Embed 包含：
- 標題（含 emoji 識別）
- 資料表格（使用 code block 排版）
- 頁尾顯示資料來源與產出時間

同時，Bot 也須將完整報告另存為 `.md` 檔案，並以 **Discord 附件**方式上傳至頻道，供 AI 分析使用。

---

## 📁 輸出檔案規格

- **Discord 推播格式**：多則 Embed 訊息（即時顯示於頻道）
- **附件格式**：`.md` 檔案（AI 分析用，以附件上傳）
- **命名規則**：`taiwan_stock_report_YYYYMMDD.md`
- **本地備份路徑**：`./reports/YYYY-MM/`（依年月自動建立資料夾）
- **編碼**：UTF-8

---

## 🗂️ 報告必須包含的資料區塊

### 【區塊 A】大盤總覽

| 項目 | 資料來源 |
|------|----------|
| 加權指數（收盤、漲跌點、漲跌幅） | TWSE MI_INDEX |
| 成交量（億元）、成交筆數 | TWSE MI_INDEX |
| 台灣50核心權值股（前50大權重股今日表現） | TWSE BWIBBU_d + MI_INDEX |
| 各類股指數（電子、金融、傳產等分類漲跌） | TWSE MI_INDEX20 |
| 上市 OTC 市場整體漲跌家數統計 | TWSE MI_INDEX |

---

### 【區塊 B】強勢 / 弱勢個股（投顧核心篩選資料）

#### B-1 上市（TWSE）

| 項目 | 篩選條件 | 資料來源 |
|------|----------|----------|
| 漲停板個股清單 | 漲幅 = +10%（或達漲停價） | TWSE MI_INDEX ALLBUT0999 |
| 漲幅 ≥ 5% 個股清單 | 漲幅 ≥ 5% 且未漲停 | TWSE MI_INDEX ALLBUT0999 |
| 跌停板個股清單 | 跌幅 = -10%（或達跌停價） | TWSE MI_INDEX ALLBUT0999 |
| 跌幅 ≥ 5% 個股清單 | 跌幅 ≥ 5% 且未跌停 | TWSE MI_INDEX ALLBUT0999 |

**每筆個股需顯示**：股票代號、股票名稱、開盤價、最高價、最低價、收盤價、漲跌幅（%）、成交量（張）、本益比、殖利率

---

### 【區塊 C】三大法人買賣超（投顧必追蹤）

#### C-1 上市三大法人彙總（TWSE T86）

| 法人別 | 買超金額 | 賣超金額 | 淨買超 |
|--------|----------|----------|--------|
| 外資及陸資 | | | |
| 投信 | | | |
| 自營商 | | | |
| **合計** | | | |

#### C-2 外資買超 / 賣超排行（前20名）

- 來源：TWSE TWT38U
- 欄位：排名、股票代號、名稱、買超張數、賣超張數、淨買超張數

#### C-3 投信買超 / 賣超排行（前20名）

- 來源：TWSE TWT44U
- 欄位：排名、股票代號、名稱、買超張數、賣超張數、淨買超張數

---

### 【區塊 D】融資融券變化（籌碼面關鍵指標）

#### D-1 上市融資融券彙總（TWSE MI_MARGN）

| 指標 | 今日餘額 | 較前日增減 | 增減幅（%） |
|------|----------|------------|-------------|
| 融資餘額（億元） | | | |
| 融券餘額（張） | | | |
| 融資使用率（%） | | | |
| 資券互抵張數 | | | |

---

### 【區塊 E】投顧分析輔助資料（依投顧公司規模新增）

> 此區塊為針對**投顧公司日常研究與推薦作業**所需，依規模分級提供。  
> 可於 `.env` 設定 `FIRM_LEVEL=basic / standard / professional` 控制顯示層級。

#### E-1 基礎級（小型投顧 / 個人研究員）`FIRM_LEVEL=basic`

- 個股本益比 / 殖利率 / 股價淨值比彙整表（TWSE BWIBBU_d）
- 當日成交量排行前30名（上市）
- 52週高低點突破個股清單（需與歷史資料比對）

#### E-2 標準級（中型投顧 / 具研究團隊）`FIRM_LEVEL=standard`

包含 E-1 全部內容，額外新增：

- 類股輪動熱力圖資料（各類股今日漲跌幅排名，來源 MI_INDEX20）
- 外資連續買超 / 賣超 N 日個股追蹤（需累積每日資料）
- 投信連續買超 / 賣超 N 日個股追蹤
- 當日融資大幅增加（>20%）/ 減少個股警示清單
- 當日融券大幅增加（>20%）/ 減少個股警示清單（軋空觀察）

#### E-3 專業級（大型投顧 / 法人研究部門）`FIRM_LEVEL=professional`

包含 E-1、E-2 全部內容，額外新增：

- 三大法人合力買超前20名（外資＋投信＋自營同步買超）
- 三大法人合力賣超前20名
- 外資 vs 投信 籌碼背離個股（一買一賣）清單
- 法人買超且同日漲停個股（強勢確認）
- 法人買超且同日大量（成交量為近5日均量3倍以上）個股
- 當日融資增加但股價下跌個股（籌碼惡化警示）
- 當日融券增加且法人同步賣超個股（做空訊號強化）

---

## ⚙️ 程式功能規格

### 排程設定

- 使用 **APScheduler** 整合至 discord.py 事件迴圈
- 排程時間：每個交易日 **17:00 UTC+8（台灣時間）**
- 需判斷是否為台灣交易日（排除週六、週日與國定假日）
- 非交易日自動跳過，並於頻道發送簡短通知：「今日非交易日，無資料更新」
- 排程執行時與手動按鈕觸發時，共用同一套資料抓取邏輯

### 錯誤處理與通知

- 每個 API 呼叫需有 retry 機制（最多3次，間隔5秒）
- API 失敗時，該區塊 Embed 標記「⚠️ 資料取得失敗，請手動確認」，不影響其他區塊發送
- 所有錯誤記錄至 `./logs/YYYYMMDD.log`
- 發生嚴重錯誤時，Bot 透過 Discord 私訊通知管理員（`ADMIN_USER_ID` 設於 `.env`）

### 資料快取

- 同一天重複觸發不重複呼叫 API（快取至 `./cache/YYYYMMDD/`）
- 使用者按下「📊 取得資料」時，若當日快取已存在，詢問是否強制重新抓取（透過 Discord 確認按鈕）

### 權限控制

- 「📊 取得資料」與「📅 指定日期」按鈕：限指定角色（Role）或頻道成員可使用
- 可於 `.env` 設定 `ALLOWED_ROLE_ID` 控制可操作角色

---

## 📡 資料來源彙整

### 證交所（TWSE）公開 API

| 資料項目 | API 網址 |
|----------|----------|
| 大盤加權指數統計 | `https://www.twse.com.tw/rwd/zh/afterTrading/MI_INDEX?date=YYYYMMDD&type=ALL&response=json` |
| 全部上市股票收盤行情 | `https://www.twse.com.tw/rwd/zh/afterTrading/MI_INDEX?date=YYYYMMDD&type=ALLBUT0999&response=json` |
| 三大法人買賣超彙總 | `https://www.twse.com.tw/rwd/zh/fund/T86?date=YYYYMMDD&selectType=ALL&response=json` |
| 外資買賣超排行 | `https://www.twse.com.tw/rwd/zh/fund/TWT38U?date=YYYYMMDD&response=json` |
| 投信買賣超排行 | `https://www.twse.com.tw/rwd/zh/fund/TWT44U?date=YYYYMMDD&response=json` |
| 融資融券餘額彙總 | `https://www.twse.com.tw/rwd/zh/marginTrading/MI_MARGN?date=YYYYMMDD&selectType=ALL&response=json` |
| 各類股指數 | `https://www.twse.com.tw/rwd/zh/afterTrading/MI_INDEX20?date=YYYYMMDD&response=json` |
| 個股本益比/殖利率 | `https://www.twse.com.tw/rwd/zh/afterTrading/BWIBBU_d?date=YYYYMMDD&selectType=ALL&response=json` |

---

## 📊 Discord Embed 輸出格式要求

- 漲幅數值以 🔴 紅色文字標示、跌幅以 🟢 綠色文字標示（台灣慣例，使用 Embed color 輔助）
- 個股表格使用 ` ``` ` code block 排版，對齊欄位
- 每則 Embed 頁尾（footer）顯示：資料來源、產出時間（台灣時間）
- 數字統一格式：漲跌幅保留2位小數、金額單位標示億/萬
- 報告最前方（第一則 Embed）包含「**今日市場摘要**」，用3-5句話概述當日重點

---

## 📦 環境需求

```
Python 3.10+
套件：discord.py>=2.3, APScheduler, requests, pandas, python-dotenv, pytz
執行環境：Windows 10/11 或 Linux Server（可長時間掛機）
```

### `.env` 設定範例

```env
DISCORD_TOKEN=your_bot_token_here
CHANNEL_ID=your_channel_id_here
ADMIN_USER_ID=your_discord_user_id_here
ALLOWED_ROLE_ID=your_role_id_here
FIRM_LEVEL=standard
SCHEDULE_TIME=17:00
TIMEZONE=Asia/Taipei
```

請同時提供：

1. `requirements.txt`
2. `README.md`（含 Bot 建立教學、Token 取得步驟、Discord 開發者後台設定、部署說明）
3. 主程式 `bot.py`
4. 資料抓取模組 `fetcher.py`
5. 報告產生模組 `report.py`
6. Discord UI 元件 `views.py`（按鈕、Modal）

---

## ⚠️ 注意事項

- 所有 API 請求需加入適當的 `User-Agent` Header，避免被擋
- 交易日判斷：排除週六、週日，以及台灣國定假日（可內建假日清單或呼叫 TWSE 行事曆）
- 請在每個 API 呼叫間加入 1-2 秒延遲，避免對交易所造成過大請求壓力
- Discord Bot 需開啟以下 Privileged Gateway Intents：`Message Content Intent`
- Bot 需具備頻道的 `Send Messages`、`Embed Links`、`Attach Files`、`Read Message History` 權限
- 資料版權屬於 TWSE，本程式僅供個人研究使用
