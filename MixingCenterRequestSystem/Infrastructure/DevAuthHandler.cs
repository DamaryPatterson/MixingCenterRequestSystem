using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MixingCenterRequests.Infrastructure;

public class DevAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public DevAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> o, ILoggerFactory l, UrlEncoder e)
        : base(o, l, e) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Cookies["devrole"] ?? "MixingCenter";
        var email = role switch
        {
            "DSC" => "dsc.tester@test.local",
            "MixingCenter2" => "mc2.tester@test.local",   // second requester, to test privacy
            _ => "mc.tester@test.local"
        };

        var claims = new List<Claim>
        {
            new("name", $"Test User ({role})"),
            new("preferred_username", email)
        };
        if (role is "MixingCenter" or "MixingCenter2") claims.Add(new("roles", "MixingCenter"));
        if (role == "DSC") claims.Add(new("roles", "DSC"));
        // role "None" = no roles, to test the "no access" message

        var identity = new ClaimsIdentity(claims, "Dev", "name", "roles");
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), "Dev")));
    }
}