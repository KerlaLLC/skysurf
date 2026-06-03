using Shared.Authentication;
using Shared.Connections;
using Shared.Connections.Views;
using Terminal.Gui;
using skysurf.Features.Main;
using skysurf.Features.QueryExecution;
using skysurf.Features.QuerySelection;
using skysurf.Features.SavedQueries;
using skysurf.Features.SchemaCatalog;

namespace skysurf.App.Navigation;

/// <summary>Drives the two-screen app: loads SKY schemas, then shows either the main screen
/// (using the default / most-recently-used connection) or the connections screen on first run.
/// Ctrl+O returns to the connections screen at any time.</summary>
public sealed class AppNavigator
{
    private readonly SessionState _session;
    private readonly SkyApiSchemaService _schemaService;
    private readonly IConnectionRepository _connectionRepository;
    private readonly SkyAuthenticationService _authService;
    private readonly SavedQueryRepository _savedQueryRepository;
    private readonly QuerySearchService _querySearchService;
    private readonly SkyQueryExecutor _queryExecutor;
    private readonly QueryResultCacheRepository _resultCache;

    private AppShell? _shell;
    private MainScreen? _mainScreen;

    public AppNavigator(
        SessionState session,
        SkyApiSchemaService schemaService,
        IConnectionRepository connectionRepository,
        SkyAuthenticationService authService,
        SavedQueryRepository savedQueryRepository,
        QuerySearchService querySearchService,
        SkyQueryExecutor queryExecutor,
        QueryResultCacheRepository resultCache)
    {
        _session = session;
        _schemaService = schemaService;
        _connectionRepository = connectionRepository;
        _authService = authService;
        _savedQueryRepository = savedQueryRepository;
        _querySearchService = querySearchService;
        _queryExecutor = queryExecutor;
        _resultCache = resultCache;
    }

    public void AttachShell(AppShell shell)
    {
        _shell = shell;
    }

    public void Start()
    {
        EnsureShell();

        if (_schemaService.TryLoadFromCache())
        {
            ShowStartupScreen("Loaded cached SKY schemas. Refreshing in background.");
            _ = Task.Run(RefreshSchemasInBackgroundAsync);
            return;
        }

        var loadingScreen = new SchemaLoadingScreen();
        _shell!.ShowScreen(loadingScreen, "Loading SKY schemas", "Fetching schemas from Blackbaud");

        _ = Task.Run(async () =>
        {
            try
            {
                var progress = new Progress<SchemaRefreshProgress>(value =>
                    Application.MainLoop.Invoke(() => loadingScreen.UpdateProgress(value)));

                await _schemaService.RefreshAsync(progress);
                Application.MainLoop.Invoke(() => ShowStartupScreen("SKY schemas loaded."));
            }
            catch (Exception ex)
            {
                Application.MainLoop.Invoke(() => loadingScreen.ShowError(ex.Message));
            }
        });
    }

    private async Task RefreshSchemasInBackgroundAsync()
    {
        try
        {
            await _schemaService.RefreshAsync();
            Application.MainLoop.Invoke(() => _shell?.SetStatus("SKY schemas refreshed."));
        }
        catch
        {
            Application.MainLoop.Invoke(() =>
                _shell?.SetStatus("Using cached SKY schemas. Background refresh failed."));
        }
    }

    /// <summary>After schemas are ready, go straight to the main screen if a connection can be
    /// resolved, otherwise show the connections screen first.</summary>
    private void ShowStartupScreen(string status)
    {
        var connection = ResolveStartupConnection();
        if (connection is not null)
        {
            _session.ActiveConnection = connection;
            ShowMain(status);
        }
        else
        {
            ShowConnections();
        }
    }

    private ConnectionRecord? ResolveStartupConnection()
    {
        var all = _connectionRepository.List();
        return all.FirstOrDefault(x => x.IsDefault)
            ?? all.Where(x => x.LastUsedUtc > DateTime.MinValue)
                  .OrderByDescending(x => x.LastUsedUtc)
                  .FirstOrDefault();
    }

    public void ShowConnections()
    {
        EnsureShell();

        var screen = new ConnectionManagerScreen(
            _connectionRepository,
            _authService,
            onSelect: connection =>
            {
                _session.ActiveConnection = connection;
                _connectionRepository.TouchLastUsed(connection.Id);
                ShowMain("Connection selected.");
            },
            showDefaultOption: true);

        _shell!.ShowScreen(screen, "Connections", "Add/select a connection, then Select. Ctrl+Q to quit.");
    }

    private void ShowMain(string status)
    {
        EnsureShell();

        _mainScreen ??= new MainScreen(
            _schemaService,
            _querySearchService,
            _savedQueryRepository,
            _queryExecutor,
            _connectionRepository,
            _session,
            _resultCache,
            onStatus: text => _shell?.SetStatus(text));

        _mainScreen.Refresh();

        var name = _session.ActiveConnection?.Name ?? "None";
        _shell!.ShowScreen(_mainScreen, "skysurf", $"Ctrl+O Connections | Connection: {name} | Ctrl+Q Quit");
        if (!string.IsNullOrWhiteSpace(status))
            _shell!.SetStatus(status);
    }

    private void EnsureShell()
    {
        if (_shell is null)
            throw new InvalidOperationException("AppShell has not been attached.");
    }
}
