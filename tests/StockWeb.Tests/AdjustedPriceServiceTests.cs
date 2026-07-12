using StockWeb.Models;
using StockWeb.Services;

namespace StockWeb.Tests;

/// <summary>
/// 前復權（還原價）純函數測試。案例與 StockDCbot tests/test_analysis.py 的 AdjustHistoryTest 對齊、
/// 數字一致：除息／除權／除權息／無事件／多事件疊加。OHLC 四價同步、量不調、輸入不被修改。
/// </summary>
public class AdjustedPriceServiceTests
{
    // rows: (date, close)；開高低收一律用同值以簡化驗證（比照 Python 版 _hist）。量固定 1000。
    private static List<StockQuote> Hist(params (string Date, double Close)[] rows)
        => rows.Select(r => new StockQuote(r.Date, r.Close, r.Close, r.Close, r.Close, 1000)).ToList();

    private static DividendAdjustment Event(string exDate, double cash, double ratio)
        => new(exDate, cash, ratio);

    [Fact]
    public void NoEvents_ReturnsUnchangedCopy()
    {
        var hist = Hist(("2026-06-03", 100.0), ("2026-06-01", 98.0));
        var outp = AdjustedPriceService.Adjust(hist, Array.Empty<DividendAdjustment>());

        Assert.Equal(new[] { 100.0, 98.0 }, outp.Select(r => r.Close!.Value));
        Assert.NotSame(hist, outp);             // 回傳新集合
        Assert.Equal(98.0, hist[1].Close);      // 未動到輸入
    }

    [Fact]
    public void CashDividend_SubtractsBeforeExDateOnly()
    {
        var hist = Hist(("2026-06-03", 100.0), ("2026-06-02", 100.0), ("2026-06-01", 100.0));
        var outp = AdjustedPriceService.Adjust(hist, new[] { Event("2026-06-02", 2.0, 0.0) });

        // 除息當日(06-02)與其後(06-03)不動；之前(06-01)減 2。
        Assert.Equal(new[] { 100.0, 100.0, 98.0 }, outp.Select(r => r.Close!.Value));
    }

    [Fact]
    public void StockDividend_DividesByRatio()
    {
        var hist = Hist(("2026-06-03", 110.0), ("2026-06-01", 110.0));
        var outp = AdjustedPriceService.Adjust(hist, new[] { Event("2026-06-02", 0.0, 0.1) });

        Assert.Equal(110.0, outp[0].Close!.Value, 6);        // 06-03 不動
        Assert.Equal(100.0, outp[1].Close!.Value, 6);        // 06-01: 110/1.1
    }

    [Fact]
    public void CombinedExRightsAndDividend_SubtractsThenDivides()
    {
        var hist = Hist(("2026-06-02", 100.0), ("2026-06-01", 100.0));
        var outp = AdjustedPriceService.Adjust(hist, new[] { Event("2026-06-02", 1.0, 0.25) });

        Assert.Equal(100.0, outp[0].Close!.Value, 6);
        Assert.Equal((100.0 - 1.0) / 1.25, outp[1].Close!.Value, 6);   // 先減息再除權
    }

    [Fact]
    public void MultipleEvents_StackNewestFirst()
    {
        var hist = Hist(("2026-06-05", 100.0), ("2026-06-03", 100.0), ("2026-06-01", 100.0));
        var events = new[]
        {
            Event("2026-06-04", 2.0, 0.0),    // 較新
            Event("2026-06-02", 0.0, 0.25),   // 較舊
        };
        var outp = AdjustedPriceService.Adjust(hist, events);

        Assert.Equal(100.0, outp[0].Close!.Value, 6);   // 06-05 無事件在其後
        Assert.Equal(98.0, outp[1].Close!.Value, 6);    // 06-03 僅受 06-04 事件: 100-2
        Assert.Equal(78.4, outp[2].Close!.Value, 6);    // 06-01: (100-2)/1.25
    }

    [Fact]
    public void AdjustsAllFourPrices_ButNotVolume()
    {
        var hist = new List<StockQuote> { new("2026-06-01", 10, 12, 8, 11, 5000) };
        var outp = AdjustedPriceService.Adjust(hist, new[] { Event("2026-06-02", 1.0, 0.0) });

        Assert.Equal(9.0, outp[0].Open!.Value, 6);
        Assert.Equal(11.0, outp[0].High!.Value, 6);
        Assert.Equal(7.0, outp[0].Low!.Value, 6);
        Assert.Equal(10.0, outp[0].Close!.Value, 6);
        Assert.Equal(5000, outp[0].Volume!.Value);   // 量不調整
    }
}
