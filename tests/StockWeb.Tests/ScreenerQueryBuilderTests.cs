using StockWeb.Models;
using StockWeb.Services;

namespace StockWeb.Tests;

/// <summary>
/// SQL 組譯測試：驗證骨架、參數化（使用者輸入只出現在 Parameters、不入 SQL 字串）、
/// 各條件對應的 JOIN/欄位/述詞是否只在啟用時出現。
/// </summary>
public class ScreenerQueryBuilderTests
{
    [Fact]
    public void Build_NoConditions_ReturnsBaseQueryWithoutParamsOrFilters()
    {
        var query = ScreenerQueryBuilder.Build(new ScreenerCriteria());

        Assert.Empty(query.Parameters);                       // ALL：無 @market 參數
        Assert.Contains("FROM daily_quotes q", query.Sql);
        Assert.Contains("LIMIT 200", query.Sql);
        Assert.Contains("ORDER BY t.Code", query.Sql);
        Assert.DoesNotContain("@market", query.Sql);
        Assert.DoesNotContain("LEFT JOIN valuation", query.Sql);
        Assert.DoesNotContain("institutional", query.Sql);
        Assert.DoesNotContain("VolumeMultiple", query.Sql);
        Assert.DoesNotContain("WHERE t.", query.Sql);          // 無外層條件
    }

    [Theory]
    [InlineData("TWSE")]
    [InlineData("twse")]   // 大小寫不敏感，正規化為大寫
    [InlineData(" TpEx ")]
    public void Build_SpecificMarket_ParameterizesMarket(string market)
    {
        var query = ScreenerQueryBuilder.Build(new ScreenerCriteria { Market = market });

        Assert.Contains("q.market = @market", query.Sql);
        Assert.Equal(market.Trim().ToUpperInvariant(), query.Parameters["market"]);
    }

    [Fact]
    public void Build_MarketAll_HasNoMarketFilter()
    {
        var query = ScreenerQueryBuilder.Build(new ScreenerCriteria { Market = "ALL" });

        Assert.DoesNotContain("@market", query.Sql);
        Assert.False(query.Parameters.ContainsKey("market"));
    }

    [Fact]
    public void Build_ValuationConditions_JoinValuationOnce_AndParameterize()
    {
        var query = ScreenerQueryBuilder.Build(new ScreenerCriteria
        {
            PeMax = 15,
            DividendYieldMin = 3,
            PbMax = 2,
        });

        // 三項估值條件共用單一 valuation JOIN。
        var joinCount = query.Sql.Split("LEFT JOIN valuation").Length - 1;
        Assert.Equal(1, joinCount);
        Assert.Contains("t.Pe <= @pe_max", query.Sql);
        Assert.Contains("t.DividendYield >= @dividend_yield_min", query.Sql);
        Assert.Contains("t.Pb <= @pb_max", query.Sql);
        Assert.Equal(15.0, query.Parameters["pe_max"]);
        Assert.Equal(3.0, query.Parameters["dividend_yield_min"]);
        Assert.Equal(2.0, query.Parameters["pb_max"]);
        // 使用者輸入的數值不得以字面量出現在 SQL。
        Assert.DoesNotContain("15", query.Sql);
    }

    [Fact]
    public void Build_RevenueYoy_JoinsMonthlyRevenue()
    {
        var query = ScreenerQueryBuilder.Build(new ScreenerCriteria { RevenueYoyMin = 10 });

        Assert.Contains("LEFT JOIN monthly_revenue", query.Sql);
        Assert.Contains("t.RevenueYoy >= @revenue_yoy_min", query.Sql);
        Assert.Equal(10.0, query.Parameters["revenue_yoy_min"]);
    }

    [Fact]
    public void Build_ForeignBuyDays_EmitsStreakSubqueryAndPredicate()
    {
        var query = ScreenerQueryBuilder.Build(new ScreenerCriteria { ForeignBuyDays = 3 });

        Assert.Contains("FROM institutional i", query.Sql);
        Assert.Contains("foreign_net > 0", query.Sql);
        Assert.Contains("AS ForeignBuyDays", query.Sql);
        Assert.Contains("t.ForeignBuyDays >= @foreign_buy_days", query.Sql);
        Assert.Equal(3, query.Parameters["foreign_buy_days"]);
    }

    [Fact]
    public void Build_TrustBuyDays_UsesTrustNetColumn()
    {
        var query = ScreenerQueryBuilder.Build(new ScreenerCriteria { TrustBuyDays = 2 });

        Assert.Contains("trust_net > 0", query.Sql);
        Assert.Contains("t.TrustBuyDays >= @trust_buy_days", query.Sql);
        Assert.Equal(2, query.Parameters["trust_buy_days"]);
    }

    [Fact]
    public void Build_VolumeMultiple_UsesFiveDayLookback()
    {
        var query = ScreenerQueryBuilder.Build(new ScreenerCriteria { VolumeMultipleMin = 2 });

        Assert.Contains("AS VolumeMultiple", query.Sql);
        Assert.Contains($"LIMIT {ScreenerQueryBuilder.VolumeLookback}", query.Sql);
        Assert.Contains("t.VolumeMultiple >= @volume_multiple_min", query.Sql);
        Assert.Equal(2.0, query.Parameters["volume_multiple_min"]);
    }

    [Fact]
    public void Build_MultipleConditions_CombinedWithAnd()
    {
        var query = ScreenerQueryBuilder.Build(new ScreenerCriteria
        {
            PeMax = 15,
            ForeignBuyDays = 3,
        });

        Assert.Contains("t.Pe <= @pe_max AND t.ForeignBuyDays >= @foreign_buy_days", query.Sql);
        Assert.Equal(2, query.Parameters.Count);
    }

    [Fact]
    public void Build_MaliciousMarket_NeverConcatenatedIntoSql()
    {
        // 即使傳入可疑字串，也只會成為參數值，SQL 仍用 @market 佔位（實際會被 API 層驗證擋下）。
        var query = ScreenerQueryBuilder.Build(new ScreenerCriteria { Market = "TWSE'; DROP TABLE daily_quotes;--" });

        Assert.DoesNotContain("DROP TABLE", query.Sql);
        Assert.Contains("q.market = @market", query.Sql);
    }
}
