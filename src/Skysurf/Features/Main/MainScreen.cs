using System.Text.Json;
using Shared.Connections;
using Terminal.Gui;
using skysurf.App.Navigation;
using skysurf.Features.QueryExecution;
using skysurf.Features.QuerySelection;
using skysurf.Features.SavedQueries;
using skysurf.Features.SchemaCatalog;

namespace skysurf.Features.Main;

/// <summary>The single main screen: query search + parameters + save/send actions on the left,
/// the response table and record tree on the right.</summary>
public sealed class MainScreen : View
{
    private readonly SkyApiSchemaService _schemaService;
    private readonly SavedQueryRepository _savedQueryRepository;
    private readonly SkyQueryExecutor _queryExecutor;
    private readonly IConnectionRepository _connectionRepository;
    private readonly SessionState _session;
    private readonly QueryResultCacheRepository _resultCache;
    private readonly Action<string>? _onStatus;

    private readonly QueriesPanel _queries;
    private readonly ParametersPanel _parameters;
    private readonly ResponsePanel _response;
    private readonly Button _deleteButton;
    private readonly Button _sendButton;

    public MainScreen(
        SkyApiSchemaService schemaService,
        QuerySearchService querySearchService,
        SavedQueryRepository savedQueryRepository,
        SkyQueryExecutor queryExecutor,
        IConnectionRepository connectionRepository,
        SessionState session,
        QueryResultCacheRepository resultCache,
        Action<string>? onStatus = null)
    {
        _schemaService = schemaService;
        _savedQueryRepository = savedQueryRepository;
        _queryExecutor = queryExecutor;
        _connectionRepository = connectionRepository;
        _session = session;
        _resultCache = resultCache;
        _onStatus = onStatus;

        _queries = new QueriesPanel(querySearchService, savedQueryRepository)
        {
            X = 0,
            Y = 0,
            Width = Dim.Percent(45),
            Height = Dim.Percent(55)
        };
        _queries.SelectionChanged += OnQuerySelectionChanged;

        _parameters = new ParametersPanel
        {
            X = 0,
            Y = Pos.Bottom(_queries),
            Width = Dim.Percent(45),
            Height = Dim.Fill(3)
        };

        var saveButton = new Button("_Save") { X = 1, Y = Pos.AnchorEnd(2) };
        saveButton.Clicked += OnSave;

        var saveAsButton = new Button("Save _As") { X = Pos.Right(saveButton) + 1, Y = Pos.AnchorEnd(2) };
        saveAsButton.Clicked += OnSaveAs;

        _deleteButton = new Button("De_lete") { X = Pos.Right(saveAsButton) + 1, Y = Pos.AnchorEnd(2), Enabled = false };
        _deleteButton.Clicked += OnDelete;

        _sendButton = new Button("Sen_d") { X = Pos.Right(_deleteButton) + 1, Y = Pos.AnchorEnd(2), IsDefault = true };
        _sendButton.Clicked += OnSend;

        _response = new ResponsePanel(onStatus)
        {
            X = Pos.Right(_queries),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        Add(_queries, _parameters, saveButton, saveAsButton, _deleteButton, _sendButton, _response);

        OnQuerySelectionChanged(_queries.Selected);
    }

    /// <summary>Reloads the saved-query list (e.g. after the connection screen returns).</summary>
    public void Refresh() => _queries.Reload();

    private void OnQuerySelectionChanged(SearchResult? selection)
    {
        _deleteButton.Enabled = selection?.SavedQuery is not null;

        if (selection is null)
        {
            _parameters.SetEndpoint(null, null);
            _response.ClearResult();
            return;
        }

        SkyEndpoint? endpoint;
        if (selection.SavedQuery is not null)
        {
            endpoint = ResolveEndpoint(selection, showErrors: false);
            _parameters.SetEndpoint(endpoint, endpoint is null ? null : selection.SavedQuery.GetParameters());
        }
        else
        {
            endpoint = selection.Endpoint;
            _parameters.SetEndpoint(endpoint, null);
        }

        RestoreCachedResult(endpoint);
    }

    /// <summary>If this endpoint + the parameters currently in the panel have been run before on the
    /// active connection, re-display that cached result with its refresh time; otherwise clear the
    /// Response box. Results are scoped per connection.</summary>
    private void RestoreCachedResult(SkyEndpoint? endpoint)
    {
        var connection = _session.ActiveConnection;
        if (endpoint is null || connection is null)
        {
            _response.ClearResult();
            return;
        }

        var key = QueryResultCacheRepository.BuildKey(connection.Id, endpoint, _parameters.PeekValues());
        var cached = _resultCache.TryGet(key);
        if (cached is not null)
            _response.ShowResult(cached.Result, cached.RefreshedUtc);
        else
            _response.ClearResult();
    }

    /// <summary>Resolves the concrete endpoint behind a selection (saved queries point at one
    /// by API id + path + method). Returns null if it is no longer in the loaded catalog.</summary>
    private SkyEndpoint? ResolveEndpoint(SearchResult selection, bool showErrors)
    {
        if (selection.Endpoint is not null)
            return selection.Endpoint;

        if (selection.SavedQuery is { } saved)
        {
            var endpoint = _schemaService.FindEndpoint(saved.ApiId, saved.EndpointPath, saved.HttpMethod);
            if (endpoint is null && showErrors)
            {
                MessageBox.ErrorQuery(
                    "Saved query unavailable",
                    "That saved query points to an endpoint that is no longer in the loaded SKY schema catalog.",
                    "OK");
            }

            return endpoint;
        }

        return null;
    }

    private void OnSave()
    {
        var selection = _queries.Selected;
        if (selection is null)
        {
            MessageBox.ErrorQuery("Nothing selected", "Select an endpoint or saved query first.", "OK");
            return;
        }

        var endpoint = ResolveEndpoint(selection, showErrors: true);
        if (endpoint is null)
            return;

        var values = _parameters.CollectValues();
        if (values is null)
            return;

        // Saved query selected → overwrite in place, no modal.
        if (selection.SavedQuery is { } existing)
        {
            var updated = new SavedQueryRecord
            {
                Id = existing.Id,
                Name = existing.Name,
                CreatedUtc = existing.CreatedUtc,
                ApiId = endpoint.ApiId,
                ApiName = endpoint.ApiName,
                EndpointPath = endpoint.Path,
                HttpMethod = endpoint.HttpMethod,
                SchemaModelName = endpoint.SchemaModelName,
                ParametersJson = JsonSerializer.Serialize(values)
            };

            _savedQueryRepository.Update(updated);
            _queries.Reload();
            _onStatus?.Invoke($"Updated saved query '{updated.Name}'.");
            return;
        }

        // Endpoint selected → prompt for a name.
        SaveAsNew(endpoint, values);
    }

    private void OnSaveAs()
    {
        var selection = _queries.Selected;
        if (selection is null)
        {
            MessageBox.ErrorQuery("Nothing selected", "Select an endpoint or saved query first.", "OK");
            return;
        }

        var endpoint = ResolveEndpoint(selection, showErrors: true);
        if (endpoint is null)
            return;

        var values = _parameters.CollectValues();
        if (values is null)
            return;

        SaveAsNew(endpoint, values);
    }

    private void SaveAsNew(SkyEndpoint endpoint, Dictionary<string, string> values)
    {
        var name = SaveQueryDialog.Show();
        if (string.IsNullOrWhiteSpace(name))
            return;

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
        _queries.Reload();
        _onStatus?.Invoke($"Saved query '{savedQuery.Name}'.");
    }

    private void OnDelete()
    {
        var selection = _queries.Selected;
        if (selection?.SavedQuery is not { } saved)
            return;

        var answer = MessageBox.Query("Delete saved query", $"Delete '{saved.Name}'?", "No", "Yes");
        if (answer != 1)
            return;

        _savedQueryRepository.Delete(saved.Id);
        _queries.Reload();
        _onStatus?.Invoke($"Deleted saved query '{saved.Name}'.");
    }

    private void OnSend()
    {
        var selection = _queries.Selected;
        if (selection is null)
        {
            MessageBox.ErrorQuery("Nothing selected", "Select an endpoint or saved query first.", "OK");
            return;
        }

        var endpoint = ResolveEndpoint(selection, showErrors: true);
        if (endpoint is null)
            return;

        var values = _parameters.CollectValues();
        if (values is null)
            return;

        var connection = _session.ActiveConnection;
        if (connection is null)
        {
            MessageBox.ErrorQuery("No connection", "Choose a SKY API connection first (Ctrl+O).", "OK");
            return;
        }

        _sendButton.Enabled = false;
        _onStatus?.Invoke($"Running {endpoint.ApiName} {endpoint.Path}…");

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _queryExecutor.ExecuteAsync(connection, endpoint, values);
                _connectionRepository.TouchLastUsed(connection.Id);
                var refreshedUtc = DateTime.UtcNow;
                _resultCache.Save(QueryResultCacheRepository.BuildKey(connection.Id, endpoint, values), result, refreshedUtc);
                Application.MainLoop.Invoke(() =>
                {
                    _response.ShowResult(result, refreshedUtc);
                    _sendButton.Enabled = true;
                    _onStatus?.Invoke($"{result.ItemCount} item(s) returned.");
                });
            }
            catch (Exception ex)
            {
                Application.MainLoop.Invoke(() =>
                {
                    _sendButton.Enabled = true;
                    MessageBox.ErrorQuery("Query failed", ex.Message, "OK");
                    _onStatus?.Invoke("Query failed.");
                });
            }
        });
    }
}
