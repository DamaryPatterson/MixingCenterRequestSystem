using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MixingCenterRequests.Infrastructure;
using MixingCenterRequests.Models;
using MixingCenterRequests.Services;

namespace MixingCenterRequests.Controllers;

[Authorize(Roles = "MixingCenter")]
public class RequestsController : Controller
{
    //private readonly SharePointService _sp;
    //public RequestsController(SharePointService sp) => _sp = sp;

    private readonly IDataService _sp;
    public RequestsController(IDataService sp) => _sp = sp;   

    [HttpGet]
    public async Task<IActionResult> Create() =>
        View(new NewRequestVm { Catalog = await _sp.GetCatalog(), Centers = await _sp.GetMixingCenters() });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NewRequestVm vm)
    {
        var catalog = await _sp.GetCatalog();
        var centers = await _sp.GetMixingCenters();

        if (!centers.Contains(vm.MixingCenter))
            ModelState.AddModelError(nameof(vm.MixingCenter), "Select a valid mixing center.");
        if (vm.Lines.Count == 0)
            ModelState.AddModelError("", "Add at least one item.");
        if (vm.DateNeeded < DateTime.Today)
            ModelState.AddModelError(nameof(vm.DateNeeded), "Date needed can't be in the past.");

        // Never trust posted item details; resolve against the catalog
        var resolved = new List<(CatalogItem, double)>();
        foreach (var line in vm.Lines)
        {
            var item = catalog.FirstOrDefault(c => c.ItemCode == line.ItemCode);
            if (item is null) ModelState.AddModelError("", $"Unknown item {line.ItemCode}.");
            else resolved.Add((item, line.Qty));
        }

        if (!ModelState.IsValid)
        {
            vm.Catalog = catalog;
            vm.Centers = centers;
            return View(vm);
        }

        var id = await _sp.CreateRequest(vm.MixingCenter, User.Email(), vm.DateNeeded!.Value, resolved);
        TempData["Message"] = $"Request #{id} submitted for City Manager approval.";
        return RedirectToAction(nameof(Mine));
    }

    public async Task<IActionResult> Mine() =>
        View(await _sp.GetRequestsByUser(User.Email()));

    public async Task<IActionResult> Details(int id)
    {
        var header = await _sp.GetRequest(id);
        if (header is null) return NotFound();
        if (header.RequestedBy != User.Email()) return Forbid();   // only your own requests
        return View(new RequestDetailsVm { Header = header, Lines = await _sp.GetLines(id) });
    }
}