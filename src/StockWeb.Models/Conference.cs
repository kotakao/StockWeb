namespace StockWeb.Models;

/// <summary>
/// 法說會「說明」欄結構化文字的解析結果（召開時間／地點／擇要訊息）。任一欄解析不到為 null，
/// 由 UI 決定是否退回顯示原文（解析失敗不中斷）。
/// </summary>
public record ConferenceDetail(string? MeetingTime, string? Location, string? Summary);

/// <summary>
/// 一場法說會公告（investor_conferences 的一列，唯讀）。FactDate＝事實發生日＝召開日（ISO）；
/// Detail 為「說明」欄的解析結果，Description 保留原文供解析失敗時顯示。
/// InWatchlist 供行事曆高亮（個股頁不使用，預設 false）。
/// </summary>
public record Conference(
    string Code,
    string? Name,
    string? Subject,
    string? AnnounceDate,
    string? FactDate,
    string? Description,
    ConferenceDetail Detail,
    bool InWatchlist = false);
