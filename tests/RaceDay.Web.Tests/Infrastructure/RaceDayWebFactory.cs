using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RaceDay.Web.Services;

namespace RaceDay.Web.Tests.Infrastructure;

/// <summary>
/// Hosts the MVC app with its API client pointed at an in-process copy of the real API,
/// so tests go through MVC, the API and back exactly as the deployed app does.
/// </summary>
public class RaceDayWebFactory : WebApplicationFactory<Program>
{
    public TestApiFactory Api { get; } = new();

    /// <summary>Replaces the connection to the API, e.g. with a handler that simulates the API being down.</summary>
    public Func<HttpMessageHandler>? ApiHandlerOverride { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("RaceDayApi:BaseUrl", "http://localhost/");
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<RaceDayApiClient>()
                .ConfigurePrimaryHttpMessageHandler(ApiHandlerOverride ?? (() => Api.Server.CreateHandler()));
        });
        builder.ConfigureLogging(logging => logging.ClearProviders());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Api.Dispose();
        base.Dispose(disposing);
    }
}
