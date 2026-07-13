namespace StockWeb.Models;

/// <summary>個股頁 DTO。序列一律以日期由舊到新排列，供 Lightweight Charts 時間軸使用。</summary>

/// <summary>個股單日 OHLCV。前復權時 OHLC 四價同步調整、成交量不調整（§7）。</summary>
public record StockQuote(string Date, double? Open, double? High, double? Low, double? Close, double? Volume);

/// <summary>
/// /api/stocks/{code}/quotes 回應：代號、名稱、市場（TWSE/TPEX，供上市/上櫃判別）、
/// 是否為前復權序列、OHLCV 序列。查無代號時 Name/Market 為 null 且 Quotes 為空（非 exception）。
/// </summary>
public record StockQuotesResponse(
    string Code,
    string? Name,
    string? Market,
    bool Adjusted,
    IReadOnlyList<StockQuote> Quotes);

/// <summary>個股逐日三大法人買賣超（張以外皆沿用資料庫原值，單位為股）。</summary>
public record StockInstitutionalRow(string Date, double? ForeignNet, double? TrustNet, double? DealerNet);

/// <summary>個股逐日融資券餘額。</summary>
public record StockMarginRow(string Date, double? MarginBalance, double? ShortBalance);

/// <summary>個股逐日估值（本益比／殖利率／淨值比）。</summary>
public record StockValuationRow(string Date, double? Pe, double? DividendYield, double? Pb);

/// <summary>個股月營收與 YoY。YearMonth 為 YYYY-MM；CumYoyPct 為累計營收 YoY。</summary>
public record StockRevenueRow(string YearMonth, double? Revenue, double? YoyPct, double? CumYoyPct);

/// <summary>
/// 個股季度損益（quarterly_financials 最新至舊）。YearQuarter 為 YYYYQn。
/// 金額欄位單位仟元、Eps 為元；GrossMargin／OperatingMargin 為讀取端計算的百分比（%），
/// 缺營收時為 null（不存於資料庫，見 FinancialsCalculator）。
/// </summary>
public record StockFinancialRow(
    string YearQuarter,
    double? Revenue,
    double? GrossProfit,
    double? OperatingIncome,
    double? NetIncome,
    double? Eps,
    double? GrossMargin,
    double? OperatingMargin);

/// <summary>
/// 前復權事件輸入（AdjustedPriceService 用）。ExDate 為 ISO 日期字串（YYYY-MM-DD，
/// 可字典序比較即為時間序）；CashDividend 現金股利、StockRatio 配股率。
/// </summary>
public record DividendAdjustment(string ExDate, double CashDividend, double StockRatio);
