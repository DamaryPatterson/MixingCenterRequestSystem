using Microsoft.AspNetCore.Mvc;
using MixingCenterRequests.Services;

namespace MixingCenterRequests.Controllers;

public class DevController : Controller
{
    private readonly FakeDataService? _fake;
    public DevController(IDataService data) => _fake = data as FakeDataService;

    public IActionResult Index() => _fake is null ? NotFound() : View(_fake);

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SetRole(string role)
    {
        if (_fake is null) return NotFound();
        Response.Cookies.Append("devrole", role);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Approve(int id, bool approve, string? comment)
    {
        if (_fake is null) return NotFound();
        _fake.SetApproval(id, approve, comment ?? "");
        TempData["Message"] = $"Request #{id} {(approve ? "approved" : "rejected")} (simulated City Manager).";
        return RedirectToAction(nameof(Index));
    }
}