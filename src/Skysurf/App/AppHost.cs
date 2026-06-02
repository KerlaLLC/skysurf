using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Authentication;
using Shared.Connections;
using skysurf.App.Navigation;
using skysurf.Features.ParameterEntry;
using skysurf.Features.QueryExecution;
using skysurf.Features.QuerySelection;
using skysurf.Features.SavedQueries;
using skysurf.Features.SchemaCatalog;

namespace skysurf.App;

public sealed class AppHost : IAsyncDisposable
{
    private readonly IHost _host;

    private AppHost(IHost host)
    {
        _host = host;
    }

    public IServiceProvider Services => _host.Services;

    public static async Task<AppHost> CreateAsync()
    {
        var paths = AppPaths.Create();
        paths.EnsureDirectoriesExist();

        var host = Host
            .CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton(paths);
                services.AddHttpClient();

                services.AddSingleton<IConnectionRepository>(
                    _ => new ConnectionRepository(paths.ConnectionsFilePath));
                services.AddSingleton(
                    _ => new SavedQueryRepository(paths.SavedQueriesFilePath));

                services.AddSingleton<SkyApiSchemaService>();
                services.AddSingleton<QuerySearchService>();
                services.AddSingleton<SkyAuthenticationService>();
                services.AddSingleton<SkyQueryExecutor>();
                services.AddSingleton<WizardState>();
                services.AddSingleton<WizardNavigator>();
            })
            .Build();

        await host.StartAsync();
        return new AppHost(host);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
    }
}
