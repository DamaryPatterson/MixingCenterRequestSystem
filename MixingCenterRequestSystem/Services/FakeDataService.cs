using MixingCenterRequests.Models;

namespace MixingCenterRequests.Services;

public class FakeDataService : IDataService
{
    private readonly object _lock = new();
    private int _nextReq = 1, _nextLine = 1;

    private readonly List<Product> _catalog = new()
    {
        new("Black Colorant", "CLR-B1", "Colorant", "QT"),
        new("Red Oxide Colorant", "CLR-R2", "Colorant", "QT"),
        new("Yellow Colorant", "CLR-Y1", "Colorant", "QT"),
        new("Mixing Cups", "OS-CUP", "OS", "Pack"),
        new("Stir Sticks", "OS-STK", "OS", "Box")
    };
    private readonly List<string> _centers = new() { "DSC 190 - Mixing Station" };

    public List<RequestHeader> Requests { get; } = new();
    public List<RequestLine> Lines { get; } = new();
    public List<string> SentMail { get; } = new();

    public Task<List<Product>> GetCatalog() => Task.FromResult(_catalog.ToList());
    public Task<List<string>> GetMixingCenters() => Task.FromResult(_centers.ToList());

    public Task<List<RequestHeader>> GetRequestsByUser(string email)
    { lock (_lock) return Task.FromResult(Requests.Where(r => r.RequestedBy == email).OrderByDescending(r => r.Id).ToList()); }

    public Task<List<RequestHeader>> GetApprovedRequests()
    { lock (_lock) return Task.FromResult(Requests.Where(r => r.Status == "Approved").ToList()); }

    public Task<RequestHeader?> GetRequest(int id)
    { lock (_lock) return Task.FromResult(Requests.FirstOrDefault(r => r.Id == id)); }

    public Task<List<RequestLine>> GetLines(int requestId)
    { lock (_lock) return Task.FromResult(Lines.Where(l => l.RequestId == requestId).ToList()); }

    public Task<int> CreateRequest(string center, string requestedBy, DateTime dateNeeded,
                                   IEnumerable<(Product product, double qty)> lines)
    {
        lock (_lock)
        {
            var id = _nextReq++;
            Requests.Add(new RequestHeader
            {
                Id = id,
                MixingCenter = center,
                RequestedBy = requestedBy,
                DateNeeded = dateNeeded,
                Status = "Pending Approval"
            });
            foreach (var (product, qty) in lines)
                Lines.Add(new RequestLine
                {
                    Id = _nextLine++,
                    RequestId = id,
                    Title = product.Title,
                    ProductCode = product.ProductCode,
                    QtyRequested = qty,
                    LineStatus = "Pending"
                });
            return Task.FromResult(id);
        }
    }

    public Task UpdateLine(int lineId, string status, double qtySupplied, string comment)
    {
        lock (_lock)
        {
            var l = Lines.First(x => x.Id == lineId);
            l.LineStatus = status; l.QtySupplied = qtySupplied; l.DscComment = comment;
        }
        return Task.CompletedTask;
    }

    public Task UpdateRequestStatus(int id, string status)
    { lock (_lock) Requests.First(r => r.Id == id).Status = status; return Task.CompletedTask; }

    public Task SendMail(string to, string subject, string html)
    { lock (_lock) SentMail.Insert(0, $"{DateTime.Now:HH:mm} → {to}: {subject}"); return Task.CompletedTask; }

    // Simulates the City Manager approval that Power Automate will handle
    public void SetApproval(int id, bool approve, string comment)
    {
        lock (_lock)
        {
            var r = Requests.First(x => x.Id == id);
            r.Status = approve ? "Approved" : "Rejected";
            r.ApproverComments = comment;
        }
    }
}