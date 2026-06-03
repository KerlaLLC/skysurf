using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Shared.Views;
using Terminal.Gui;
using Terminal.Gui.Trees;
using skysurf.Features.QueryExecution;

namespace skysurf.Features.Main;

/// <summary>The <c>_Response</c> group box: the result data table, a tree of the selected
/// record, a search box that walks matches, and the export / open / copy buttons.</summary>
public sealed class ResponsePanel : GroupBox
{
    private readonly Action<string>? _onStatus;

    private readonly TextField _searchField;
    private readonly Label _hitLabel;
    private readonly TableView _tableView;
    private readonly TreeView _treeView;

    private QueryResult? _result;
    private List<(int Row, int Column)> _hits = [];
    private int _hitIndex = -1;

    public ResponsePanel(Action<string>? onStatus = null) : base("_Response")
    {
        _onStatus = onStatus;

        Add(new Label("Search:") { X = 1, Y = 1 });

        _searchField = new TextField(string.Empty)
        {
            X = 9,
            Y = 1,
            Width = 28
        };
        _searchField.TextChanged += _ => RecomputeHits();
        _searchField.KeyPress += OnSearchKey;

        _hitLabel = new Label("0/0 hits")
        {
            X = Pos.Right(_searchField) + 2,
            Y = 1,
            Width = Dim.Fill(2)
        };

        var tableFrame = new FrameView("Rows")
        {
            X = 1,
            Y = 2,
            Width = Dim.Percent(58),
            Height = Dim.Fill(4)
        };
        _tableView = new TableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            FullRowSelect = true,
            MultiSelect = false,
            MaxCellWidth = 36
        };
        _tableView.SelectedCellChanged += _ => UpdateTree();
        tableFrame.Add(_tableView);

        var treeFrame = new GroupBox("record _tree")
        {
            X = Pos.Right(tableFrame),
            Y = 2,
            Width = Dim.Fill(1),
            Height = Dim.Fill(4)
        };
        _treeView = new TreeView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        treeFrame.Add(_treeView);

        var exportButton = new Button("_Export") { X = 1, Y = Pos.AnchorEnd(2) };
        exportButton.Clicked += ExportJson;

        var openButton = new Button("_Open in editor") { X = Pos.Right(exportButton) + 1, Y = Pos.AnchorEnd(2) };
        openButton.Clicked += OpenInEditor;

        var copyJsonButton = new Button("Copy _JSON") { X = Pos.Right(openButton) + 1, Y = Pos.AnchorEnd(2) };
        copyJsonButton.Clicked += CopyJson;

        var copyCellButton = new Button("Copy c_ell") { X = Pos.Right(copyJsonButton) + 1, Y = Pos.AnchorEnd(2) };
        copyCellButton.Clicked += CopySelectedCell;

        var copyRowButton = new Button("Copy ro_w") { X = Pos.Right(copyCellButton) + 1, Y = Pos.AnchorEnd(2) };
        copyRowButton.Clicked += CopySelectedRow;

