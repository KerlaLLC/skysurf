using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Authentication;
using Shared.Connections;

namespace toms.App;

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
                services.AddSingleton<SkyAuthenticationService>();
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
