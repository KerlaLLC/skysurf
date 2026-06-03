using Microsoft.Extensions.DependencyInjection;
using Shared.Theming;
using skysurf.App;
using skysurf.App.Navigation;
using Terminal.Gui;

var appHost = await AppHost.CreateAsync();

Application.Init();
AppTheme.Apply();

try
{
    var navigator = appHost.Services.GetRequiredService<AppNavigator>();
    var shell = new AppShell(navigator);
    navigator.AttachShell(shell);
    navigator.Start();
    Application.Run(shell);
}
finally
{
    Application.Shutdown();
    await appHost.DisposeAsync();
}
