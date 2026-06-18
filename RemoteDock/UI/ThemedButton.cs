using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RemoteDock.UI;

public enum ButtonTone { Primary, Default, Ghost, Danger }

/// <summary>
/// Flat, softly rounded button with a quiet hover state.
/// Keeps the commercial feel without WinForms' default chrome.
/// </summary>
public sealed class ThemedButton : Button
{
    private bool _hover;
    private bool _down;
    private ButtonTone _tone = ButtonTone.Default;

    public ButtonTone Tone
    {
        get => _tone;
        set { _tone = value; Invalidate(); }
    }

    public ThemedButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseOverBackColor = Color.Transparent;
        FlatAppearance.MouseDownBackColor = Color.Transparent;
        Font = Theme.BodyStrong;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        Height = 34;
    }

    protected override void OnMouseEnter(System.EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(System.EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var parentBack = Parent?.BackColor ?? Theme.Canvas;
        g.Clear(parentBack);

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        (Color fill, Color border, Color text) = Resolve();

        using var path = Theme.RoundedRect(rect, 8);
        if (fill != Color.Transparent)
        {
            using var b = new SolidBrush(fill);
            g.FillPath(b, path);
        }
        if (border != Color.Transparent)
        {
            using var p = new Pen(border);
            g.DrawPath(p, path);
        }

        var enabledText = Enabled ? text : Theme.InkFaint;
        TextRenderer.DrawText(g, Text, Font, rect, enabledText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private (Color fill, Color border, Color text) Resolve()
    {
        if (!Enabled)
            return (Theme.SurfaceMuted, Theme.Border, Theme.InkFaint);

        switch (_tone)
        {
            case ButtonTone.Primary:
                var pf = _down ? Theme.AccentHover : _hover ? Theme.AccentHover : Theme.Accent;
                return (pf, Color.Transparent, Theme.AccentInk);
            case ButtonTone.Danger:
                var df = _hover ? Theme.Offline : Color.FromArgb(0xF6, 0xDE, 0xDE);
                var dt = _hover ? Color.White : Theme.Offline;
                return (df, _hover ? Color.Transparent : Color.FromArgb(0xEE, 0xC9, 0xC9), dt);
            case ButtonTone.Ghost:
                return (_hover ? Theme.SurfaceMuted : Color.Transparent, Color.Transparent, Theme.InkSoft);
            default:
                var bf = _down ? Theme.SurfaceMuted : _hover ? Color.FromArgb(0xFB, 0xFA, 0xF7) : Theme.Surface;
                return (bf, _hover ? Theme.BorderStrong : Theme.Border, Theme.Ink);
        }
    }
}
