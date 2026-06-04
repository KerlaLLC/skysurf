using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Shared.Authentication;
using Shared.Connections;
using toms.App;

// No subcommand → TUI
if (args.Length == 0)
{
    await TuiRunner.RunAsync();
    return 0;
}

// --- Root command ---
var rootCommand = new RootCommand("Toms: (To)ken (M)anager for (S)KY. Gets access and refresh tokens for your Blackbaud SKY API connections. Renews them automatically so they don't expire. ");

// list
var listCommand = new Command("list", "List all connections");
listCommand.SetHandler(async () =>
{
    var host = await AppHost.CreateAsync();
    var repo = host.Services.GetRequiredService<IConnectionRepository>();
    var connections = repo.List();

    if (connections.Count == 0)
    {
        Console.WriteLine("No connections.");
        return;
    }

    var col1 = Math.Max(4, connections.Max(c => c.Name.Length));
    var col2 = Math.Max(9, connections.Max(c => c.ClientId.Length));

    Console.WriteLine($"{"Name".PadRight(col1)}  {"Client ID".PadRight(col2)}  {"Token Expiry (UTC)",-22}  Status");
    Console.WriteLine(new string('-', col1 + col2 + 50));

    foreach (var c in connections)
    {
        var expired = c.RefreshTokenValidToUtc <= DateTime.UtcNow;
        var expiringSoon = !expired && c.RefreshTokenValidToUtc <= DateTime.UtcNow.AddMonths(6);
        var status = expired ? "EXPIRED" : expiringSoon ? "Expiring soon" : "OK";
        Console.WriteLine($"{c.Name.PadRight(col1)}  {c.ClientId.PadRight(col2)}  {c.RefreshTokenValidToUtc:u,-22}  {status}");
    }

    await host.DisposeAsync();
});
rootCommand.AddCommand(listCommand);

// add
var addCommand = new Command("add", "Add a new connection");
var addName = new Option<string>("--name", "Connection name") { IsRequired = true };
var addSubKey = new Option<string>("--subscription-key", "Blackbaud subscription key") { IsRequired = true };
var addClientId = new Option<string>("--client-id", "OAuth client ID") { IsRequired = true };
var addClientSecret = new Option<string>("--client-secret", "OAuth client secret") { IsRequired = true };
var addAuthCode = new Option<string>("--auth-code", "OAuth2 authorization code (exchange for refresh token)") { IsRequired = true };
addCommand.AddOption(addName);
addCommand.AddOption(addSubKey);
addCommand.AddOption(addClientId);
addCommand.AddOption(addClientSecret);
addCommand.AddOption(addAuthCode);
addCommand.SetHandler(async (name, subKey, clientId, clientSecret, authCode) =>
{
    Console.WriteLine($"Authorization URL: {SkyAuthenticationService.GetAuthorizationUrl(clientId)}");

    var host = await AppHost.CreateAsync();
    var repo = host.Services.GetRequiredService<IConnectionRepository>();
    var authService = host.Services.GetRequiredService<SkyAuthenticationService>();

    try
    {
        var (refreshToken, expiresAtUtc) = await authService.ExchangeAuthCodeAsync(clientId, clientSecret, authCode);

        repo.Add(new ConnectionRecord
        {
            Name = name,
            SubscriptionKey = subKey,
            ClientId = clientId,
            ClientSecret = clientSecret,
            RefreshToken = refreshToken,
            RefreshTokenValidToUtc = expiresAtUtc
        });

        Console.WriteLine($"Connection '{name}' added (token expires {expiresAtUtc:u}).");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        await host.DisposeAsync();
        Environment.Exit(1);
        return;
    }

    await host.DisposeAsync();
},
addName, addSubKey, addClientId, addClientSecret, addAuthCode);
rootCommand.AddCommand(addCommand);

