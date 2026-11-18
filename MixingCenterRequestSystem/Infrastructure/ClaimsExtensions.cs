using System.Security.Claims;

namespace MixingCenterRequests.Infrastructure;

public static class ClaimsExtensions
{
    public static string Email(this ClaimsPrincipal user) =>
        (user.FindFirst("preferred_username")?.Value ?? "").ToLowerInvariant();
}