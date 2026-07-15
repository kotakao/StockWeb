using System.Text.RegularExpressions;
using StockWeb.Models;

namespace StockWeb.Services;

/// <summary>
/// 法說會「說明」欄結構化文字的純解析器。重大訊息的說明欄為逐行「標籤：值」文字，例如：
///   1.召開法人說明會之日期：115/07/16
///   2.召開法人說明會之時間：14 時 00 分
///   3.召開法人說明會之地點：台中市太平區永豐路78號
///   4.法人說明會擇要訊息：…
/// 抽取召開時間／地點／擇要訊息。任一欄找不到即為 null（由 UI 退回顯示原文）；不丟例外。
/// </summary>
public static partial class ConferenceParser
{
    public static ConferenceDetail Parse(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return new ConferenceDetail(null, null, null);

        string? time = null, location = null, summary = null;

        // 全形或半形冒號皆可能出現；以第一個冒號切分標籤與值。
        foreach (var rawLine in description.Split('\n'))
        {
            var line = rawLine.Trim();
            var idx = line.IndexOfAny(new[] { '：', ':' });
            if (idx <= 0 || idx == line.Length - 1)
                continue;

            var label = line[..idx];
            var value = line[(idx + 1)..].Trim();
            if (value.Length == 0)
                continue;

            if (time is null && label.Contains("時間") && label.Contains("召開"))
                time = NormalizeTime(value);
            else if (location is null && label.Contains("地點") && label.Contains("召開"))
                location = value;
            else if (summary is null && label.Contains("擇要"))
                summary = value;
        }

        return new ConferenceDetail(time, location, summary);
    }

    // "14 時 00 分" → "14:00"；無法辨識時原樣回傳（去除多餘空白）。
    private static string NormalizeTime(string value)
    {
        var m = TimePattern().Match(value);
        if (m.Success)
            return $"{int.Parse(m.Groups[1].Value):D2}:{int.Parse(m.Groups[2].Value):D2}";
        return SpacePattern().Replace(value, "");
    }

    [GeneratedRegex(@"(\d{1,2})\s*時\s*(\d{1,2})\s*分")]
    private static partial Regex TimePattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpacePattern();
}
