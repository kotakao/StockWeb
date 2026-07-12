namespace StockWeb.Components.Charts;

/// <summary>
/// Lightweight Charts 圖表規格（C# 端組裝，經 JS Interop 傳給 charts.js 繪製）。
/// 與圖表庫解耦的最小抽象，供 Dashboard 與後續個股頁重用。
/// </summary>
public record ChartSpec(IReadOnlyList<ChartSeries> Series, bool LeftScale = false, int Height = 260);

/// <summary>單一資料序列。Type：line / histogram / area。PriceScaleId：right（預設）或 left（雙軸）。</summary>
public record ChartSeries(
    string Type,
    IReadOnlyList<ChartPoint> Data,
    string? Color = null,
    string? PriceScaleId = null,
    string? Title = null);

/// <summary>時間點：Time 為 YYYY-MM-DD，Value 為數值。</summary>
public record ChartPoint(string Time, double Value);
