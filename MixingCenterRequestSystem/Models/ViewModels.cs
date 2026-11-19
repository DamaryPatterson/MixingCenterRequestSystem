using System.ComponentModel.DataAnnotations;

namespace MixingCenterRequests.Models;

public class NewRequestVm
{
    [Required] public string MixingCenter { get; set; } = "";
    [Required] public DateTime? DateNeeded { get; set; }
    public List<LineInput> Lines { get; set; } = new();

    // Dropdown data (not posted)
    public List<Product> Catalog { get; set; } = new();
    public List<string> Centers { get; set; } = new();
}

public class LineInput
{
    [Required] public string ProductCode { get; set; } = "";
    [Range(0.01, 100000)] public double Qty { get; set; }
}

public class RequestDetailsVm
{
    public RequestHeader Header { get; set; } = new();
    public List<RequestLine> Lines { get; set; } = new();
}

public class ReceiptVm
{
    public int RequestId { get; set; }
    public List<ReceiptLineInput> Lines { get; set; } = new();
}

public class ReceiptLineInput
{
    public int LineId { get; set; }
    public double? QtyReceived { get; set; }
}

public class FulfilVm
{
    public int RequestId { get; set; }
    public RequestHeader? Header { get; set; }
    public List<FulfilLine> Lines { get; set; } = new();
}

public class FulfilLine
{
    public int LineId { get; set; }
    public string Title { get; set; } = "";
    public double QtyRequested { get; set; }
    public bool Supply { get; set; } = true;
    public double QtySupplied { get; set; }
    public string? Comment { get; set; }
}