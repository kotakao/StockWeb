using StockWeb.Models;
using StockWeb.Services;

namespace StockWeb.Tests;

/// <summary>
/// 持股損益計算的邊界測試：正常持股、零成本（報酬率無法計算）、無持股（各欄 null）、
/// 除權息後成本情境（一律以未還原收盤與名目成本計算，不調整成本）、聚合。
/// </summary>
public class HoldingsCalculatorTests
{
    [Fact]
    public void NormalHolding_ComputesValuePnlAndReturn()
    {
        // 1000 股、均價 100、收盤 110、當日漲跌額 +5。
        Assert.Equal(110_000, HoldingsCalculator.MarketValue(1000, 110));
        Assert.Equal(10_000, HoldingsCalculator.UnrealizedPnl(1000, 100, 110));
        Assert.Equal(10, HoldingsCalculator.ReturnRate(100, 110));   // (110-100)/100*100 = 10%
        Assert.Equal(5_000, HoldingsCalculator.DayPnl(1000, 5));
    }

    [Fact]
    public void LossHolding_IsNegative()
    {
        Assert.Equal(-2_000, HoldingsCalculator.UnrealizedPnl(1000, 100, 98));
        Assert.Equal(-2, HoldingsCalculator.ReturnRate(100, 98));
        Assert.Equal(-3_000, HoldingsCalculator.DayPnl(1000, -3));
    }

    [Fact]
    public void ZeroCost_ReturnRateIsNull_ButValueAndPnlStillComputed()
    {
        // 零成本（例如成本已完全回收）：報酬率無法計算（除以 0）→ null；市值與損益仍可算。
        Assert.Null(HoldingsCalculator.ReturnRate(0, 110));
        Assert.Equal(110_000, HoldingsCalculator.MarketValue(1000, 110));
        Assert.Equal(110_000, HoldingsCalculator.UnrealizedPnl(1000, 0, 110));
    }

    [Fact]
    public void NoHolding_AllNull()
    {
        Assert.Null(HoldingsCalculator.MarketValue(null, 110));
        Assert.Null(HoldingsCalculator.UnrealizedPnl(null, null, 110));
        Assert.Null(HoldingsCalculator.ReturnRate(null, 110));
        Assert.Null(HoldingsCalculator.DayPnl(null, 5));
    }

    [Fact]
    public void MissingClose_YieldsNull()
    {
        // 停牌／當日無收盤：需要收盤的欄位皆為 null（不丟例外）。
        Assert.Null(HoldingsCalculator.MarketValue(1000, null));
        Assert.Null(HoldingsCalculator.UnrealizedPnl(1000, 100, null));
        Assert.Null(HoldingsCalculator.ReturnRate(100, null));
    }

    [Fact]
    public void PostDividend_UsesRawCloseAndNominalCost_NotAdjusted()
    {
        // 除權息後情境：使用者名目成本 100；除息 5 元後收盤自然落到 96。
        // 計算一律以「未還原收盤 96」對「名目成本 100」——不因除權息調整成本，故呈現帳面虧損。
        Assert.Equal(-4_000, HoldingsCalculator.UnrealizedPnl(1000, 100, 96));
        Assert.Equal(-4, HoldingsCalculator.ReturnRate(100, 96));
        Assert.Equal(96_000, HoldingsCalculator.MarketValue(1000, 96));
    }

    [Fact]
    public void Summarize_SumsHoldingsAndIgnoresNulls()
    {
        var rows = new List<WatchlistStatusRow>
        {
            new() { Code = "2330", MarketValue = 110_000, UnrealizedPnl = 10_000, DayPnl = 5_000 },
            new() { Code = "2317", MarketValue = 50_000, UnrealizedPnl = -2_000, DayPnl = -1_000 },
            new() { Code = "1101" }, // 純觀察，無持股 → 各欄 null，聚合視為 0
        };

        var summary = HoldingsCalculator.Summarize(rows);

        Assert.Equal(160_000, summary.TotalMarketValue);
        Assert.Equal(8_000, summary.TotalUnrealizedPnl);
        Assert.Equal(4_000, summary.TotalDayPnl);
    }
}
