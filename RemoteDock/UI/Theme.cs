using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RemoteDock.UI;

/// <summary>
/// Central design system for RemoteDock.
/// Direction: calm warm-neutral surfaces, a single restrained clay accent,
/// monochrome ink text, hairline borders, generous spacing.
/// Inspired by the Claude / Codex desktop look: quiet, precise, commercial-grade.
/// </summary>
public static class Theme
{
    // --- Surfaces -------------------------------------------------------
    public static readonly Color Canvas = Color.FromArgb(0xF6, 0xF5, 0xF1); // app background (warm paper)
    public static readonly Color Surface = Color.FromArgb(0xFF, 0xFF, 0xFF); // cards / inputs
    public static readonly Color SurfaceMuted = Color.FromArgb(0xF0, 0xEF, 0xEA); // subtle panels
    public static readonly Color Ink = Color.FromArgb(0x23, 0x21, 0x1E); // primary text
    public static readonly Color InkSoft = Color.FromArgb(0x6B, 0x69, 0x63); // secondary text
    public static readonly Color InkFaint = Color.FromArgb(0x9A, 0x97, 0x90); // hints
    public static readonly Color Border = Color.FromArgb(0xE2, 0xE0, 0xD9); // hairline
    public static readonly Color BorderStrong = Color.FromArgb(0xCF, 0xCC, 0xC3);

    // --- Accent (clay / terracotta, used with restraint) ----------------
    public static readonly Color Accent = Color.FromArgb(0xC2, 0x61, 0x3E);
    public static readonly Color AccentHover = Color.FromArgb(0xAD, 0x52, 0x31);
    public static readonly Color AccentSoft = Color.FromArgb(0xF3, 0xE7, 0xE0); // tint for selection
    public static readonly Color AccentInk = Color.FromArgb(0xFF, 0xFF, 0xFF);

    // --- Status ---------------------------------------------------------
    public static readonly Color Online = Color.FromArgb(0x2E, 0xA0, 0x5F);
    public static readonly Color OnlineSoft = Color.FromArgb(0xE8, 0xF4, 0xEC);
    public static readonly Color Offline = Color.FromArgb(0xD0, 0x3A, 0x3A);
    public static readonly Color OfflineSoft = Color.FromArgb(0xFA, 0xEC, 0xEC);
    public static readonly Color Idle = Color.FromArgb(0xB6, 0xB2, 0xA9);
    public static readonly Color Warn = Color.FromArgb(0xC7, 0x8A, 0x1E);

    // --- Metric bar thresholds -----------------------------------------
    public static readonly Color MetricGood = Color.FromArgb(0x3E, 0xA8, 0x6B);
    public static readonly Color MetricBusy = Color.FromArgb(0xC7, 0x8A, 0x1E);
    public static readonly Color MetricHot = Color.FromArgb(0xCE, 0x4A, 0x4A);
    public static readonly Color MetricTrack = Color.FromArgb(0xEC, 0xEA, 0xE4);

    // --- Typography -----------------------------------------------------
    private const string FontFamily = "Segoe UI";
    public static Font Display => new(FontFamily, 15f, FontStyle.Bold);
    public static Font Title => new(FontFamily, 11.5f, FontStyle.Bold);
    public static Font Body => new(FontFamily, 9.75f, FontStyle.Regular);
    public static Font BodyStrong => new(FontFamily, 9.75f, FontStyle.Bold);
    public static Font Label => new(FontFamily, 9f, FontStyle.Regular);
    public static Font Caption => new(FontFamily, 8.5f, FontStyle.Regular);
    public static Font Mono => new("Cascadia Mono", 9f, FontStyle.Regular);

    /// <summary>Mono font with a Consolas fallback for older Windows.</summary>
    public static Font MonoSafe()
    {
        try
        {
            using var probe = new Font("Cascadia Mono", 9f);
            if (probe.Name.StartsWith("Cascadia")) return new Font("Cascadia Mono", 9f);
        }
        catch { }
        return new Font("Consolas", 9.5f);
    }

    // --- Form / control helpers ----------------------------------------

    public static void ApplyWindow(Form form)
    {
        form.BackColor = Canvas;
        form.ForeColor = Ink;
        form.Font = Body;
    }

    public static void StyleInput(TextBox box)
    {
        box.BorderStyle = BorderStyle.FixedSingle;
        box.BackColor = Surface;
        box.ForeColor = Ink;
        box.Font = Body;
    }

    public static void StyleInput(NumericUpDown box)
    {
        box.BorderStyle = BorderStyle.FixedSingle;
        box.BackColor = Surface;
        box.ForeColor = Ink;
        box.Font = Body;
    }

    public static void StyleInput(ComboBox box)
    {
        box.FlatStyle = FlatStyle.Flat;
        box.BackColor = Surface;
        box.ForeColor = Ink;
        box.Font = Body;
    }

    public static void StyleReadout(TextBox box)
    {
        box.BorderStyle = BorderStyle.None;
        box.BackColor = SurfaceMuted;
        box.ForeColor = Ink;
        box.Font = MonoSafe();
    }

    public static void StyleGroup(GroupBox box)
    {
        box.ForeColor = InkSoft;
        box.Font = Title;
        box.BackColor = Color.Transparent;
    }

    /// <summary>Quiet card surface with a hairline border and soft rounded corners.</summary>
    public static void PaintCard(Graphics g, Rectangle bounds, Color fill, Color border, int radius = 10)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundedRect(bounds, radius);
        using var b = new SolidBrush(fill);
        using var p = new Pen(border);
        g.FillPath(b, path);
        g.DrawPath(p, path);
    }

    public static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        if (radius <= 0) { path.AddRectangle(r); return path; }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
