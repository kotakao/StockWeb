namespace StockWeb.Components.Charts;

/// <summary>
/// Lightweight Charts 圖表規格（C# 端組裝，經 JS Interop 傳給 charts.js 繪製）。
/// 與圖表庫解耦的最小抽象，供 Dashboard 與後續個股頁重用。
/// </summary>
public record ChartSpec(IReadOnlyList<ChartSeries> Series, bool LeftScale = false, int Height = 260);

/// <summary>
/// 單一資料序列。Type：line / histogram / area / candlestick。PriceScaleId：right（預設）或 left（雙軸）。
/// candlestick 型別改用 Candles 資料（K 線紅漲綠跌由 charts.js 依台灣慣例上色），Data 傳空集合即可。
/// </summary>
public record ChartSeries(
    string Type,
    IReadOnlyList<ChartPoint> Data,
    string? Color = null,
    string? PriceScaleId = null,
    string? Title = null,
    IReadOnlyList<ChartCandle>? Candles = null);

/// <summary>時間點：Time 為 YYYY-MM-DD，Value 為數值。Color 可選（histogram 逐柱上色，如漲跌紅綠）。</summary>
public record ChartPoint(string Time, double Value, string? Color = null);

/// <summary>K 線單根：Time 為 YYYY-MM-DD，四價為前復權後（或原始）OHLC。</summary>
public record ChartCandle(string Time, double Open, double High, double Low, double Close);
