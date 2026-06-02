using Microsoft.Extensions.DependencyInjection;
using Shared.Authentication;
using Shared.Connections;
using Shared.Connections.Views;
using Terminal.Gui;
using toms.App;

namespace toms.App;

public static class TuiRunner
{
    public static async Task RunAsync()
    {
        var appHost = await AppHost.CreateAsync();

        Application.Init();

        try
        {
            var connectionRepository = appHost.Services.GetRequiredService<IConnectionRepository>();
            var authService = appHost.Services.GetRequiredService<SkyAuthenticationService>();

            var window = new Window("toms")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            var screen = new ConnectionManagerScreen(
                connectionRepository,
                authService,
                onGetToken: connection =>
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var token = await authService.GetAccessTokenAsync(connection);
                            Application.MainLoop.Invoke(() =>
                                MessageBox.Query("Access Token", token, "OK"));
                        }
                        catch (Exception ex)
                        {
                            Application.MainLoop.Invoke(() =>
                                MessageBox.ErrorQuery("Error", ex.Message, "OK"));
                        }
                    });
                },
                onRotateToken: connection =>
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var token = await authService.GetAccessTokenAsync(connection, forceRotate: true);
                            Application.MainLoop.Invoke(() =>
                                MessageBox.Query("Refresh Token Rotated", $"Access token:\n{token}", "OK"));
                        }
                        catch (Exception ex)
                        {
                            Application.MainLoop.Invoke(() =>
                                MessageBox.ErrorQuery("Error", ex.Message, "OK"));
                        }
                    });
                },
                showQuit: true)
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            window.Add(screen);
            Application.Run(window);
        }
        finally
        {
            Application.Shutdown();
            await appHost.DisposeAsync();
        }
    }
}
