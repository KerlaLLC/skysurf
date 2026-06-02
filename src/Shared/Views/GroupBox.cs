using Terminal.Gui;

namespace Shared.Views;

/// <summary>
/// A <see cref="FrameView"/> whose <see cref="FrameView.Title"/> may carry a mnemonic
/// (e.g. <c>"_Requests"</c>). Pressing Alt+&lt;mnemonic&gt; moves focus to the first
/// focusable control inside the group, mirroring the WinForms label-mnemonic behavior.
/// </summary>
public class GroupBox : FrameView
{
    public GroupBox()
    {
    }

    public GroupBox(string title) : base(title)
    {
    }

    public override bool ProcessHotKey(KeyEvent ke)
    {
        // Title is not virtual in FrameView, so parse the mnemonic on demand. This only
        // runs on Alt key presses, so the per-keystroke cost is negligible.
        if (ke.IsAlt &&
            TextFormatter.FindHotKey(Title ?? "", (Rune)'_', false, out _, out var hotKey) &&
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
        // live one level deeper, so recurse to reach them.
        foreach (var sub in view.Subviews)
        {
            if (sub.CanFocus && sub.Visible && sub.Enabled)
            {
                return sub;
            }

            var nested = FindFirstFocusable(sub);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
