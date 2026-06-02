using Terminal.Gui;

namespace skysurf.App;

public sealed class AppShell : Window
{
    private readonly Label _statusLabel;
    private View? _currentScreen;

    public AppShell(object _)
    {
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        Title = "skysurf";

        _statusLabel = new Label(string.Empty)
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(19),
            Height = 1
        };

        var quitButton = new Button("Quit (Ctrl+Q)")
        {
            X = Pos.AnchorEnd(18),
            Y = Pos.AnchorEnd(1)
        };
        quitButton.Clicked += () => Application.RequestStop();

        Add(_statusLabel, quitButton);
    }

    public void ShowScreen(View view, string title, string status)
    {
        if (_currentScreen is not null)
        {
            Remove(_currentScreen);
        }

        _currentScreen = view;
        _currentScreen.X = 0;
        _currentScreen.Y = 0;
        _currentScreen.Width = Dim.Fill();
        _currentScreen.Height = Dim.Fill(1);

        Title = title;
        _statusLabel.Text = status;
        Add(_currentScreen);
        _currentScreen.SetFocus();
    }

    public void SetStatus(string status)
    {
        _statusLabel.Text = status;
    }
}
