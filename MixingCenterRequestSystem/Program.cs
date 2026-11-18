using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using MixingCenterRequests.Infrastructure;
using MixingCenterRequests.Services;

var builder = WebApplication.CreateBuilder(args);

// Dev mode only runs in the Development environment AND when "DevMode": true
var devMode = builder.Environment.IsDevelopment()
              && builder.Configuration.GetValue<bool>("DevMode");

// ---------- Authentication + data ----------
if (devMode)
{
    // Fake sign-in (role picked on /Dev) + in-memory data
    builder.Services.AddAuthentication("Dev")
        .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>("Dev", null);

    builder.Services.AddSingleton<IDataService, FakeDataService>();
}
else
{
    var ad = builder.Configuration.GetSection("AzureAd");

    // Real sign-in with company Microsoft accounts
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(ad);

    // Make [Authorize(Roles = "...")] read Entra App Roles
    builder.Services.Configure<OpenIdConnectOptions>(
        OpenIdConnectDefaults.AuthenticationScheme,
        o => o.TokenValidationParameters.RoleClaimType = "roles");

    // App identity used to call Microsoft Graph (SharePoint lists + mail)
    builder.Services.AddSingleton<TokenCredential>(new ClientSecretCredential(
        ad["TenantId"], ad["ClientId"], ad["ClientSecret"]));

    builder.Services.AddHttpClient<IDataService, SharePointService>();
}

// ---------- MVC ----------
// Every page requires sign-in by default
builder.Services.AddControllersWithViews(o =>
        o.Filters.Add(new AuthorizeFilter(
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())))
    .AddMicrosoftIdentityUI();

builder.Services.AddRazorPages();   // needed for the sign-out pages

var app = builder.Build();

// ---------- Pipeline ----------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();