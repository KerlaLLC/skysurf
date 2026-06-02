using System.Text.Json;
using Shared.Authentication;
using Terminal.Gui;
using Shared.Connections;
using Shared.Connections.Views;
using skysurf.App;
using skysurf.Features.ParameterEntry;
using skysurf.Features.QueryExecution;
using skysurf.Features.QuerySelection;
using skysurf.Features.SavedQueries;
using skysurf.Features.SchemaCatalog;

namespace skysurf.App.Navigation;

public sealed class WizardNavigator
{
    private readonly WizardState _state;
    private readonly SkyApiSchemaService _schemaService;
    private readonly IConnectionRepository _connectionRepository;
    private readonly SkyAuthenticationService _authService;
    private readonly SavedQueryRepository _savedQueryRepository;
    private readonly QuerySearchService _querySearchService;
    private readonly SkyQueryExecutor _queryExecutor;
    private AppShell? _shell;

    public WizardNavigator(
        WizardState state,
        SkyApiSchemaService schemaService,
        IConnectionRepository connectionRepository,
        SkyAuthenticationService authService,
        SavedQueryRepository savedQueryRepository,
        QuerySearchService querySearchService,
        SkyQueryExecutor queryExecutor)
    {
        _state = state;
        _schemaService = schemaService;
        _connectionRepository = connectionRepository;
        _authService = authService;
        _savedQueryRepository = savedQueryRepository;
        _querySearchService = querySearchService;
        _queryExecutor = queryExecutor;
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
            ShowConnectionPicker("Loaded cached SKY schemas. Refreshing in background.");
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
                Application.MainLoop.Invoke(() => ShowConnectionPicker("SKY schemas loaded."));
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
            Application.MainLoop.Invoke(() =>
                _shell?.SetStatus("SKY schemas refreshed."));
        }
        catch
        {
            Application.MainLoop.Invoke(() =>
                _shell?.SetStatus("Using cached SKY schemas. Background refresh failed."));
        }
    }

    private void ShowConnectionPicker(string status)
    {
        EnsureShell();

        var screen = new ConnectionManagerScreen(
            _connectionRepository,
            _authService,
            onSelect: connection =>
            {
                _state.SelectedConnection = connection;
                ShowQuerySelection();
            });

        _shell!.ShowScreen(screen, "Connection", $"{status}  Add/Delete connections, then Select.");
    }

    private void ShowQuerySelection()
    {
        EnsureShell();

        var savedQueries = _savedQueryRepository.List();
        var screen = new QuerySelectionScreen(
            _querySearchService,
            savedQueries,
            onBack: () => ShowConnectionPicker("Choose a SKY API connection."),
            onSelect: result =>
            {
                if (result.SavedQuery is not null)
                {
                    var endpoint = _schemaService.FindEndpoint(
                        result.SavedQuery.ApiId,
                        result.SavedQuery.EndpointPath,
                        result.SavedQuery.HttpMethod);

                    if (endpoint is null)
                    {
                        MessageBox.ErrorQuery(
                            "Saved query unavailable",
                            "That saved query points to an endpoint that is no longer in the loaded SKY schema catalog.",
                            "OK");
                        return;
                    }

                    ShowParameterEntry(endpoint, result.SavedQuery.GetParameters());
                    return;
                }

                if (result.Endpoint is not null)
                {
                    ShowParameterEntry(result.Endpoint, null);
                }
            });

        _shell!.ShowScreen(
            screen,
            "Query",
            $"Connection: {_state.SelectedConnection?.Name ?? "None"}  Type to search. Enter/Select to continue.");
    }

    private void ShowParameterEntry(SkyEndpoint endpoint, IReadOnlyDictionary<string, string>? initialValues)
    {
        EnsureShell();
        _state.SelectedEndpoint = endpoint;

        if (endpoint.RequiredParameters.Count == 0 && endpoint.OptionalParameters.Count == 0)
        {
            ExecuteQuery(endpoint, new ParameterValueSet
            {
                Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            });
            return;
        }

        var screen = new ParameterEntryScreen(
            endpoint,
            initialValues,
            onBack: ShowQuerySelection,
            onRun: values => ExecuteQuery(endpoint, values));

        _shell!.ShowScreen(
            screen,
            "Parameters",
            $"{endpoint.ApiName} {endpoint.Path}  Fill required fields, then Run.");
    }

    private void ExecuteQuery(SkyEndpoint endpoint, ParameterValueSet values)
    {
        EnsureShell();

        if (_state.SelectedConnection is null)
        {
            MessageBox.ErrorQuery("No connection", "Select a SKY API connection first.", "OK");
            ShowConnectionPicker("Choose a SKY API connection.");
            return;
        }

        var loading = new QueryLoadingScreen(endpoint);
        _shell!.ShowScreen(loading, "Running query", $"{endpoint.ApiName} {endpoint.Path}");

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _queryExecutor.ExecuteAsync(
                    _state.SelectedConnection,
                    endpoint,
                    values.Values);

                Application.MainLoop.Invoke(() =>
                    ShowResults(endpoint, values.Values, result));
            }
            catch (Exception ex)
            {
                Application.MainLoop.Invoke(() =>
                {
                    MessageBox.ErrorQuery("Query failed", ex.Message, "OK");
                    if (endpoint.RequiredParameters.Count == 0 && endpoint.OptionalParameters.Count == 0)
                        ShowQuerySelection();
                    else
                        ShowParameterEntry(endpoint, values.Values);
                });
            }
        });
    }

    private void ShowResults(
        SkyEndpoint endpoint,
        IReadOnlyDictionary<string, string> values,
        QueryResult result)
    {
        EnsureShell();

        var screen = new ResultsScreen(
            result,
            onBack: () =>
            {
                if (endpoint.RequiredParameters.Count == 0 && endpoint.OptionalParameters.Count == 0)
                    ShowQuerySelection();
                else
                    ShowParameterEntry(endpoint, values);
            },
            onSave: () =>
            {
                var name = SaveQueryDialog.Show();
                if (string.IsNullOrWhiteSpace(name))
                {
                    return;
                }

                var savedQuery = new SavedQueryRecord
                {
                    Name = name.Trim(),
                    ApiId = endpoint.ApiId,
                    ApiName = endpoint.ApiName,
                    EndpointPath = endpoint.Path,
                    HttpMethod = endpoint.HttpMethod,
                    SchemaModelName = endpoint.SchemaModelName,
                    ParametersJson = JsonSerializer.Serialize(values)
                };

                _savedQueryRepository.Add(savedQuery);
                _shell!.SetStatus($"Saved query '{savedQuery.Name}'.");
            },
            onStatus: status => _shell!.SetStatus(status));

        _shell!.ShowScreen(
            screen,
            "Results",
            $"{result.ItemCount} item(s) returned. Save query or go Back.");
    }

    private void EnsureShell()
    {
        if (_shell is null)
        {
            throw new InvalidOperationException("AppShell has not been attached.");
        }
    }
}
