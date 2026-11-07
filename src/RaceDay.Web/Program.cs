using System.Globalization;
using Microsoft.AspNetCore.Localization;
using RaceDay.Web.Filters;
using RaceDay.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Session management (keeps the logged-in user and their API session between requests) ----------
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Web.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UserSession>();

// ---------- RaceDay API client ----------
// UseCookies = false: the shared HttpClient must never keep cookies, otherwise users would share API sessions.
// ApiSessionCookieHandler adds the current user's own API session cookie to each request instead.
var apiBaseUrl = builder.Configuration["RaceDayApi:BaseUrl"]
    ?? throw new InvalidOperationException("RaceDayApi:BaseUrl is not configured.");
builder.Services.AddTransient<ApiSessionCookieHandler>();
builder.Services.AddHttpClient<RaceDayApiClient>(client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl.EndsWith('/') ? apiBaseUrl : apiBaseUrl + "/");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false })
    .AddHttpMessageHandler<ApiSessionCookieHandler>();

builder.Services.AddControllersWithViews(options => options.Filters.Add<ApiExceptionFilter>());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");

// Fixed culture so "42.2" in a number field always means 42.2 km, whatever the server's regional settings are.
var formCulture = CultureInfo.GetCultureInfo("en-GB");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(formCulture),
    SupportedCultures = new[] { formCulture },
    SupportedUICultures = new[] { formCulture }
});

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

/// <summary>Exposed so the test project can host the MVC app with WebApplicationFactory.</summary>
public partial class Program { }