        Add(_searchField, _hitLabel, tableFrame, treeFrame,
            exportButton, openButton, copyJsonButton, copyCellButton, copyRowButton);
    }

    /// <summary>Populates the panel with a freshly executed query result.</summary>
    public void ShowResult(QueryResult result)
    {
        _result = result;
        _searchField.Text = string.Empty;
        _hits = [];
        _hitIndex = -1;
        _hitLabel.Text = "0/0 hits";

        _tableView.Table = BuildDataTable(result.Table);
        if (result.Table.Rows.Count > 0 && result.Table.Columns.Count > 0)
            _tableView.SetSelection(0, 0, false);

        UpdateTree();
    }

    private static DataTable BuildDataTable(QueryResultTable table)
    {
        var dataTable = new DataTable();
        foreach (var column in table.Columns)
            dataTable.Columns.Add(column.Name, typeof(string));

        foreach (var row in table.Rows)
        {
            var values = row.Cells.Select(x => (object?)x.DisplayValue ?? string.Empty).ToArray();
            dataTable.Rows.Add(values);
        }

        return dataTable;
    }

    private void UpdateTree()
    {
        _treeView.ClearObjects();
        var row = GetSelectedRow();
        if (row is null)
            return;

        try
        {
            using var document = JsonDocument.Parse(row.Json);
            var node = JsonTreeBuilder.Build(document.RootElement);
            _treeView.AddObject(node);
            _treeView.ExpandAll();
        }
        catch (JsonException)
        {
            _treeView.AddObject(new TreeNode(row.Json));
        }
    }

    // ── Table search ──────────────────────────────────────────────────────

    private void RecomputeHits()
    {
        _hits = [];
        _hitIndex = -1;

        var term = _searchField.Text?.ToString() ?? string.Empty;
        if (_result is not null && !string.IsNullOrEmpty(term))
        {
            var rows = _result.Table.Rows;
            for (var r = 0; r < rows.Count; r++)
            {
                var cells = rows[r].Cells;
                for (var c = 0; c < cells.Count; c++)
                {
                    if (cells[c].DisplayValue.Contains(term, StringComparison.OrdinalIgnoreCase))
                        _hits.Add((r, c));
                }
            }
        }

        UpdateHitLabel();
    }

    private void OnSearchKey(View.KeyEventEventArgs e)
    {
        if ((e.KeyEvent.Key & ~Key.ShiftMask) != Key.Enter)
            return;

        if (_hits.Count == 0)
        {
            e.Handled = true;
            return;
        }

        var backwards = e.KeyEvent.Key.HasFlag(Key.ShiftMask);
        _hitIndex = backwards
            ? (_hitIndex - 1 + _hits.Count) % _hits.Count
            : (_hitIndex + 1) % _hits.Count;

        var (row, column) = _hits[_hitIndex];
        _tableView.SetSelection(column, row, false);
        _tableView.EnsureSelectedCellIsVisible();
        UpdateHitLabel();
        e.Handled = true;
    }

    private void UpdateHitLabel()
    {
        _hitLabel.Text = _hits.Count == 0
            ? "0/0 hits"
            : $"{_hitIndex + 1}/{_hits.Count} hits";
    }

    // ── Selection helpers (ported from the old ResultsScreen) ───────────────

    private (int RowIndex, QueryResultCell Cell)? GetSelectedCell()
    {
        if (_result is null || _tableView.Table is null)
            return null;

        var selectedRow = _tableView.SelectedRow;
        var selectedColumn = _tableView.SelectedColumn;
        if (selectedRow < 0 || selectedRow >= _result.Table.Rows.Count)
            return null;

        var row = _result.Table.Rows[selectedRow];
        if (selectedColumn < 0 || selectedColumn >= row.Cells.Count)
            return null;

        return (selectedRow, row.Cells[selectedColumn]);
    }

    private QueryResultRow? GetSelectedRow()
    {
        if (_result is null || _tableView.Table is null)
            return null;

        var selectedRow = _tableView.SelectedRow;
        return selectedRow >= 0 && selectedRow < _result.Table.Rows.Count
            ? _result.Table.Rows[selectedRow]
            : null;
    }

    // ── Export / open / copy ────────────────────────────────────────────────

    private void ExportJson()
    {
        if (_result is null)
            return;

        var dialog = new SaveDialog("Export JSON", "Choose where to save the response.")
        {
            AllowedFileTypes = [".json"]
        };
        Application.Run(dialog);

        if (dialog.Canceled)
            return;

        var path = dialog.FilePath?.ToString();
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            File.WriteAllText(path, _result.Json);
            _onStatus?.Invoke($"Exported response to {path}.");
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Export failed", ex.Message, "OK");
        }
    }

    private void OpenInEditor()
    {
        if (_result is null)
            return;

        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"skysurf-{Guid.NewGuid():N}.json");
            File.WriteAllText(path, _result.Json);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            _onStatus?.Invoke("Opened response in the default editor.");
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Open failed", ex.Message, "OK");
        }
    }

    private void CopyJson()
    {
        if (_result is null)
            return;

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

        CopyToClipboard(
            selectedCell.Value.Cell.GetCopyText(),
            $"Copied '{selectedCell.Value.Cell.ColumnName}' to the clipboard.");
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
}