// show
var showNameArg = new Argument<string>("name-or-id", "Connection name or ID");
var showCommand = new Command("show", "Show details of a connection") { showNameArg };
showCommand.SetHandler(async (nameOrId) =>
{
    var host = await AppHost.CreateAsync();
    var repo = host.Services.GetRequiredService<IConnectionRepository>();
    var connection = repo.GetByNameOrId(nameOrId);

    if (connection is null)
    {
        Console.Error.WriteLine($"Connection '{nameOrId}' not found.");
        await host.DisposeAsync();
        Environment.Exit(1);
        return;
    }

    var expired = connection.RefreshTokenValidToUtc <= DateTime.UtcNow;
    var expiringSoon = !expired && connection.RefreshTokenValidToUtc <= DateTime.UtcNow.AddMonths(6);

    Console.WriteLine($"Name:                    {connection.Name}");
    Console.WriteLine($"ID:                      {connection.Id}");
    Console.WriteLine($"Client ID:               {connection.ClientId}");
    Console.WriteLine($"Client Secret:           {connection.ClientSecret}");
    Console.WriteLine($"Subscription Key:        {connection.SubscriptionKey}");
    Console.WriteLine($"Refresh Token:           {connection.RefreshToken}");
    Console.WriteLine($"Token Expiry (UTC):      {connection.RefreshTokenValidToUtc:u}");
    Console.WriteLine($"Status:                  {(expired ? "EXPIRED" : expiringSoon ? "Expiring soon" : "OK")}");
    Console.WriteLine($"Created (UTC):           {connection.CreatedUtc:u}");

    await host.DisposeAsync();
},
showNameArg);
rootCommand.AddCommand(showCommand);

// delete
var deleteNameArg = new Argument<string>("name-or-id", "Connection name or ID");
var deleteYesOption = new Option<bool>("--yes", "Skip confirmation prompt");
var deleteCommand = new Command("delete", "Delete a connection") { deleteNameArg };
deleteCommand.AddOption(deleteYesOption);
deleteCommand.SetHandler(async (nameOrId, yes) =>
{
    var host = await AppHost.CreateAsync();
    var repo = host.Services.GetRequiredService<IConnectionRepository>();
    var connection = repo.GetByNameOrId(nameOrId);

    if (connection is null)
    {
        Console.Error.WriteLine($"Connection '{nameOrId}' not found.");
        await host.DisposeAsync();
        Environment.Exit(1);
        return;
    }

    if (!yes)
    {
        Console.Write($"Delete connection '{connection.Name}'? [y/N] ");
        var answer = Console.ReadLine();
        if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Cancelled.");
            await host.DisposeAsync();
            return;
        }
    }

    repo.Delete(connection.Id);
    Console.WriteLine($"Connection '{connection.Name}' deleted.");
    await host.DisposeAsync();
},
deleteNameArg, deleteYesOption);
rootCommand.AddCommand(deleteCommand);

// update
var updateNameArg = new Argument<string>("name-or-id", "Connection name or ID");
var updateCommand = new Command("update", "Update fields of a connection") { updateNameArg };
var updateName = new Option<string?>("--name");
var updateSubKey = new Option<string?>("--subscription-key");
var updateClientId = new Option<string?>("--client-id");
var updateClientSecret = new Option<string?>("--client-secret");
var updateRefreshToken = new Option<string?>("--refresh-token");
var updateExpires = new Option<DateTime?>("--refresh-token-expires");
updateCommand.AddOption(updateName);
updateCommand.AddOption(updateSubKey);
updateCommand.AddOption(updateClientId);
updateCommand.AddOption(updateClientSecret);
updateCommand.AddOption(updateRefreshToken);
updateCommand.AddOption(updateExpires);
updateCommand.SetHandler(async (nameOrId, name, subKey, clientId, clientSecret, refreshToken, expires) =>
{
    var host = await AppHost.CreateAsync();
    var repo = host.Services.GetRequiredService<IConnectionRepository>();
    var connection = repo.GetByNameOrId(nameOrId);

    if (connection is null)
    {
        Console.Error.WriteLine($"Connection '{nameOrId}' not found.");
        await host.DisposeAsync();
        Environment.Exit(1);
        return;
    }

    if (name is not null) connection.Name = name;
    if (subKey is not null) connection.SubscriptionKey = subKey;
    if (clientId is not null) connection.ClientId = clientId;
    if (clientSecret is not null) connection.ClientSecret = clientSecret;
    if (refreshToken is not null) connection.RefreshToken = refreshToken;
    if (expires.HasValue) connection.RefreshTokenValidToUtc = DateTime.SpecifyKind(expires.Value, DateTimeKind.Utc);

    repo.Update(connection);
    Console.WriteLine($"Connection '{connection.Name}' updated.");
    await host.DisposeAsync();
},
updateNameArg, updateName, updateSubKey, updateClientId, updateClientSecret, updateRefreshToken, updateExpires);
rootCommand.AddCommand(updateCommand);

