using Terminal.Gui;

namespace skysurf.Features.SchemaCatalog;

public sealed class SchemaLoadingScreen : FrameView
{
    private readonly Label _messageLabel;
    private readonly ProgressBar _progressBar;

    public SchemaLoadingScreen()
    {
        Title = "Loading SKY schemas";

        _messageLabel = new Label("Loading API schema catalog...")
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(2)
        };

        _progressBar = new ProgressBar
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill(2),
            Height = 1,
            Fraction = 0f
        };

        Add(_messageLabel, _progressBar);
    }

    public void UpdateProgress(SchemaRefreshProgress progress)
    {
        _messageLabel.Text = $"Loading {progress.Current}/{progress.Total}: {progress.ApiName}";
        _progressBar.Fraction = progress.Total == 0
            ? 0f
            : (float)progress.Current / progress.Total;
    }

    public void ShowError(string message)
    {
        _messageLabel.Text = $"Failed to load SKY schemas: {message}";
    }
}
