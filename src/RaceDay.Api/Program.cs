using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Swagger;

var builder = WebApplication.CreateBuilder(args);

// ---------- Database (EF Core, code-first) ----------
// SQL Server by default. "InMemory" is used by the automated tests and CI, which have no SQL Server.
var provider = builder.Configuration["Database:Provider"] ?? "SqlServer";
builder.Services.AddDbContext<RaceDayDbContext>(options =>
{
    if (provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
        options.UseInMemoryDatabase(builder.Configuration["Database:InMemoryName"] ?? "RaceDayDB");
    else
        options.UseSqlServer(builder.Configuration.GetConnectionString("RaceDayDB"));
});

// ---------- Password hashing (PBKDF2 + salt, from ASP.NET Core Identity) ----------
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// ---------- Session management ----------
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;            // not readable from JavaScript
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

// ---------- Authentication & role-based authorisation from the session ----------
builder.Services
    .AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ---------- Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description =
            "RESTful API for the RaceDay race-event management system.\n\n" +
            "**How to test protected endpoints:** call `POST /api/auth/login` first. The server starts a session and " +
            "your browser stores the session cookie, so every request made from this page afterwards is authenticated " +
            "as that user. Call `POST /api/auth/logout` to end the session or switch users.\n\n" +
            "Demo accounts (password `Password123!`): Organiser `thabo.mokoena@raceday.co.za`, " +
            "Participant `sarah.botha@example.com`."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));

    options.AddSecurityDefinition("SessionCookie", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Name = ".RaceDay.Session",
        Description = "Session cookie set by POST /api/auth/login. Browsers send it automatically."
    });
    options.OperationFilter<AuthorizationOperationFilter>();

    // Show the endpoint groups in the same order as the Part 1 endpoint plan.
    var planOrder = new[] { "Auth", "Users", "Events", "Categories", "Enrolments", "Results" };
    options.OrderActionsBy(api =>
    {
        var controller = api.ActionDescriptor.RouteValues["controller"] ?? string.Empty;
        var index = Array.IndexOf(planOrder, controller);
        return $"{(index < 0 ? 99 : index):D2}";
    });
});

var app = builder.Build();

await DbSeeder.InitialiseAsync(app.Services, app.Configuration);

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
    options.DocumentTitle = "RaceDay API";
});

app.UseHttpsRedirection();

// Order matters: the session must be loaded before authentication reads it.
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapControllers();

app.Run();

/// <summary>Exposed so the test project can host the API with WebApplicationFactory.</summary>
public partial class Program { }
