using System.Net;
using System.Text;

namespace GameOptEngine.Service;

public static class ActivityViewer
{
    public static string RenderHtml(IReadOnlyList<ActivityRecord> records)
    {
        var rows = new StringBuilder();
        foreach (var record in records.OrderByDescending(record => record.Index))
        {
            rows.Append("<tr><td>").Append(record.Index)
                .Append("</td><td>").Append(WebUtility.HtmlEncode(record.TimestampUtc.ToString("u")))
                .Append("</td><td>").Append(WebUtility.HtmlEncode(record.Activity))
                .Append("</td><td>").Append(WebUtility.HtmlEncode(record.Details))
                .AppendLine("</td></tr>");
        }

        return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><meta http-equiv=\"refresh\" content=\"5\"><title>GameOptEngine Activity</title><style>body{font:15px system-ui,sans-serif;margin:2rem;background:#10151d;color:#e8edf4}h1{margin-bottom:.3rem}p{color:#aeb9c8}table{width:100%;border-collapse:collapse;margin-top:1rem}th,td{text-align:left;padding:.7rem;border-bottom:1px solid #344052}th{color:#9fc7ff}td:first-child{font-variant-numeric:tabular-nums}</style></head><body><h1>GameOptEngine Activity</h1><p>Indexed activity records, newest first. Auto-refreshes every 5 seconds. <a href=\"/api/activity\">JSON</a></p><table><thead><tr><th>Index</th><th>Timestamp (UTC)</th><th>Activity</th><th>Details</th></tr></thead><tbody>" + rows + "</tbody></table></body></html>";
    }
}
