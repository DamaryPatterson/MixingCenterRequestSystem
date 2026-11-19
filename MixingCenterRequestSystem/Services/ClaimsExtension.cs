using Microsoft.AspNetCore.Mvc;

namespace MixingCenterRequests.Models;

public record Product(string Title, string ProductCode, string Category, string Uom);

public class RequestHeader
{
    public int Id { get; set; }
    public string MixingCenter { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    public DateTime? DateNeeded { get; set; }
    public string Status { get; set; } = "";
    public string ApproverComments { get; set; } = "";
}

public class RequestLine
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public string Title { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public double QtyRequested { get; set; }
    public double QtySupplied { get; set; }
    public string LineStatus { get; set; } = "";
    public string DscComment { get; set; } = "";
}