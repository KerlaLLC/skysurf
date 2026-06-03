using Terminal.Gui;
using skysurf.App.Navigation;

namespace skysurf.App;

public sealed class AppShell : Window
{
    private readonly AppNavigator _navigator;
    private readonly Label _statusLabel;
    private View? _currentScreen;

    // The status bar is composed of a transient message (set by SetStatus) followed by the
    // current screen's persistent text (set by ShowScreen). SetStatus only swaps the message
    // so the persistent text — e.g. the navigation hints — is preserved.
    private string _statusBase = string.Empty;
    private string _statusMessage = string.Empty;

    public AppShell(AppNavigator navigator)
    {
        _navigator = navigator;

        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        Title = "skysurf";

        _statusLabel = new Label(string.Empty)
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1
        };

        Add(_statusLabel);
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
        _statusBase = status;
        _statusMessage = string.Empty;
        RenderStatus();
        Add(_currentScreen);
        _currentScreen.SetFocus();
    }

    public void SetStatus(string message)
    {
        _statusMessage = message;
        RenderStatus();
    }

    private void RenderStatus()
    {
        if (string.IsNullOrWhiteSpace(_statusMessage))
            _statusLabel.Text = _statusBase;
        else if (string.IsNullOrWhiteSpace(_statusBase))
            _statusLabel.Text = _statusMessage;
        else
            _statusLabel.Text = $"{_statusMessage}  {_statusBase}";
    }

    public override bool ProcessKey(KeyEvent keyEvent)
    {
        // Let the focused child handle the key first, then treat Ctrl+O as a global shortcut
        // to open the connections screen.
        if (base.ProcessKey(keyEvent))
        {
            return true;
        }

        if (keyEvent.Key == (Key.CtrlMask | Key.O))
        {
            _navigator.ShowConnections();
            return true;
        }

        return false;
    }
}
