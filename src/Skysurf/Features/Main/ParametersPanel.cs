using Shared.Views;
using Terminal.Gui;
using skysurf.Features.ParameterEntry;
using skysurf.Features.SchemaCatalog;

namespace skysurf.Features.Main;

/// <summary>The <c>_Parameters</c> group box: a scrollable list of label + text-input rows
/// rebuilt whenever the selected endpoint changes. Required parameters are marked with a
/// trailing asterisk; the up/down arrows move focus between fields.</summary>
public sealed class ParametersPanel : GroupBox
{
    private const int FieldColumn = 20;
    private const int RowStride = 2;

    private readonly ScrollView _scroll;
    private readonly Label _placeholder;

    // Ordered for arrow-key navigation; the dictionary is for read-back by name.
    private readonly List<TextField> _orderedFields = [];
    private readonly Dictionary<string, TextField> _fields = new(StringComparer.OrdinalIgnoreCase);

    private SkyEndpoint? _endpoint;
    private int _contentHeight = 1;

    public ParametersPanel() : base("_Parameters")
    {
        _scroll = new ScrollView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2),
            Height = Dim.Fill(1),
            ShowHorizontalScrollIndicator = false,
            ShowVerticalScrollIndicator = true,
            KeepContentAlwaysInViewport = true
        };

        // Bounds.Width is 0 until the first layout pass, which would squash the Dim.Fill
        // fields. Keep the content width matched to the viewport once it is known.
        _scroll.LayoutComplete += _ =>
        {
            var width = Math.Max(1, _scroll.Bounds.Width - 1);
            if (_scroll.ContentSize.Width != width || _scroll.ContentSize.Height != _contentHeight)
                _scroll.ContentSize = new Size(width, _contentHeight);
        };

        _placeholder = new Label("Select an endpoint or saved query to edit its parameters.")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill()
        };

        _scroll.Add(_placeholder);
        Add(_scroll);
    }

    /// <summary>Rebuilds the parameter rows for <paramref name="endpoint"/>, pre-filling values
    /// from <paramref name="initialValues"/> (e.g. a saved query) when provided.</summary>
    public void SetEndpoint(SkyEndpoint? endpoint, IReadOnlyDictionary<string, string>? initialValues)
    {
        _endpoint = endpoint;
        _scroll.RemoveAll();
        _orderedFields.Clear();
        _fields.Clear();

        if (endpoint is null
            || (endpoint.RequiredParameters.Count == 0 && endpoint.OptionalParameters.Count == 0))
        {
            _placeholder.Text = endpoint is null
                ? "Select an endpoint or saved query to edit its parameters."
                : "This endpoint takes no parameters. Press Send to run it.";
            _scroll.Add(_placeholder);
            _contentHeight = 1;
            _scroll.ContentSize = new Size(Math.Max(1, _scroll.Bounds.Width - 1), _contentHeight);
            _scroll.ContentOffset = new Point(0, 0);
            return;
        }

        var currentY = 0;
        foreach (var prompt in endpoint.RequiredParameters)
            currentY = AddRow(prompt, required: true, initialValues, currentY);

        foreach (var prompt in endpoint.OptionalParameters)
            currentY = AddRow(prompt, required: false, initialValues, currentY);

        _contentHeight = currentY;
        _scroll.ContentSize = new Size(Math.Max(1, _scroll.Bounds.Width - 1), _contentHeight);
        _scroll.ContentOffset = new Point(0, 0);
    }

    private int AddRow(
        ParameterPrompt prompt,
        bool required,
        IReadOnlyDictionary<string, string>? initialValues,
        int currentY)
    {
        // The panel is narrow, so keep labels to "name *" (asterisk = required) and truncate
        // anything longer than the field column rather than overrunning the input.
        var asterisk = required ? " *" : string.Empty;
        Label label = new($"{prompt.Name}{asterisk}")
        {
            X = 0,
            Y = currentY,
            Width = FieldColumn - 1
        };

        var initial = initialValues?.TryGetValue(prompt.Name, out var value) == true ? value : string.Empty;
        var field = new TextField(initial)
        {
            X = FieldColumn,
            Y = currentY,
            Width = Dim.Fill(1)
        };

        var index = _orderedFields.Count;
        field.KeyPress += e => OnFieldKey(e, index);

        _scroll.Add(label, field);
        _orderedFields.Add(field);
        _fields[prompt.Name] = field;
        return currentY + RowStride;
    }

    private void OnFieldKey(View.KeyEventEventArgs e, int index)
    {
        switch (e.KeyEvent.Key)
        {
            case Key.CursorUp when index > 0:
                FocusField(index - 1);
                e.Handled = true;
                break;
            case Key.CursorDown when index < _orderedFields.Count - 1:
                FocusField(index + 1);
                e.Handled = true;
                break;
        }
    }

    private void FocusField(int index)
    {
        var field = _orderedFields[index];
        field.SetFocus();
        EnsureVisible(index * RowStride);
    }

    private void EnsureVisible(int rowY)
    {
        var top = -_scroll.ContentOffset.Y;
        var viewHeight = _scroll.Bounds.Height;
        if (viewHeight <= 0)
            return;

        // The setter takes the absolute value and negates it, so we pass positive offsets.
        if (rowY < top)
            _scroll.ContentOffset = new Point(0, rowY);
        else if (rowY >= top + viewHeight)
            _scroll.ContentOffset = new Point(0, rowY - viewHeight + 1);
    }

    /// <summary>Reads the field values, validating that every required parameter is filled.
    /// Returns <c>null</c> (after showing an error) when a required value is missing.</summary>
    public Dictionary<string, string>? CollectValues()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (_endpoint is null)
            return values;

        foreach (var prompt in _endpoint.RequiredParameters)
        {
            var text = _fields[prompt.Name].Text?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.ErrorQuery("Missing parameter", $"Enter a value for '{prompt.Name}'.", "OK");
                _fields[prompt.Name].SetFocus();
                return null;
            }

            values[prompt.Name] = text;
        }

        foreach (var prompt in _endpoint.OptionalParameters)
        {
            var text = _fields[prompt.Name].Text?.ToString()?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
                values[prompt.Name] = text;
        }

        return values;
    }
}
