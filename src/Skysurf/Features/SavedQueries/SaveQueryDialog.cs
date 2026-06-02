using Terminal.Gui;

namespace skysurf.Features.SavedQueries;

public static class SaveQueryDialog
{
    public static string? Show()
    {
        var dialog = new Dialog("Save query", 60, 10);

        var nameField = new TextField(string.Empty)
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill(2)
        };

        dialog.Add(new Label("Saved query name")
        {
            X = 1,
            Y = 1
        });
        dialog.Add(nameField);

        string? result = null;

        var cancelButton = new Button("Cancel");
        cancelButton.Clicked += () => Application.RequestStop();

        var saveButton = new Button("Save")
        {
            IsDefault = true
        };
        saveButton.Clicked += () =>
        {
            var name = nameField.Text?.ToString();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.ErrorQuery("Invalid name", "Enter a name for the saved query.", "OK");
                return;
            }

            result = name.Trim();
            Application.RequestStop();
        };

        dialog.AddButton(cancelButton);
        dialog.AddButton(saveButton);

        Application.Run(dialog);
        return result;
    }
}
