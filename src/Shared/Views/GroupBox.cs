using Terminal.Gui;

namespace Shared.Views;

/// <summary>
/// A <see cref="FrameView"/> whose title may carry a mnemonic (e.g. <c>"_Requests"</c>).
/// The underscore is not shown; instead the marked letter is drawn in the accent
/// (<see cref="ColorScheme.HotNormal"/>) color, and pressing Alt+&lt;mnemonic&gt; moves
/// focus to the first focusable control inside the group — mirroring WinForms
/// label-mnemonic behavior.
/// </summary>
/// <remarks>
/// FrameView draws its <see cref="FrameView.Title"/> verbatim (via the driver's
/// DrawWindowTitle), so a raw "_Requests" would render the literal underscore. We
/// therefore keep the base Title empty and paint the title ourselves in <see cref="Redraw"/>.
/// </remarks>
public class GroupBox : FrameView
{
    private string _mnemonicTitle = string.Empty;

    public GroupBox()
    {
    }

    public GroupBox(string title)
    {
        MnemonicTitle = title;
    }

    /// <summary>The title, including its <c>_</c> mnemonic marker (e.g. <c>"_Requests"</c>).</summary>
    public string MnemonicTitle
    {
        get => _mnemonicTitle;
        set
        {
            _mnemonicTitle = value ?? string.Empty;
            // The base draws titles verbatim; we render ours (mnemonic-aware) in Redraw.
            base.Title = string.Empty;
            SetNeedsDisplay();
        }
    }

    public override void Redraw(Rect bounds)
    {
        base.Redraw(bounds);
        DrawMnemonicTitle();
    }

    private void DrawMnemonicTitle()
    {
        var driver = Application.Driver;
        if (driver == null || string.IsNullOrEmpty(_mnemonicTitle) || Frame.Width <= 0)
        {
            return;
        }

        // Split off the mnemonic: the rune after the first '_' is the hotkey; the
        // underscore itself is removed from what we display.
        var spec = _mnemonicTitle;
        var underscore = spec.IndexOf('_');
        string display;
        int hotIndex;
        if (underscore >= 0 && underscore < spec.Length - 1)
        {
            display = spec.Remove(underscore, 1);
            hotIndex = underscore;
        }
        else
        {
            display = spec.Replace("_", string.Empty);
            hotIndex = -1;
        }

        // The title sits on the top border row (row 0), after the corner and a leading
        // space: "┌ Title ─┐". Coordinates are view-relative; AddRune clips to the frame.
        var max = Frame.Width - 4; // corner + space + space + corner
        if (max < 1)
        {
            return;
        }
        if (display.Length > max)
        {
            display = display.Substring(0, max);
            if (hotIndex >= max)
            {
                hotIndex = -1;
            }
        }

        var normal = GetNormalColor();
        var hot = ColorScheme.HotNormal;
        const int row = 0;
        var col = 1; // leading space cell, just past the corner

        driver.SetAttribute(normal);
        AddRune(col, row, (Rune)' ');
        for (var i = 0; i < display.Length; i++)
        {
            driver.SetAttribute(i == hotIndex ? hot : normal);
            AddRune(col + 1 + i, row, (Rune)display[i]);
        }
        driver.SetAttribute(normal);
        AddRune(col + 1 + display.Length, row, (Rune)' ');
    }

    public override bool ProcessHotKey(KeyEvent ke)
    {
        // Title is not virtual in FrameView, so parse the mnemonic on demand. This only
        // runs on Alt key presses, so the per-keystroke cost is negligible.
        if (ke.IsAlt &&
            TextFormatter.FindHotKey(_mnemonicTitle, (Rune)'_', false, out _, out var hotKey) &&
            ke.Key == (Key.AltMask | hotKey))
        {
            var target = FindFirstFocusable(this);
            if (target != null)
            {
                target.SetFocus();
                return true; // handled — stop propagation
            }
        }

        // Preserve any child hotkeys (buttons, check boxes, etc.).
        return base.ProcessHotKey(ke);
    }

    private static View? FindFirstFocusable(View view)
    {
        // For the FrameView itself, Subviews is just [contentView]; the real children
        // live one level deeper, so recurse to reach them. We descend BEFORE returning the
        // subview itself because containers (the content view, nested FrameViews/GroupBoxes)
        // report CanFocus = true — returning one of those and calling SetFocus lands on the
        // wrong leaf. Descending first yields the first real control in document order, i.e.
        // the topmost focusable control in the group.
        foreach (var sub in view.Subviews)
        {
            if (!sub.Visible || !sub.Enabled)
            {
                continue;
            }

            var nested = FindFirstFocusable(sub);
            if (nested != null)
            {
                return nested;
            }

            if (sub.CanFocus)
            {
                return sub;
            }
        }

        return null;
    }
}
