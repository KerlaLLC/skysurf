using Microsoft.Extensions.DependencyInjection;
using skysurf.App;
using skysurf.App.Navigation;
using Terminal.Gui;

var appHost = await AppHost.CreateAsync();

Application.Init();

try
{
    var navigator = appHost.Services.GetRequiredService<WizardNavigator>();
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
