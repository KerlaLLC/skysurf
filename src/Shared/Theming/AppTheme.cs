using Terminal.Gui;

namespace Shared.Theming;

/// <summary>
/// A white-on-black theme for the Terminal.Gui apps, replacing the default
/// blue-background ("DOS edit.com") look with something that blends with the
/// terminal's own color scheme.
/// </summary>
public static class AppTheme
{
    /// <summary>
    /// Overrides the global Terminal.Gui color schemes. Must be called after
    /// <see cref="Application.Init()"/> — the driver does not exist before then.
    /// </summary>
    public static void Apply()
    {
        var mono = new ColorScheme
        {
            Normal    = Application.Driver.MakeAttribute(Color.White,      Color.Black),
            Focus     = Application.Driver.MakeAttribute(Color.Black,      Color.White),
            HotNormal = Application.Driver.MakeAttribute(Color.BrightCyan, Color.Black),
            HotFocus  = Application.Driver.MakeAttribute(Color.Black,      Color.White),
            Disabled  = Application.Driver.MakeAttribute(Color.DarkGray,   Color.Black),
        };

        Colors.TopLevel = mono;
        Colors.Base     = mono;
        Colors.Menu     = mono;
        Colors.Dialog   = mono;

        Colors.Error = new ColorScheme
        {
            Normal    = Application.Driver.MakeAttribute(Color.BrightRed,    Color.Black),
            Focus     = Application.Driver.MakeAttribute(Color.Black,        Color.Red),
            HotNormal = Application.Driver.MakeAttribute(Color.BrightYellow, Color.Black),
            HotFocus  = Application.Driver.MakeAttribute(Color.Black,        Color.Red),
            Disabled  = Application.Driver.MakeAttribute(Color.DarkGray,     Color.Black),
        };
    }
}
