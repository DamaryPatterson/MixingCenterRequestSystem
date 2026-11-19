using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MixingCenterRequests.Models;
using MixingCenterRequests.Services;

namespace MixingCenterRequests.Controllers;

[Authorize(Roles = "DSC")]
public class DscController : Controller
{
    //private readonly SharePointService _sp;
    //public DscController(SharePointService sp) => _sp = sp;

    private readonly IDataService _sp;
    public DscController(IDataService sp) => _sp = sp;
    public async Task<IActionResult> Index() =>
        View(await _sp.GetApprovedRequests());

    [HttpGet]
    public async Task<IActionResult> Fulfil(int id)
    {
        var header = await _sp.GetRequest(id);
        if (header is null) return NotFound();
        if (header.Status != "Approved")
        {
            TempData["Error"] = "This request is not awaiting fulfilment.";
            return RedirectToAction(nameof(Index));
        }

        var lines = await _sp.GetLines(id);
        return View(new FulfilVm
        {
            RequestId = id,
            Header = header,
            Lines = lines.Select(l => new FulfilLine
            {
                LineId = l.Id,
                Title = l.Title,
                QtyRequested = l.QtyRequested,
                Supply = true,
                QtySupplied = l.QtyRequested
            }).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Fulfil(FulfilVm vm)
    {
        var header = await _sp.GetRequest(vm.RequestId);
        if (header is null) return NotFound();
        if (header.Status != "Approved")
        {
            TempData["Error"] = "This request is not awaiting fulfilment.";
            return RedirectToAction(nameof(Index));
        }

        // Validate every line first, then write; uses server-side quantities, not posted ones
        var lines = await _sp.GetLines(vm.RequestId);
        var decisions = new List<(RequestLine line, FulfilLine input)>();
        foreach (var line in lines)
        {
            var input = vm.Lines.FirstOrDefault(l => l.LineId == line.Id);
            if (input is null) return Fail($"Missing decision for {line.Title}.");
            if (input.Supply && (input.QtySupplied <= 0 || input.QtySupplied > line.QtyRequested))
                return Fail($"Qty supplied for {line.Title} must be above 0 and no more than {line.QtyRequested}.");
            decisions.Add((line, input));
        }

        foreach (var (line, input) in decisions)
            await _sp.UpdateLine(line.Id,
                input.Supply ? "Supplied" : "Declined",
                input.Supply ? input.QtySupplied : 0,
                input.Comment ?? "");

        var supplied = decisions.Count(d => d.input.Supply);
        var status = supplied == lines.Count ? "Supplied"
                   : supplied == 0 ? "Declined" : "Partially Supplied";
        await _sp.UpdateRequestStatus(header.Id, status);

        var rows = string.Join("", decisions.Select(d =>
            $"<tr><td>{System.Net.WebUtility.HtmlEncode(d.line.Title)}</td><td>{d.line.QtyRequested}</td>" +
            $"<td>{(d.input.Supply ? d.input.QtySupplied : 0)}</td><td>{(d.input.Supply ? "Supplied" : "Declined")}</td></tr>"));

        await _sp.SendMail(header.RequestedBy, $"Colorant/OS request #{header.Id}: {status}",
            $"<p>The DSC has processed your request. Status: <b>{status}</b>.</p>" +
            $"<table border='1' cellpadding='4'><tr><th>Product</th><th>Requested</th><th>Supplied</th><th>Status</th></tr>{rows}</table>");

        TempData["Message"] = $"Request #{header.Id} marked as {status}.";
        return RedirectToAction(nameof(Index));

        IActionResult Fail(string msg)
        {
            TempData["Error"] = msg;
            return RedirectToAction(nameof(Fulfil), new { id = vm.RequestId });
        }
    }
}