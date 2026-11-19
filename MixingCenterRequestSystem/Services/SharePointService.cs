using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using MixingCenterRequests.Models;

namespace MixingCenterRequests.Services;

public class SharePointService : IDataService
{
    private readonly HttpClient _http;
    private readonly TokenCredential _cred;
    private readonly string _site;
    private readonly string _mailFrom;

    public SharePointService(HttpClient http, TokenCredential cred, IConfiguration cfg)
    {
        _http = http;
        _cred = cred;
        _site = cfg["SharePoint:SiteId"]!;
        _mailFrom = cfg["Mail:From"]!;
    }

    // ---------- Graph plumbing ----------
    private async Task<JsonElement?> Send(HttpMethod method, string path, object? body = null)
    {
        var token = await _cred.GetTokenAsync(
            new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }), default);

        using var req = new HttpRequestMessage(method, "https://graph.microsoft.com/v1.0" + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        req.Headers.Add("Prefer", "HonorNonIndexedQueriesWarningMayFailRandomly");
        if (body != null) req.Content = JsonContent.Create(body);

        using var res = await _http.SendAsync(req);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        var text = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Graph {(int)res.StatusCode}: {text}");
        return string.IsNullOrWhiteSpace(text) ? null : JsonDocument.Parse(text).RootElement.Clone();
    }

    private async Task<List<JsonElement>> GetFields(string list, string? filter = null)
    {
        var path = $"/sites/{_site}/lists/{list}/items?$expand=fields&$top=500";
        if (filter != null) path += "&$filter=" + Uri.EscapeDataString(filter);
        var json = await Send(HttpMethod.Get, path);
        return json!.Value.GetProperty("value").EnumerateArray()
                   .Select(i => i.GetProperty("fields")).ToList();
    }

    // Field names are case-sensitive in SharePoint -> always use a Dictionary (no camelCasing)
    private async Task<int> Create(string list, Dictionary<string, object?> fields)
    {
        var json = await Send(HttpMethod.Post, $"/sites/{_site}/lists/{list}/items", new { fields });
        return int.Parse(json!.Value.GetProperty("id").GetString()!);
    }

    private Task Update(string list, int id, Dictionary<string, object?> fields) =>
        Send(HttpMethod.Patch, $"/sites/{_site}/lists/{list}/items/{id}/fields", fields);

    private static string Str(JsonElement f, string n) =>
        f.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

    private static double Num(JsonElement f, string n) =>
        !f.TryGetProperty(n, out var v) ? 0
        : v.ValueKind == JsonValueKind.Number ? v.GetDouble()
        : double.TryParse(v.GetString(), out var d) ? d : 0;

    private static DateTime? Date(JsonElement f, string n) =>
        DateTime.TryParse(Str(f, n), out var d) ? d : null;

    public static string Esc(string s) => s.Replace("'", "''");

    // ---------- Mapping ----------
    private static RequestHeader ToHeader(JsonElement f) => new()
    {
        Id = int.Parse(Str(f, "id")),
        MixingCenter = Str(f, "MixingCenter"),
        RequestedBy = Str(f, "RequestedBy").ToLowerInvariant(),
        DateNeeded = Date(f, "DateNeeded"),
        Status = Str(f, "Status"),
        ApproverComments = Str(f, "ApproverComments")
    };

    private static RequestLine ToLine(JsonElement f) => new()
    {
        Id = int.Parse(Str(f, "id")),
        RequestId = (int)Num(f, "RequestID"),
        Title = Str(f, "Title"),
        ProductCode = Str(f, "ItemCode"),
        QtyRequested = Num(f, "QtyRequested"),
        QtySupplied = Num(f, "QtySupplied"),
        QtyReceived = Num(f, "QtyReceived"),
        LineStatus = Str(f, "LineStatus"),
        DscComment = Str(f, "DSCComment")
    };

    // ---------- Public API ----------
    public async Task<List<Product>> GetCatalog() =>
        (await GetFields("Items", "fields/Active eq 1"))
        .Select(f => new Product(Str(f, "Title"), Str(f, "ItemCode"), Str(f, "Category"), Str(f, "UOM")))
        .OrderBy(p => p.Title).ToList();

    public async Task<List<string>> GetMixingCenters() =>
        (await GetFields("MixingCenters")).Select(f => Str(f, "Title")).OrderBy(t => t).ToList();

    public async Task<List<RequestHeader>> GetRequests(string filter) =>
        (await GetFields("Requests", filter)).Select(ToHeader).OrderByDescending(r => r.Id).ToList();

    public async Task<RequestHeader?> GetRequest(int id)
    {
        var json = await Send(HttpMethod.Get, $"/sites/{_site}/lists/Requests/items/{id}?$expand=fields");
        return json is null ? null : ToHeader(json.Value.GetProperty("fields"));
    }

    public async Task<List<RequestLine>> GetLines(int requestId) =>
        (await GetFields("RequestLines", $"fields/RequestID eq {requestId}")).Select(ToLine).ToList();

    public async Task<int> CreateRequest(string center, string requestedBy, DateTime dateNeeded,
                                         IEnumerable<(Product product, double qty)> lines)
    {
        var id = await Create("Requests", new()
        {
            ["Title"] = "Colorant/OS Request",
            ["MixingCenter"] = center,
            ["RequestedBy"] = requestedBy,
            ["DateNeeded"] = dateNeeded.ToString("yyyy-MM-dd"),
            ["Status"] = "Pending Approval"
        });

        foreach (var (product, qty) in lines)
            await Create("RequestLines", new()
            {
                ["RequestID"] = id,
                ["Title"] = product.Title,
                ["ItemCode"] = product.ProductCode,
                ["QtyRequested"] = qty,
                ["LineStatus"] = "Pending"
            });

        return id;
    }

    public Task UpdateLine(int lineId, string status, double qtySupplied, string comment) =>
        Update("RequestLines", lineId, new()
        {
            ["LineStatus"] = status,
            ["QtySupplied"] = qtySupplied,
            ["DSCComment"] = comment
        });

    public Task UpdateReceivedQuantity(int lineId, double qtyReceived) =>
        Update("RequestLines", lineId, new() { ["QtyReceived"] = qtyReceived });

    public Task UpdateRequestStatus(int id, string status) =>
        Update("Requests", id, new() { ["Status"] = status });

    public Task SendMail(string to, string subject, string html) =>
        Send(HttpMethod.Post, $"/users/{_mailFrom}/sendMail", new
        {
            message = new
            {
                subject,
                body = new { contentType = "HTML", content = html },
                toRecipients = new[] { new { emailAddress = new { address = to } } }
            },
            saveToSentItems = true
        });

    public Task<List<RequestHeader>> GetRequestsByUser(string email) =>
    GetRequests($"fields/RequestedBy eq '{Esc(email)}'");

    public Task<List<RequestHeader>> GetApprovedRequests() =>
        GetRequests("fields/Status eq 'Approved'");
}