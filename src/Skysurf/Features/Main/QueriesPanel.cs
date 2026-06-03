using Shared.Views;
using Terminal.Gui;
using skysurf.Features.QuerySelection;
using skysurf.Features.SavedQueries;

namespace skysurf.Features.Main;

/// <summary>The <c>_Queries</c> group box: a filter text input, the <c>_Blackbaud's</c> /
/// <c>_Mine</c> source checkboxes, and the list of matching endpoints and saved queries.</summary>
public sealed class QueriesPanel : GroupBox
{
    private readonly QuerySearchService _searchService;
    private readonly SavedQueryRepository _savedQueryRepository;

    private readonly TextField _filterField;
    private readonly CheckBox _blackbaudCheck;
    private readonly CheckBox _mineCheck;
    private readonly ListView _listView;

    private IReadOnlyList<SavedQueryRecord> _savedQueries = [];
    private List<SearchResult> _results = [];

    /// <summary>Raised whenever the highlighted result changes (or becomes null).</summary>
    public event Action<SearchResult?>? SelectionChanged;

    public QueriesPanel(QuerySearchService searchService, SavedQueryRepository savedQueryRepository)
        : base("_Queries")
    {
        _searchService = searchService;
        _savedQueryRepository = savedQueryRepository;

        _filterField = new TextField(string.Empty)
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2)
        };
        _filterField.TextChanged += _ => RefreshResults();

        _blackbaudCheck = new CheckBox("_Blackbaud's") { X = 1, Y = 3, Checked = true };
        _blackbaudCheck.Toggled += _ => RefreshResults();

        _mineCheck = new CheckBox("_Mine") { X = Pos.Right(_blackbaudCheck) + 3, Y = 3, Checked = true };
        _mineCheck.Toggled += _ => RefreshResults();

        _listView = new ListView
        {
            X = 1,
            Y = 5,
            Width = Dim.Fill(2),
            Height = Dim.Fill(1)
        };
        _listView.SelectedItemChanged += _ => RaiseSelectionChanged();
        _listView.KeyPress += e =>
        {
            if (e.KeyEvent.Key == Key.CursorUp && _listView.SelectedItem == 0)
            {
                _filterField.SetFocus();
                e.Handled = true;
            }
            else if (!e.KeyEvent.IsAlt && !e.KeyEvent.IsCtrl
                && e.KeyEvent.KeyValue >= 32 && e.KeyEvent.KeyValue < 127)
            {
                _filterField.Text = (_filterField.Text?.ToString() ?? string.Empty) + (char)e.KeyEvent.KeyValue;
                _filterField.CursorPosition = _filterField.Text?.ToString()?.Length ?? 0;
                _filterField.SetFocus();
                e.Handled = true;
            }
        };

        Add(_filterField, _blackbaudCheck, _mineCheck, _listView);

        Reload();
    }

    public SearchResult? Selected =>
        _results.Count > 0 && _listView.SelectedItem >= 0 && _listView.SelectedItem < _results.Count
            ? _results[_listView.SelectedItem]
            : null;

    /// <summary>Reloads saved queries from disk and re-runs the current filter.</summary>
    public void Reload()
    {
        _savedQueries = _savedQueryRepository.List();
        RefreshResults();
    }

    private void RefreshResults()
    {
        _results = _searchService.Search(
            _filterField.Text?.ToString() ?? string.Empty,
            _savedQueries,
            includeEndpoints: _blackbaudCheck.Checked,
            includeSaved: _mineCheck.Checked).ToList();

        _listView.SetSource(_results.Select(x => (object)x.DisplayText).ToList());
        RaiseSelectionChanged();
    }

    private void RaiseSelectionChanged()
    {
        SelectionChanged?.Invoke(Selected);
    }
}
