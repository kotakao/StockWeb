namespace StockWeb.Models;

/// <summary>
/// 單一資料表的覆蓋範圍：最早日期、最新日期與去重後的日數。
/// 空表時 MinDate/MaxDate 為 null、DistinctDays 為 0。
/// </summary>
public record CoverageRow
{
    public string TableName { get; init; } = "";
    public string? MinDate { get; init; }
    public string? MaxDate { get; init; }
    public int DistinctDays { get; init; }
}
