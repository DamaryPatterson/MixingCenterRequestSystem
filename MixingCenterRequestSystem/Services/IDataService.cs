using MixingCenterRequests.Models;

namespace MixingCenterRequests.Services;

public interface IDataService
{
    Task<List<Product>> GetCatalog();
    Task<List<string>> GetMixingCenters();
    Task<List<RequestHeader>> GetRequestsByUser(string email);
    Task<List<RequestHeader>> GetApprovedRequests();
    Task<RequestHeader?> GetRequest(int id);
    Task<List<RequestLine>> GetLines(int requestId);
    Task<int> CreateRequest(string center, string requestedBy, DateTime dateNeeded,
                            IEnumerable<(Product product, double qty)> lines);
    Task UpdateLine(int lineId, string status, double qtySupplied, string comment);
    Task UpdateReceivedQuantity(int lineId, double qtyReceived);
    Task UpdateRequestStatus(int id, string status);
    Task SendMail(string to, string subject, string html);
}
