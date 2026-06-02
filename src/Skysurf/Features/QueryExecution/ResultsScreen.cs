using System.Data;
using Terminal.Gui;

namespace skysurf.Features.QueryExecution;

public sealed class ResultsScreen : FrameView
{
    private readonly QueryResult _result;
    private readonly Action<string>? _onStatus;
    private readonly TabView _tabs;
    private readonly TableView _resultsTableView;
    private readonly TableView _detailsTableView;
    private readonly TextView _detailsTextView;
    private readonly TextView _jsonView;
    private readonly Label _detailsSummaryLabel;

    public ResultsScreen(QueryResult result, Action onBack, Action onSave, Action<string>? onStatus = null)
    {
        _result = result;
        _onStatus = onStatus;

        Title = "Results";

        _tabs = new TabView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Height = Dim.Fill(5)
        };

        _resultsTableView = CreateTableView();
        _resultsTableView.SelectedCellChanged += _ => UpdateSelectionDetails();

        _detailsTableView = CreateTableView();
        _detailsTableView.CanFocus = true;

        _detailsSummaryLabel = new Label("Select a result cell to inspect its value.")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill()
        };

        _detailsTextView = new TextView
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            ReadOnly = true,
            WordWrap = false,
            Visible = true
        };

        _detailsTableView.X = 0;
        _detailsTableView.Y = 1;
        _detailsTableView.Width = Dim.Fill();
        _detailsTableView.Height = Dim.Fill(1);
        _detailsTableView.Visible = false;

        _jsonView = new TextView
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            ReadOnly = true,
            WordWrap = false,
            Text = result.Json
        };

        var tableTab = new TabView.Tab("Table", BuildTableTab());
        var jsonTab = new TabView.Tab("JSON", BuildJsonTab());

        _tabs.AddTab(tableTab, andSelect: true);
        _tabs.AddTab(jsonTab, andSelect: false);
        _tabs.SelectedTab = tableTab;
        _tabs.SelectedTabChanged += (_, _) => UpdateStatusHint();

        var backButton = new Button("Back")
        {
            X = 1,
            Y = Pos.AnchorEnd(3)
        };
        backButton.Clicked += onBack;

        var saveButton = new Button("Save query")
        {
            X = Pos.Right(backButton) + 2,
            Y = Pos.Top(backButton),
            IsDefault = true
        };
        saveButton.Clicked += onSave;

        var copyJsonButton = new Button("Copy JSON")
        {
            X = Pos.Right(saveButton) + 2,
            Y = Pos.Top(backButton)
        };
        copyJsonButton.Clicked += CopyJson;

        var copyCellButton = new Button("Copy cell")
        {
            X = Pos.Right(copyJsonButton) + 2,
            Y = Pos.Top(backButton)
        };
        copyCellButton.Clicked += CopySelectedCell;

        var copyRowButton = new Button("Copy row")
        {
            X = Pos.Right(copyCellButton) + 2,
            Y = Pos.Top(backButton)
        };
        copyRowButton.Clicked += CopySelectedRow;

        Add(_tabs, backButton, saveButton, copyJsonButton, copyCellButton, copyRowButton);

        if (_result.Table.Rows.Count > 0 && _result.Table.Columns.Count > 0)
        {
            _resultsTableView.SetSelection(0, 0, false);
        }

        UpdateSelectionDetails();
        UpdateStatusHint();
    }

    private FrameView BuildTableTab()
    {
        var tableTab = new FrameView
        {
            Title = "Table"
        };

        var resultsFrame = new FrameView
        {
            Title = "Rows",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(58)
        };

        if (_result.Table.Rows.Count == 0)
        {
            resultsFrame.Add(new Label(_result.Table.EmptyMessage)
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill(2)
            });
        }
        else
        {
            PopulateTableView(_resultsTableView, _result.Table);
            resultsFrame.Add(_resultsTableView);
        }

        var detailsFrame = new FrameView
        {
            Title = "Selected value",
            X = 0,
            Y = Pos.Bottom(resultsFrame),
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        detailsFrame.Add(_detailsSummaryLabel, _detailsTextView, _detailsTableView);

        tableTab.Add(resultsFrame, detailsFrame);
        return tableTab;
    }

    private FrameView BuildJsonTab()
    {
        var jsonTab = new FrameView
        {
            Title = "JSON"
        };

        jsonTab.Add(new Label("Use Copy JSON, or press Ctrl+A then Ctrl+C.")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill()
        });
        jsonTab.Add(_jsonView);

        return jsonTab;
    }

    private static TableView CreateTableView()
    {
        return new TableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            FullRowSelect = true,
            MultiSelect = false,
            MaxCellWidth = 36
        };
    }

    private static void PopulateTableView(TableView tableView, QueryResultTable table)
    {
        tableView.Table = BuildDataTable(table);
    }

    private static DataTable BuildDataTable(QueryResultTable table)
    {
        var dataTable = new DataTable();

        foreach (var column in table.Columns)
        {
            dataTable.Columns.Add(column.Name, typeof(string));
        }

        foreach (var row in table.Rows)
        {
            var values = row.Cells.Select(x => (object?)x.DisplayValue ?? string.Empty).ToArray();
            dataTable.Rows.Add(values);
        }

        return dataTable;
    }

    private void UpdateSelectionDetails()
    {
        if (_result.Table.Rows.Count == 0)
        {
            ShowDetailsText("No results were returned for this query.", _result.Table.EmptyMessage);
            return;
        }

        var selectedCell = GetSelectedCell();
        if (selectedCell is null)
        {
            ShowDetailsText("Select a result cell to inspect its value.", "No cell selected.");
            return;
        }

        var (rowIndex, cell) = selectedCell.Value;
        var summary = $"Row {rowIndex + 1}, {cell.ColumnName}: {cell.DisplayValue}";

        if (cell.Value is null)
        {
            ShowDetailsText(string.Empty, summary);
            return;
        }

        if (cell.HasNestedContent)
        {
            var nestedTable = QueryResultNormalizer.BuildTable(cell.Value.Value);
            if (nestedTable.Rows.Count > 0 && nestedTable.Columns.Count > 0)
            {
                PopulateTableView(_detailsTableView, nestedTable);
                _detailsSummaryLabel.Text = summary;
                _detailsTextView.Visible = false;
                _detailsTableView.Visible = true;
                return;
            }

            ShowDetailsText(QueryResultNormalizer.Serialize(cell.Value.Value, writeIndented: true), summary);
            return;
        }

        ShowDetailsText(cell.GetCopyText(), summary);
    }

    private (int RowIndex, QueryResultCell Cell)? GetSelectedCell()
    {
        if (_resultsTableView.Table is null)
        {
            return null;
        }

        var selectedRow = _resultsTableView.SelectedRow;
        var selectedColumn = _resultsTableView.SelectedColumn;
        if (selectedRow < 0 || selectedRow >= _result.Table.Rows.Count)
        {
            return null;
        }

        var row = _result.Table.Rows[selectedRow];
        if (selectedColumn < 0 || selectedColumn >= row.Cells.Count)
        {
            return null;
        }

        return (selectedRow, row.Cells[selectedColumn]);
    }

    private QueryResultRow? GetSelectedRow()
    {
        if (_resultsTableView.Table is null)
        {
            return null;
        }

        var selectedRow = _resultsTableView.SelectedRow;
        return selectedRow >= 0 && selectedRow < _result.Table.Rows.Count
            ? _result.Table.Rows[selectedRow]
            : null;
    }

    private void ShowDetailsText(string text, string summary)
    {
        _detailsSummaryLabel.Text = summary;
        _detailsTextView.Text = text;
        _detailsTextView.Visible = true;
        _detailsTableView.Visible = false;
    }

    private void CopyJson()
    {
        CopyToClipboard(_result.Json, $"Copied {_result.ItemCount} item(s) as JSON.");
    }

    private void CopySelectedCell()
    {
        var selectedCell = GetSelectedCell();
        if (selectedCell is null)
        {
            MessageBox.ErrorQuery("Nothing selected", "Select a table cell first.", "OK");
            return;
        }

        CopyToClipboard(selectedCell.Value.Cell.GetCopyText(), $"Copied '{selectedCell.Value.Cell.ColumnName}' to the clipboard.");
    }

    private void CopySelectedRow()
    {
        var selectedRow = GetSelectedRow();
        if (selectedRow is null)
        {
            MessageBox.ErrorQuery("Nothing selected", "Select a result row first.", "OK");
            return;
        }

        CopyToClipboard(selectedRow.Json, $"Copied row {selectedRow.Index + 1} as JSON.");
    }

    private void CopyToClipboard(string text, string statusMessage)
    {
        Clipboard.TrySetClipboardData(text);
        _onStatus?.Invoke(statusMessage);
    }

    private void UpdateStatusHint()
    {
        var tabTitle = _tabs.SelectedTab?.Text?.ToString() ?? "Results";
        _onStatus?.Invoke($"{_result.ItemCount} item(s) returned. {tabTitle} view ready. Save query, copy data, or go Back.");
    }
}

