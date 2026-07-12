namespace StockWeb.Models;

/// <summary>市場寬度計算結果 DTO。演算法對齊 StockDCbot analysis.py，數值以 Python 版為準。</summary>

/// <summary>A/D Line 單日點：漲家、跌家、當日淨額、累積值（原點為區間最舊日）。</summary>
public record AdLinePoint(string Date, long Up, long Down, long Net, long Cumulative);

/// <summary>A/D Line 結果：近 recent 日序列、趨勢（與 compare 日前比較）、比較日數、樣本天數。</summary>
public record AdLineResult(IReadOnlyList<AdLinePoint> Series, string? Trend, int CompareDays, int SampleDays);

/// <summary>量能溫度：當日成交金額、近 N 日均值、倍數（當日/均值）、樣本天數。</summary>
public record VolumeTemperatureResult(double? Today, double? Average, double? Multiple, int SampleDays);

/// <summary>法人流向單日點：當日淨額（張）與累積（張，原點為窗最舊日）。</summary>
public record FlowPoint(string Date, long Net, long Cumulative);

/// <summary>法人流向結果：近 recent 日序列、趨勢、樣本天數。</summary>
public record FlowResult(IReadOnlyList<FlowPoint> Series, string? Trend, int SampleDays);

/// <summary>/api/market/breadth 回應：A/D Line、量能溫度、三大法人近 N 日累積。</summary>
public record BreadthResponse(
    AdLineResult AdLine,
    VolumeTemperatureResult VolumeTemperature,
    FlowResult ForeignFlow,
    FlowResult TrustFlow,
    FlowResult DealerFlow);