// token
var tokenNameArg = new Argument<string>("name-or-id", "Connection name or ID");
var tokenRotateOption = new Option<bool>("--rotate", "Force refresh token rotation regardless of expiry");
var tokenCommand = new Command("token", "Get an access token for a connection") { tokenNameArg };
tokenCommand.AddOption(tokenRotateOption);
tokenCommand.SetHandler(async (nameOrId, rotate) =>
{
    var host = await AppHost.CreateAsync();
    var repo = host.Services.GetRequiredService<IConnectionRepository>();
    var authService = host.Services.GetRequiredService<SkyAuthenticationService>();
    var connection = repo.GetByNameOrId(nameOrId);

    if (connection is null)
    {
        Console.Error.WriteLine($"Connection '{nameOrId}' not found.");
        await host.DisposeAsync();
        Environment.Exit(1);
        return;
    }

    try
    {
        var accessToken = await authService.GetAccessTokenAsync(connection, forceRotate: rotate);
        Console.WriteLine(accessToken);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        await host.DisposeAsync();
        Environment.Exit(1);
        return;
    }

    await host.DisposeAsync();
},
tokenNameArg, tokenRotateOption);
rootCommand.AddCommand(tokenCommand);

// subscription-key
var subKeyNameArg = new Argument<string>("name-or-id", "Connection name or ID");
var subKeyCommand = new Command("subscription-key", "Get the subscription key for a connection") { subKeyNameArg };
subKeyCommand.SetHandler(async (nameOrId) =>
{
    var host = await AppHost.CreateAsync();
    var repo = host.Services.GetRequiredService<IConnectionRepository>();
    var connection = repo.GetByNameOrId(nameOrId);

    if (connection is null)
    {
        Console.Error.WriteLine($"Connection '{nameOrId}' not found.");
        await host.DisposeAsync();
        Environment.Exit(1);
        return;
    }

    Console.WriteLine(connection.SubscriptionKey);
    await host.DisposeAsync();
},
subKeyNameArg);
rootCommand.AddCommand(subKeyCommand);

// refresh  (always rotates; alias for token --rotate)
var refreshNameArg = new Argument<string>("name-or-id", "Connection name or ID");
var refreshCommand = new Command("refresh", "Rotate the refresh token and get a new access token") { refreshNameArg };
refreshCommand.SetHandler(async (nameOrId) =>
{
    var host = await AppHost.CreateAsync();
    var repo = host.Services.GetRequiredService<IConnectionRepository>();
    var authService = host.Services.GetRequiredService<SkyAuthenticationService>();
    var connection = repo.GetByNameOrId(nameOrId);

    if (connection is null)
    {
        Console.Error.WriteLine($"Connection '{nameOrId}' not found.");
        await host.DisposeAsync();
        Environment.Exit(1);
        return;
    }

    try
    {
        var accessToken = await authService.GetAccessTokenAsync(connection, forceRotate: true);
        Console.WriteLine(accessToken);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        await host.DisposeAsync();
        Environment.Exit(1);
        return;
    }

    await host.DisposeAsync();
},
refreshNameArg);
rootCommand.AddCommand(refreshCommand);

return await rootCommand.InvokeAsync(args);

