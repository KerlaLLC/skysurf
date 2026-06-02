using Terminal.Gui;
using skysurf.Features.SavedQueries;

namespace skysurf.Features.QuerySelection;

public sealed class QuerySelectionScreen : FrameView
{
    private readonly QuerySearchService _querySearchService;
    private readonly IReadOnlyList<SavedQueryRecord> _savedQueries;
    private readonly Action _onBack;
    private readonly Action<SearchResult> _onSelect;
    private readonly TextField _searchField;
    private readonly ListView _listView;
    private List<SearchResult> _results = [];

    public QuerySelectionScreen(
        QuerySearchService querySearchService,
        IReadOnlyList<SavedQueryRecord> savedQueries,
        Action onBack,
        Action<SearchResult> onSelect)
    {
        _querySearchService = querySearchService;
        _savedQueries = savedQueries;
        _onBack = onBack;
        _onSelect = onSelect;

        Title = "Select endpoint or saved query";

        Add(new Label("Search")
        {
            X = 1,
            Y = 1
        });

        _searchField = new TextField(string.Empty)
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill(2)
        };
        _searchField.TextChanged += _ => RefreshResults();

        _listView = new ListView()
        {
            X = 1,
            Y = 4,
            Width = Dim.Fill(2),
            Height = Dim.Fill(5)
        };
        _listView.OpenSelectedItem += _ => ActivateSelection();
        _listView.KeyPress += e =>
        {
            if (e.KeyEvent.Key == Key.CursorUp && _listView.SelectedItem == 0)
            {
                _searchField.SetFocus();
                e.Handled = true;
            }
            else if (!e.KeyEvent.IsAlt && !e.KeyEvent.IsCtrl
                && e.KeyEvent.KeyValue >= 32 && e.KeyEvent.KeyValue < 127)
            {
                _searchField.Text = _searchField.Text.ToString() + (char)e.KeyEvent.KeyValue;
                _searchField.CursorPosition = _searchField.Text.ToString().Length;
                _searchField.SetFocus();
                e.Handled = true;
            }
        };

        var backButton = new Button("Back")
        {
            X = 1,
            Y = Pos.AnchorEnd(3)
        };
        backButton.Clicked += _onBack;

        var selectButton = new Button("Select")
        {
            X = Pos.Right(backButton) + 2,
            Y = Pos.Top(backButton),
            IsDefault = true
        };
        selectButton.Clicked += ActivateSelection;

        Add(_searchField, _listView, backButton, selectButton);

        RefreshResults();
    }

    private void RefreshResults()
    {
        _results = _querySearchService.Search(_searchField.Text?.ToString() ?? string.Empty, _savedQueries).ToList();
        _listView.SetSource(_results.Select(x => (object)x.DisplayText).ToList());
    }

    private void ActivateSelection()
    {
        if (_results.Count == 0 || _listView.SelectedItem < 0 || _listView.SelectedItem >= _results.Count)
        {
            return;
        }

        _onSelect(_results[_listView.SelectedItem]);
    }
}
