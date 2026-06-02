using Terminal.Gui;
using skysurf.Features.SchemaCatalog;

namespace skysurf.Features.ParameterEntry;

public sealed class ParameterEntryScreen : FrameView
{
    private readonly SkyEndpoint _endpoint;
    private readonly Action _onBack;
    private readonly Action<ParameterValueSet> _onRun;
    private readonly Dictionary<string, TextField> _fields = new(StringComparer.OrdinalIgnoreCase);

    public ParameterEntryScreen(
        SkyEndpoint endpoint,
        IReadOnlyDictionary<string, string>? initialValues,
        Action onBack,
        Action<ParameterValueSet> onRun)
    {
        _endpoint = endpoint;
        _onBack = onBack;
        _onRun = onRun;

        Title = "Enter parameters";

        var currentY = 1;

        foreach (var prompt in _endpoint.RequiredParameters)
        {
            Add(new Label($"{prompt.Name} ({prompt.Location})")
            {
                X = 1,
                Y = currentY
            });

            var field = new TextField(initialValues?.TryGetValue(prompt.Name, out var value) == true ? value : string.Empty)
            {
                X = 30,
                Y = currentY,
                Width = Dim.Fill(2)
            };

            field.KeyPress += e =>
            {
                if (e.KeyEvent.IsAlt && !e.KeyEvent.IsCtrl)
                {
                    switch (char.ToLower((char)e.KeyEvent.KeyValue))
                    {
                        case 'b':
                            _onBack();
                            e.Handled = true;
                            break;
                        case 'r':
                            RunQuery();
                            e.Handled = true;
                            break;
                    }
                }
            };

            Add(field);
            _fields[prompt.Name] = field;
            currentY += 2;
        }

        foreach (var prompt in _endpoint.OptionalParameters)
        {
            Add(new Label($"{prompt.Name} ({prompt.Location}, optional)")
            {
                X = 1,
                Y = currentY
            });

            var field = new TextField(initialValues?.TryGetValue(prompt.Name, out var value) == true ? value : string.Empty)
            {
                X = 30,
                Y = currentY,
                Width = Dim.Fill(2)
            };

            field.KeyPress += e =>
            {
                if (e.KeyEvent.IsAlt && !e.KeyEvent.IsCtrl)
                {
                    switch (char.ToLower((char)e.KeyEvent.KeyValue))
                    {
                        case 'b':
                            _onBack();
                            e.Handled = true;
                            break;
                        case 'r':
                            RunQuery();
                            e.Handled = true;
                            break;
                    }
                }
            };

            Add(field);
            _fields[prompt.Name] = field;
            currentY += 2;
        }

        var backButton = new Button("Back")
        {
            X = 1,
            Y = Pos.AnchorEnd(3)
        };
        backButton.Clicked += _onBack;

        var runButton = new Button("Run")
        {
            X = Pos.Right(backButton) + 2,
            Y = Pos.Top(backButton),
            IsDefault = true
        };
        runButton.Clicked += RunQuery;

        Add(backButton, runButton);
    }

    private void RunQuery()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var prompt in _endpoint.RequiredParameters)
        {
            var text = _fields[prompt.Name].Text?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.ErrorQuery("Missing parameter", $"Enter a value for '{prompt.Name}'.", "OK");
                return;
            }

            values[prompt.Name] = text;
        }

        foreach (var prompt in _endpoint.OptionalParameters)
        {
            var text = _fields[prompt.Name].Text?.ToString()?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
                values[prompt.Name] = text;
        }

        _onRun(new ParameterValueSet { Values = values });
    }
}
