using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Principal;
using Microsoft.Win32;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using RemoteDock.UI;

namespace RemoteDock;

internal static class Program
{
    private static readonly string CrashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RemoteDock",
        "crash.log");

    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.AppendAllText(CrashLogPath, $"\n===== RemoteDock v15 start {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====\n", Encoding.UTF8);

            Application.ThreadException += (_, e) => ReportCrash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex) ReportCrash(ex);
                else File.AppendAllText(CrashLogPath, $"Unhandled non-Exception: {e.ExceptionObject}\n", Encoding.UTF8);
            };

            if (args.Any(a => a.Equals("--reset-profiles", StringComparison.OrdinalIgnoreCase)))
            {
                var profilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RemoteDock", "profiles.json");
                if (File.Exists(profilePath)) File.Delete(profilePath);
                MessageBox.Show($"Profiles reset complete.\n{profilePath}", "RemoteDock", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            ReportCrash(ex);
            Environment.ExitCode = 1;
        }
    }

    private static void ReportCrash(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.AppendAllText(CrashLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n", Encoding.UTF8);
        }
        catch { }

        try
        {
            MessageBox.Show(
                "RemoteDock startup/runtime error:\n\n" + ex.Message + "\n\nCrash log:\n" + CrashLogPath,
                "RemoteDock Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch { }
    }
}

public sealed class DeviceProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Device";
    public string MountName { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string User { get; set; } = "";
    public string RemotePath { get; set; } = "/home";
    public string DriveLetter { get; set; } = "R";
    public string WebUrl { get; set; } = "";
    public string VsCodeRemotePath { get; set; } = "";
    public string SshKeyPath { get; set; } = "";
    public string DeviceType { get; set; } = "Auto"; // Auto, Linux, Windows, Network/Web
    public int SortOrder { get; set; }
    public bool AutoMount { get; set; }
    public string EncryptedPasswordBase64 { get; set; } = "";
    public string GroupName { get; set; } = "Default";
    public string Tags { get; set; } = "";
    public string Notes { get; set; } = "";
    public string FavoritePathsText { get; set; } = "";
    public string CommandPresetsText { get; set; } = "";
    public string ServicesText { get; set; } = "";
    public string DockerComposePath { get; set; } = "";
    public string RoutesText { get; set; } = "";
    public string WorkspaceLocalPath { get; set; } = "";
    public string WorkspaceRemotePath { get; set; } = "";
    public string WorkspaceWebUrl { get; set; } = "";
    public string BackupRemotePath { get; set; } = "";
    public string BackupLocalPath { get; set; } = "";
}

public sealed class ProfileStore
{
    private readonly string _appDir;
    private readonly string _profilePath;
    private readonly string _keyDir;

    public ProfileStore()
    {
        _appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RemoteDock");
        _profilePath = Path.Combine(_appDir, "profiles.json");
        _keyDir = Path.Combine(_appDir, "keys");
        Directory.CreateDirectory(_appDir);
        Directory.CreateDirectory(_keyDir);
    }

    public string AppDir => _appDir;
    public string ProfilePath => _profilePath;
    public string KeyDir => _keyDir;

    public List<DeviceProfile> Load()
    {
        if (!File.Exists(_profilePath)) return new List<DeviceProfile>();
        try
        {
            var json = File.ReadAllText(_profilePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<List<DeviceProfile>>(json) ?? new List<DeviceProfile>();
        }
        catch
        {
            return new List<DeviceProfile>();
        }
    }

    public void Save(List<DeviceProfile> profiles)
    {
        var json = JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_profilePath, json, Encoding.UTF8);
    }

    public string ImportKeyForProfile(DeviceProfile profile, string sourcePath)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (string.IsNullOrWhiteSpace(sourcePath)) return "";
        sourcePath = sourcePath.Trim().Trim('"');
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("SSH key file was not found.", sourcePath);

        var profileKeyDir = Path.Combine(_keyDir, profile.Id);
        Directory.CreateDirectory(profileKeyDir);

        var originalName = Path.GetFileName(sourcePath);
        var safeName = string.Concat(originalName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        var hash = ShortFileHash(sourcePath);
        var target = Path.Combine(profileKeyDir, $"{hash}_{safeName}");

        File.Copy(sourcePath, target, true);
        try { File.SetAttributes(target, (File.GetAttributes(target) | FileAttributes.Hidden) & ~FileAttributes.ReadOnly); } catch { }
        TryFixPrivateKeyAcl(target);
        return target;
    }

    private static string ShortFileHash(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash).ToLowerInvariant()[..12];
        }
        catch
        {
            return Guid.NewGuid().ToString("N")[..12];
        }
    }

    public static void TryFixPrivateKeyAcl(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try
        {
            var user = WindowsIdentity.GetCurrent().Name;
            RunHidden("icacls", $"\"{path}\" /inheritance:r");
            RunHidden("icacls", $"\"{path}\" /remove:g *S-1-1-0 *S-1-5-32-545 *S-1-5-11");
            RunHidden("icacls", $"\"{path}\" /grant:r \"{user}:(R)\"");
        }
        catch { }
    }

    private static void RunHidden(string fileName, string args)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            process.Start();
            process.WaitForExit(3000);
        }
        catch { }
    }

    public bool IsInsideKeyStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var full = Path.GetFullPath(path.Trim().Trim('"'));
            var keyRoot = Path.GetFullPath(_keyDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(keyRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static string EncryptPassword(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public static string DecryptPassword(string encryptedBase64)
    {
        if (string.IsNullOrWhiteSpace(encryptedBase64)) return "";
        try
        {
            var protectedBytes = Convert.FromBase64String(encryptedBase64);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return "";
        }
    }
}

public sealed class StatusMetrics
{
    public bool Online { get; set; }
    public bool Mounted { get; set; }
    public string StatusText { get; set; } = "Unknown";
    public string HostName { get; set; } = "";
    public string Uptime { get; set; } = "";
    public int CpuPercent { get; set; } = -1;
    public int RamPercent { get; set; } = -1;
    public int DiskPercent { get; set; } = -1;
    public string RamText { get; set; } = "";
    public string DiskText { get; set; } = "";
    public string ProcessText { get; set; } = "";
    public string RawText { get; set; } = "";
    public string ErrorText { get; set; } = "";
}

public sealed class MetricBar : Control
{
    private int _value = -1;
    private string _caption = "";

    public int ValuePercent
    {
        get => _value;
        set { _value = value; Invalidate(); }
    }

    public string Caption
    {
        get => _caption;
        set { _caption = value; Invalidate(); }
    }

    public MetricBar()
    {
        DoubleBuffered = true;
        Height = 34;
        Font = new Font("Segoe UI", 9, FontStyle.Bold);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(Color.FromArgb(18, 18, 22));
        using var border = new Pen(Color.FromArgb(70, 70, 80));
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        g.DrawRectangle(border, rect);

        var v = Math.Clamp(_value, -1, 100);
        var fillWidth = v < 0 ? 0 : (int)((Width - 2) * (v / 100.0));
        var color = v < 0 ? Color.FromArgb(55, 55, 60) : v < 70 ? Color.FromArgb(31, 185, 93) : v < 90 ? Color.FromArgb(235, 169, 46) : Color.FromArgb(230, 73, 73);
        using var fill = new SolidBrush(color);
        g.FillRectangle(fill, 1, 1, fillWidth, Height - 2);

        var text = v < 0 ? $"{_caption}: ?" : $"{_caption}: {v}%";
        TextRenderer.DrawText(g, text, Font, rect, Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
    }
}

public sealed class BusyForm : Form
{
    private readonly Label _label;
    private readonly string _message;
    private readonly System.Windows.Forms.Timer _timer = new();
    private int _frame;

    // User-directed MEOWNTING loader:
    // only the rabbit / cheering lines move left-right; the cat stays fixed.
    // The operation text stays below the whole emoticon block.
    private readonly string[] _topOffsets = new[] { "", " ", "  ", "   ", "  ", " ", "" };
    private readonly string[] _armOffsets = new[] { "   ", "    ", "     ", "      ", "     ", "    ", "   " };
    private readonly string[] _tail = new[] { "   ", "  ｣", "  ))", " )))", "  ))", "  ｣", "   " };
    private readonly string[] _dots = new[] { ".", "..", "...", "....", "...", ".." };

    public BusyForm(string title, string message)
    {
        _message = string.IsNullOrWhiteSpace(message) ? "Working" : message;
        Text = title;
        Width = 500;
        Height = 260;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ControlBox = false;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(30, 30, 35);

        _label = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Cascadia Mono", 12, FontStyle.Bold),
            Text = BuildFrame()
        };
        Controls.Add(_label);

        _timer.Interval = 160;
        _timer.Tick += (_, _) =>
        {
            _frame++;
            _label.Text = BuildFrame();
        };
        Shown += (_, _) =>
        {
            CenterOnOwnerScreen();
            _timer.Start();
        };
        FormClosed += (_, _) => _timer.Stop();
    }

    private string BuildFrame()
    {
        var idx = _frame % _topOffsets.Length;
        var dots = _dots[_frame % _dots.Length];
        return
            $"{_topOffsets[idx]}      (\\ /)\n" +
            $"{_armOffsets[idx]}   ヾ(>᎑<)ﾉ{_tail[idx]}\n" +
            "      ∧,,,∧\n" +
            "     (◉_◉)\n" +
            "     /づ♡\n" +
            "\n" +
            $"MEOWNTING{dots}\n" +
            _message;
    }

    private void CenterOnOwnerScreen()
    {
        try
        {
            if (Owner != null && !Owner.IsDisposed)
            {
                var x = Owner.Left + (Owner.Width - Width) / 2;
                var y = Owner.Top + (Owner.Height - Height) / 2;
                var screen = Screen.FromControl(Owner).WorkingArea;
                x = Math.Max(screen.Left, Math.Min(x, screen.Right - Width));
                y = Math.Max(screen.Top, Math.Min(y, screen.Bottom - Height));
                Location = new Point(x, y);
            }
            else
            {
                StartPosition = FormStartPosition.CenterScreen;
            }
        }
        catch { }
    }
}


public sealed class StatusPopupForm : Form
{
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _onlineLabel;
    private readonly Label _mountedLabel;
    private readonly MetricBar _cpu;
    private readonly MetricBar _ram;
    private readonly MetricBar _disk;
    private readonly TextBox _processes;
    private readonly TextBox _raw;

    public StatusPopupForm(string deviceName)
    {
        Text = "Device Status - " + deviceName;
        Width = 980;
        Height = 720;
        MinimumSize = new Size(820, 560);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(245, 246, 248);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, Padding = new Padding(14) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(32, 34, 40), Padding = new Padding(12) };
        root.Controls.Add(header, 0, 0);
        _title = new Label { Text = deviceName, ForeColor = Color.White, Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 14, FontStyle.Bold) };
        _subtitle = new Label { Text = "Loading live status...", ForeColor = Color.Gainsboro, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9) };
        header.Controls.Add(_subtitle);
        header.Controls.Add(_title);

        var pills = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 12, 0, 0) };
        _onlineLabel = MakePill("● Checking", Color.Gray);
        _mountedLabel = MakePill("◆ Mount ?", Color.Gray);
        pills.Controls.Add(_onlineLabel);
        pills.Controls.Add(_mountedLabel);
        root.Controls.Add(pills, 0, 1);

        var bars = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        bars.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        bars.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        bars.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
        _cpu = new MetricBar { Caption = "CPU / Load", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3) };
        _ram = new MetricBar { Caption = "RAM", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3) };
        _disk = new MetricBar { Caption = "DISK /", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3) };
        bars.Controls.Add(_cpu, 0, 0);
        bars.Controls.Add(_ram, 0, 1);
        bars.Controls.Add(_disk, 0, 2);
        root.Controls.Add(bars, 0, 2);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        _processes = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font(FontFamily.GenericMonospace, 9) };
        _raw = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font(FontFamily.GenericMonospace, 9) };
        var tab1 = new TabPage("Top Processes") { Padding = new Padding(6) };
        var tab2 = new TabPage("Raw / Notes") { Padding = new Padding(6) };
        tab1.Controls.Add(_processes);
        tab2.Controls.Add(_raw);
        tabs.TabPages.Add(tab1);
        tabs.TabPages.Add(tab2);
        root.Controls.Add(tabs, 0, 3);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
        var close = new Button { Text = "Close", Width = 100, Height = 30 };
        close.Click += (_, _) => Close();
        var copy = new Button { Text = "Copy Raw", Width = 100, Height = 30 };
        copy.Click += (_, _) => { if (!string.IsNullOrEmpty(_raw.Text)) Clipboard.SetText(_raw.Text); };
        buttons.Controls.Add(close);
        buttons.Controls.Add(copy);
        root.Controls.Add(buttons, 0, 4);
    }

    private static Label MakePill(string text, Color color)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            Width = 160,
            Height = 34,
            Margin = new Padding(0, 0, 10, 0),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = color,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
    }

    public void SetLoading(string message)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string>(SetLoading), message); return; }
        _subtitle.Text = message;
        _onlineLabel.Text = "● Checking";
        _onlineLabel.BackColor = Color.Gray;
        _mountedLabel.Text = "◆ Mount ?";
        _mountedLabel.BackColor = Color.Gray;
        _cpu.ValuePercent = -1;
        _ram.ValuePercent = -1;
        _disk.ValuePercent = -1;
        _processes.Text = "Fetching status over SSH...";
        _raw.Text = message;
    }

    public void SetMetrics(StatusMetrics metrics)
    {
        if (InvokeRequired) { BeginInvoke(new Action<StatusMetrics>(SetMetrics), metrics); return; }
        _subtitle.Text = $"{metrics.HostName}   {metrics.Uptime}".Trim();
        _onlineLabel.Text = metrics.Online ? "● ONLINE" : "● OFFLINE";
        _onlineLabel.BackColor = metrics.Online ? Color.FromArgb(22, 163, 74) : Color.FromArgb(220, 38, 38);
        _mountedLabel.Text = metrics.Mounted ? "◆ MOUNTED" : "◆ NOT MOUNTED";
        _mountedLabel.BackColor = metrics.Mounted ? Color.FromArgb(21, 128, 61) : Color.FromArgb(185, 28, 28);
        _cpu.ValuePercent = metrics.CpuPercent;
        _ram.ValuePercent = metrics.RamPercent;
        _disk.ValuePercent = metrics.DiskPercent;
        _processes.Text = string.IsNullOrWhiteSpace(metrics.ProcessText) ? "No process data." : metrics.ProcessText.Replace("\n", Environment.NewLine);
        _raw.Text = BuildRaw(metrics).Replace("\n", Environment.NewLine);
    }

    private static string BuildRaw(StatusMetrics m)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Online: {m.Online}");
        sb.AppendLine($"Mounted: {m.Mounted}");
        sb.AppendLine($"Host: {m.HostName}");
        sb.AppendLine($"Uptime: {m.Uptime}");
        sb.AppendLine($"CPU/Load: {m.CpuPercent}%");
        sb.AppendLine($"RAM: {m.RamPercent}% {m.RamText}");
        sb.AppendLine($"Disk: {m.DiskPercent}% {m.DiskText}");
        if (!string.IsNullOrWhiteSpace(m.ErrorText))
        {
            sb.AppendLine();
            sb.AppendLine("--- NOTES / STDERR ---");
            sb.AppendLine(m.ErrorText);
        }
        if (!string.IsNullOrWhiteSpace(m.RawText))
        {
            sb.AppendLine();
            sb.AppendLine("--- RAW ---");
            sb.AppendLine(m.RawText);
        }
        return sb.ToString();
    }
}

public sealed class DiscoveryResult
{
    public string IpAddress { get; set; } = "";
    public string HostName { get; set; } = "";
    public string Services { get; set; } = "";
    public string SuggestedType { get; set; } = "";
    public string SuggestedName { get; set; } = "";
    public bool IsGateway { get; set; }
}

public sealed class DiscoveryForm : Form
{
    private readonly Action<DiscoveryResult> _onAdd;
    private readonly DataGridView _grid;
    private readonly TextBox _log;
    private readonly Button _scanButton;
    private readonly Button _addButton;
    private readonly List<DiscoveryResult> _results = new();

    public DiscoveryForm(Action<DiscoveryResult> onAdd)
    {
        _onAdd = onAdd;
        Text = "Device Discovery - LAN Scan";
        Width = 980;
        Height = 680;
        MinimumSize = new Size(820, 520);
        StartPosition = FormStartPosition.CenterParent;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        Controls.Add(root);

        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        _scanButton = new Button { Text = "Scan LAN", Width = 110, Height = 32, Margin = new Padding(0, 8, 8, 0) };
        _scanButton.Click += async (_, _) => await StartScanAsync();
        _addButton = new Button { Text = "Add Selected", Width = 120, Height = 32, Margin = new Padding(0, 8, 8, 0) };
        _addButton.Click += (_, _) => AddSelected();
        var close = new Button { Text = "Close", Width = 90, Height = 32, Margin = new Padding(0, 8, 8, 0) };
        close.Click += (_, _) => Close();
        var hint = new Label { Text = "Scans local IPv4 /24 ranges + default gateways. SSH devices appear when TCP ports respond.", AutoSize = true, Margin = new Padding(12, 15, 0, 0) };
        top.Controls.AddRange(new Control[] { _scanButton, _addButton, close, hint });
        root.Controls.Add(top, 0, 0);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Theme.Canvas,
            BorderStyle = BorderStyle.None,
            EnableHeadersVisualStyles = false
        };
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.SurfaceMuted;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.InkSoft;
        _grid.DefaultCellStyle.BackColor = Theme.Surface;
        _grid.DefaultCellStyle.ForeColor = Theme.Ink;
        _grid.DefaultCellStyle.SelectionBackColor = Theme.AccentSoft;
        _grid.DefaultCellStyle.SelectionForeColor = Theme.Ink;
        _grid.Columns.Add("Ip", "IP");
        _grid.Columns.Add("Host", "Host name");
        _grid.Columns.Add("Services", "Detected services");
        _grid.Columns.Add("Type", "Type");
        _grid.Columns.Add("Name", "Suggested name");
        _grid.CellDoubleClick += (_, _) => AddSelected();
        root.Controls.Add(_grid, 0, 1);

        _log = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, Font = new Font(FontFamily.GenericMonospace, 9) };
        root.Controls.Add(_log, 0, 2);
    }

    public async Task StartScanAsync()
    {
        _scanButton.Enabled = false;
        _results.Clear();
        _grid.Rows.Clear();
        Log("Starting LAN discovery...");
        try
        {
            var candidates = BuildCandidateIps().Distinct().ToList();
            Log($"Candidate IP count: {candidates.Count}");
            var gateWays = GetDefaultGatewayIps().ToHashSet();
            var semaphore = new System.Threading.SemaphoreSlim(96);
            var tasks = candidates.Select(async ip =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var services = new List<string>();
                    if (await TcpOpenAsync(ip, 22, 450)) services.Add("SSH");
                    if (await TcpOpenAsync(ip, 80, 350)) services.Add("HTTP");
                    if (await TcpOpenAsync(ip, 443, 350)) services.Add("HTTPS");
                    if (await TcpOpenAsync(ip, 445, 250)) services.Add("SMB");
                    if (await TcpOpenAsync(ip, 3389, 250)) services.Add("RDP");
                    if (services.Count == 0) return;
                    var host = await TryReverseDnsAsync(ip, 800);
                    var isGateway = gateWays.Contains(ip);
                    var type = isGateway ? "Gateway/Router" : services.Contains("SSH") ? "SSH-capable" : "Network/Web";
                    var name = !string.IsNullOrWhiteSpace(host) && host != ip ? host : isGateway ? $"Router {ip}" : services.Contains("SSH") ? $"SSH Device {ip}" : $"Network Device {ip}";
                    AddResult(new DiscoveryResult { IpAddress = ip, HostName = host, Services = string.Join(", ", services), SuggestedType = type, SuggestedName = name, IsGateway = isGateway });
                }
                finally { semaphore.Release(); }
            }).ToList();
            await Task.WhenAll(tasks);
            Log($"Discovery finished. Found {_results.Count} responsive device(s).");
        }
        catch (Exception ex) { Log("Discovery error: " + ex.Message); }
        finally { _scanButton.Enabled = true; }
    }

    private void AddSelected()
    {
        if (_grid.CurrentRow?.Tag is not DiscoveryResult result) return;
        _onAdd(result);
        Log($"Added: {result.SuggestedName} ({result.IpAddress})");
    }

    private void AddResult(DiscoveryResult result)
    {
        if (InvokeRequired) { BeginInvoke(new Action<DiscoveryResult>(AddResult), result); return; }
        if (_results.Any(r => r.IpAddress == result.IpAddress)) return;
        _results.Add(result);
        var row = _grid.Rows.Add(result.IpAddress, result.HostName, result.Services, result.SuggestedType, result.SuggestedName);
        _grid.Rows[row].Tag = result;
        Log($"Found {result.IpAddress} | {result.Services} | {result.SuggestedName}");
    }

    private void Log(string text)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string>(Log), text); return; }
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
    }

    private static IEnumerable<string> BuildCandidateIps()
    {
        var set = new HashSet<string>();
        foreach (var gw in GetDefaultGatewayIps()) set.Add(gw);
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
            IPInterfaceProperties props;
            try { props = ni.GetIPProperties(); } catch { continue; }
            foreach (var ua in props.UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var parts = ua.Address.ToString().Split('.');
                if (parts.Length != 4) continue;
                var prefix = $"{parts[0]}.{parts[1]}.{parts[2]}.";
                for (var i = 1; i <= 254; i++) set.Add(prefix + i);
            }
        }
        return set;
    }

    private static IEnumerable<string> GetDefaultGatewayIps()
    {
        var list = new List<string>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            IPInterfaceProperties props;
            try { props = ni.GetIPProperties(); } catch { continue; }
            foreach (var gw in props.GatewayAddresses)
            {
                if (gw.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    var ip = gw.Address.ToString();
                    if (!string.IsNullOrWhiteSpace(ip) && ip != "0.0.0.0") list.Add(ip);
                }
            }
        }
        return list;
    }

    private static async Task<bool> TcpOpenAsync(string host, int port, int timeoutMs)
    {
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync(host, port);
            var completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
            return completed == task && client.Connected;
        }
        catch { return false; }
    }

    private static async Task<string> TryReverseDnsAsync(string ip, int timeoutMs)
    {
        try
        {
            var task = Dns.GetHostEntryAsync(ip);
            var completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
            if (completed == task)
            {
                var host = task.Result.HostName;
                return string.IsNullOrWhiteSpace(host) ? ip : host;
            }
        }
        catch { }
        return ip;
    }
}


public static class UiText
{
    public static string Blocks5(int value)
    {
        var v = Math.Clamp(value, 0, 100);
        var filled = Math.Clamp((int)Math.Round(v / 20.0), 0, 5);
        return new string('■', filled) + new string('□', 5 - filled);
    }
}

public sealed class DeviceHubForm : Form
{
    private readonly DeviceProfile _profile;
    private readonly Action _save;
    private TextBox _group = null!;
    private TextBox _tags = null!;
    private TextBox _notes = null!;
    private TextBox _favorites = null!;
    private TextBox _commands = null!;
    private TextBox _services = null!;
    private TextBox _docker = null!;
    private TextBox _routes = null!;
    private TextBox _workspaceLocal = null!;
    private TextBox _workspaceRemote = null!;
    private TextBox _workspaceWeb = null!;
    private TextBox _backupRemote = null!;
    private TextBox _backupLocal = null!;

    public DeviceHubForm(DeviceProfile profile, Action save)
    {
        _profile = profile;
        _save = save;
        Text = "Device Hub - " + (string.IsNullOrWhiteSpace(profile.MountName) ? profile.Name : profile.MountName);
        Width = 960;
        Height = 720;
        MinimumSize = new Size(820, 560);
        StartPosition = FormStartPosition.CenterParent;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(root);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        root.Controls.Add(tabs, 0, 0);

        var general = AddTab(tabs, "Group / Notes");
        _group = AddBox(general, "Group", profile.GroupName, singleLine: true);
        _tags = AddBox(general, "Tags", profile.Tags, singleLine: true);
        _notes = AddBox(general, "Notes", profile.Notes, singleLine: false);

        var fav = AddTab(tabs, "Favorite Paths");
        _favorites = AddBox(fav, "One path per line. Example: /home/user/project or project/logs", profile.FavoritePathsText, singleLine: false);

        var cmd = AddTab(tabs, "Command Presets");
        _commands = AddBox(cmd, "One command per line. Optional: Name = command", profile.CommandPresetsText, singleLine: false);

        var svc = AddTab(tabs, "Services / Docker");
        _services = AddBox(svc, "systemd services, one per line. Example: nginx.service", profile.ServicesText, singleLine: false);
        _docker = AddBox(svc, "Docker compose path. Example: /home/user/app", profile.DockerComposePath, singleLine: true);

        var routes = AddTab(tabs, "Routes");
        _routes = AddBox(routes, "Connection routes, one per line. Example: LAN=192.168.0.55 or TS=100.x.x.x", profile.RoutesText, singleLine: false);

        var ws = AddTab(tabs, "Workspace");
        _workspaceLocal = AddBox(ws, "Local path", profile.WorkspaceLocalPath, singleLine: true);
        _workspaceRemote = AddBox(ws, "Remote path", profile.WorkspaceRemotePath, singleLine: true);
        _workspaceWeb = AddBox(ws, "Web URL", profile.WorkspaceWebUrl, singleLine: true);

        var backup = AddTab(tabs, "Backup");
        _backupRemote = AddBox(backup, "Remote source path", profile.BackupRemotePath, singleLine: true);
        _backupLocal = AddBox(backup, "Local backup folder", profile.BackupLocalPath, singleLine: true);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
        var close = new Button { Text = "Close", Width = 100, Height = 30 };
        close.Click += (_, _) => Close();
        var saveBtn = new Button { Text = "Save Hub", Width = 120, Height = 30 };
        saveBtn.Click += (_, _) => { SaveBack(); _save(); Close(); };
        bottom.Controls.Add(close);
        bottom.Controls.Add(saveBtn);
        root.Controls.Add(bottom, 0, 1);
    }

    private static Panel AddTab(TabControl tabs, string title)
    {
        var page = new TabPage(title) { Padding = new Padding(8) };
        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        page.Controls.Add(panel);
        tabs.TabPages.Add(page);
        return panel;
    }

    private static TextBox AddBox(Control parent, string label, string value, bool singleLine)
    {
        var container = new TableLayoutPanel { Dock = DockStyle.Top, Height = singleLine ? 68 : 220, RowCount = 2, ColumnCount = 1, Padding = new Padding(0,0,0,8) };
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var lbl = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        var tb = new TextBox { Text = value ?? "", Dock = DockStyle.Fill, Multiline = !singleLine, ScrollBars = singleLine ? ScrollBars.None : ScrollBars.Both, WordWrap = !singleLine };
        container.Controls.Add(lbl, 0, 0);
        container.Controls.Add(tb, 0, 1);
        parent.Controls.Add(container);
        container.BringToFront();
        return tb;
    }

    private void SaveBack()
    {
        _profile.GroupName = string.IsNullOrWhiteSpace(_group.Text) ? "Default" : _group.Text.Trim();
        _profile.Tags = _tags.Text.Trim();
        _profile.Notes = _notes.Text.Trim();
        _profile.FavoritePathsText = _favorites.Text.Trim();
        _profile.CommandPresetsText = _commands.Text.Trim();
        _profile.ServicesText = _services.Text.Trim();
        _profile.DockerComposePath = _docker.Text.Trim();
        _profile.RoutesText = _routes.Text.Trim();
        _profile.WorkspaceLocalPath = _workspaceLocal.Text.Trim();
        _profile.WorkspaceRemotePath = _workspaceRemote.Text.Trim();
        _profile.WorkspaceWebUrl = _workspaceWeb.Text.Trim();
        _profile.BackupRemotePath = _backupRemote.Text.Trim();
        _profile.BackupLocalPath = _backupLocal.Text.Trim();
    }
}

public sealed class ListPickerForm : Form
{
    private readonly ListBox _list;
    public string? SelectedValueText { get; private set; }
    public ListPickerForm(string title, IEnumerable<string> items)
    {
        Text = title;
        Width = 680;
        Height = 480;
        StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        Controls.Add(root);
        _list = new ListBox { Dock = DockStyle.Fill, Font = new Font(FontFamily.GenericMonospace, 10) };
        foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x))) _list.Items.Add(item.Trim());
        _list.DoubleClick += (_, _) => Accept();
        root.Controls.Add(_list, 0, 0);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Cancel", Width = 90, Height = 30 };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        var ok = new Button { Text = "Run/Open", Width = 110, Height = 30 };
        ok.Click += (_, _) => Accept();
        buttons.Controls.Add(cancel); buttons.Controls.Add(ok); root.Controls.Add(buttons, 0, 1);
    }
    private void Accept()
    {
        if (_list.SelectedItem == null) return;
        SelectedValueText = _list.SelectedItem.ToString();
        DialogResult = DialogResult.OK;
        Close();
    }
}

public sealed class DiagnosticForm : Form
{
    private readonly TextBox _text;
    public DiagnosticForm(string title)
    {
        Text = title;
        Width = 860;
        Height = 640;
        StartPosition = FormStartPosition.CenterParent;
        Theme.ApplyWindow(this);
        _text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false };
        Theme.StyleReadout(_text);
        Controls.Add(_text);
    }
    public void SetText(string text)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string>(SetText), text); return; }
        _text.Text = (text ?? "").Replace("\n", Environment.NewLine);
    }
}

public sealed class KeyManagerForm : Form
{
    public KeyManagerForm(string keyDir)
    {
        Text = "Key Store";
        Width = 900;
        Height = 560;
        StartPosition = FormStartPosition.CenterParent;
        Theme.ApplyWindow(this);
        var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Theme.Canvas, BorderStyle = BorderStyle.None, EnableHeadersVisualStyles = false };
        grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.SurfaceMuted;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.InkSoft;
        grid.DefaultCellStyle.BackColor = Theme.Surface;
        grid.DefaultCellStyle.ForeColor = Theme.Ink;
        grid.DefaultCellStyle.SelectionBackColor = Theme.AccentSoft;
        grid.DefaultCellStyle.SelectionForeColor = Theme.Ink;
        grid.Columns.Add("File", "Key file");
        grid.Columns.Add("Size", "Size");
        grid.Columns.Add("Modified", "Modified");
        grid.Columns.Add("Folder", "Profile folder");
        Controls.Add(grid);
        if (Directory.Exists(keyDir))
        {
            foreach (var f in Directory.GetFiles(keyDir, "*", SearchOption.AllDirectories))
            {
                var info = new FileInfo(f);
                grid.Rows.Add(info.Name, info.Length + " B", info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), info.Directory?.Name ?? "");
            }
        }
    }
}
public sealed class MainForm : Form
{
    private readonly ProfileStore _store = new();
    private readonly List<DeviceProfile> _profiles;
    private readonly Dictionary<string, string> _status = new();
    private readonly Dictionary<string, StatusMetrics> _metrics = new();
    private readonly Dictionary<string, DateTime> _lastAutoMountFailLog = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly NotifyIcon _tray;

    private DataGridView _grid = null!;
    private TextBox _name = null!;
    private TextBox _mountName = null!;
    private TextBox _host = null!;
    private NumericUpDown _port = null!;
    private TextBox _user = null!;
    private TextBox _remotePath = null!;
    private TextBox _drive = null!;
    private TextBox _webUrl = null!;
    private TextBox _vsCodePath = null!;
    private TextBox _sshKey = null!;
    private TextBox _password = null!;
    private ComboBox _deviceType = null!;
    private CheckBox _autoMount = null!;
    private TextBox _output = null!;
    private ComboBox _groupFilter = null!;
    private TableLayoutPanel _root = null!;
    private CheckBox _autoDetect = null!;
    private NumericUpDown _autoDetectSeconds = null!;
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _statusLabel = null!;
    private int _dragRowIndex = -1;
    private DateTime _lastAutoCheck = DateTime.MinValue;

    public MainForm()
    {
        Text = "RemoteDock MVP v15 Final Polish";
        Width = 1640;
        Height = 940;
        MinimumSize = new Size(1340, 850);
        StartPosition = FormStartPosition.CenterScreen;
        Theme.ApplyWindow(this);
        BackColor = Theme.Canvas;

        _profiles = _store.Load();
        EnsureSortOrders();
        _tray = BuildTrayIcon();
        BuildUi();
        RefreshGrid();
        AppendOutput($"Profile file: {_store.ProfilePath}");
        AppendOutput($"SSH key copies: {_store.KeyDir}");

        _timer.Interval = 180_000;
        _timer.Tick += async (_, _) =>
        {
            if (_autoDetect.Checked)
            {
                await CheckAllStatusesAsync(false);
                _lastAutoCheck = DateTime.Now;
                UpdateStatusBar();
            }
        };
        _timer.Start();
        Shown += async (_, _) => { AdjustLayout(); UpdateStatusBar(); await CheckAllStatusesAsync(false); };
        Resize += (_, _) => AdjustLayout();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _tray.Visible = false;
        _tray.Dispose();
        base.OnFormClosing(e);
    }

    private NotifyIcon BuildTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); });
        menu.Items.Add("Check All", null, async (_, _) => await CheckAllStatusesAsync(true));
        menu.Items.Add("Exit", null, (_, _) => Close());
        var notify = new NotifyIcon { Text = "RemoteDock", Icon = SystemIcons.Application, Visible = true, ContextMenuStrip = menu };
        notify.DoubleClick += (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); };
        return notify;
    }

    private void BuildUi()
    {
        var main = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Theme.Canvas };
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        Controls.Add(main);

        _root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 2, Padding = new Padding(8, 8, 8, 4), BackColor = Theme.Canvas };
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 640));
        main.Controls.Add(_root, 0, 0);

        _statusStrip = new StatusStrip { Dock = DockStyle.Fill, SizingGrip = false, BackColor = Theme.SurfaceMuted, ForeColor = Theme.InkSoft };
        _statusLabel = new ToolStripStatusLabel { Text = "Ready", Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _statusStrip.Items.Add(_statusLabel);
        main.Controls.Add(_statusStrip, 0, 1);

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, BackColor = Theme.Canvas };
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        _root.Controls.Add(left, 0, 0);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowDrop = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            RowTemplate = { Height = 54 },
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = Theme.Border,
            BackgroundColor = Theme.Canvas,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 32,
            ColumnHeadersDefaultCellStyle = { BackColor = Theme.SurfaceMuted, ForeColor = Theme.InkSoft, Font = Theme.Caption },
            DefaultCellStyle = { BackColor = Theme.Surface, ForeColor = Theme.Ink, SelectionBackColor = Theme.AccentSoft, SelectionForeColor = Theme.Ink, Font = Theme.Body }
        };
        _grid.SelectionChanged += (_, _) => LoadSelectedIntoForm();
        _grid.CellDoubleClick += async (_, _) => await ShowSelectedStatusPopupAsync();
        _grid.MouseDown += Grid_MouseDown;
        _grid.MouseMove += Grid_MouseMove;
        _grid.DragOver += (_, e) => e.Effect = DragDropEffects.Move;
        _grid.DragDrop += Grid_DragDrop;
        _grid.RowPostPaint += Grid_RowPostPaint;
        left.Controls.Add(_grid, 0, 0);

        var leftButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(6, 10, 6, 6), WrapContents = false, BackColor = Theme.Canvas };
        var btnNew = MakeButton("New", 80, 30, ButtonTone.Primary);
        btnNew.Click += (_, _) => NewProfile();
        var btnDelete = MakeButton("Delete", 80, 30, ButtonTone.Danger);
        btnDelete.Click += (_, _) => DeleteSelected();
        var btnCheckAll = MakeButton("Check All", 100, 30);
        btnCheckAll.Click += async (_, _) => await RunBusyAsync("Checking", "Cat is checking all devices...", async () => { await CheckAllStatusesAsync(true); _lastAutoCheck = DateTime.Now; UpdateStatusBar(); });
        var btnDiscover = MakeButton("Discover", 100, 30);
        btnDiscover.Click += async (_, _) => await OpenDiscoveryAsync();
        var btnKeys = MakeButton("Keys", 76, 30, ButtonTone.Ghost);
        btnKeys.Click += (_, _) => new KeyManagerForm(_store.KeyDir).Show(this);
        var btnExport = MakeButton("Export", 80, 30, ButtonTone.Ghost);
        btnExport.Click += (_, _) => ExportProfilesBundle();
        _autoDetect = new CheckBox { Text = "Auto detect", Checked = true, AutoSize = true, Margin = new Padding(14, 6, 4, 0), ForeColor = Theme.InkSoft, BackColor = Theme.Canvas };
        _autoDetect.CheckedChanged += (_, _) => UpdateStatusBar();
        _autoDetectSeconds = new NumericUpDown { Minimum = 5, Maximum = 3600, Value = 180, Width = 66, Margin = new Padding(2, 4, 2, 0) };
        _autoDetectSeconds.ValueChanged += (_, _) => { _timer.Interval = (int)_autoDetectSeconds.Value * 1000; UpdateStatusBar(); };
        var secondsLabel = new Label { Text = "sec", AutoSize = true, Margin = new Padding(2, 8, 0, 0), ForeColor = Theme.InkSoft, BackColor = Theme.Canvas };
        leftButtons.Controls.AddRange(new Control[] { btnNew, btnDelete, btnCheckAll, btnDiscover, btnKeys, btnExport, _autoDetect, _autoDetectSeconds, secondsLabel });
        left.Controls.Add(leftButtons, 0, 1);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(10), AutoScroll = false, BackColor = Theme.Canvas };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 492));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 154));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _root.Controls.Add(right, 1, 0);

        var fieldGroup = new GroupBox { Text = "Selected Device", Dock = DockStyle.Fill, Padding = new Padding(10), Margin = new Padding(0, 0, 0, 6) };
        Theme.StyleGroup(fieldGroup);
        right.Controls.Add(fieldGroup, 0, 0);

        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 14, Padding = new Padding(0, 4, 0, 0), BackColor = Theme.Canvas };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 14; i++)
        {
            var h = i == 9 ? 36 : i == 12 ? 42 : i == 13 ? 46 : 30;
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        }
        fieldGroup.Controls.Add(fields);

        _name = AddTextField(fields, "Device Name", 0, 0);
        _mountName = AddTextField(fields, "Mount Name", 0, 1);
        _host = AddTextField(fields, "Host", 0, 2);
        _port = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = 22, Dock = DockStyle.Fill };
        Theme.StyleInput(_port);
        AddLabel(fields, "Port", 0, 3); fields.Controls.Add(_port, 1, 3);
        _user = AddTextField(fields, "User", 0, 4);
        _remotePath = AddTextField(fields, "Remote Path", 0, 5);
        _drive = AddTextField(fields, "Drive Letter", 0, 6);
        _webUrl = AddTextField(fields, "Web URL", 0, 7);
        _vsCodePath = AddTextField(fields, "VS Code Path", 0, 8);
        AddLabel(fields, "SSH Key", 0, 9);
        fields.Controls.Add(BuildKeyPicker(), 1, 9);
        _password = AddTextField(fields, "Password", 0, 10);
        _password.UseSystemPasswordChar = true;
        _deviceType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        Theme.StyleInput(_deviceType);
        _deviceType.Items.AddRange(new object[] { "Auto", "Linux", "Windows", "Network/Web" });
        _deviceType.SelectedIndex = 0;
        AddLabel(fields, "Device Type", 0, 11); fields.Controls.Add(_deviceType, 1, 11);

        var bottomFields = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Theme.Canvas };
        _autoMount = new CheckBox { Text = "Auto mount when online", AutoSize = true, Margin = new Padding(0, 9, 16, 0), ForeColor = Theme.InkSoft, BackColor = Theme.Canvas };
        var saveButton = MakeButton("Save Profile", 124, 32, ButtonTone.Primary);
        saveButton.Click += (_, _) => SaveCurrent();
        bottomFields.Controls.Add(_autoMount);
        bottomFields.Controls.Add(saveButton);
        fields.Controls.Add(bottomFields, 1, 12);

        var keyHint = new Label { Dock = DockStyle.Fill, Text = "SSH key is copied into %APPDATA%\\RemoteDock\\keys when browsing, dragging, or saving.", ForeColor = Theme.InkFaint, TextAlign = ContentAlignment.MiddleLeft, BackColor = Theme.Canvas, Font = Theme.Caption };
        fields.Controls.Add(keyHint, 1, 13);

        var actionGroup = new GroupBox { Text = "Actions", Dock = DockStyle.Fill, Padding = new Padding(8), Margin = new Padding(0, 0, 0, 6) };
        Theme.StyleGroup(actionGroup);
        right.Controls.Add(actionGroup, 0, 1);
        var actionPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 3, BackColor = Theme.Canvas };
        for (int i = 0; i < 5; i++) actionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        actionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        actionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        actionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
        actionGroup.Controls.Add(actionPanel);
        AddAction(actionPanel, "Check", async () => await CheckSelectedStatusAsync(true), 0, 0);
        AddAction(actionPanel, "Mount", async () => await MountSelectedAsync(), 1, 0);
        AddAction(actionPanel, "Unmount", async () => await UnmountSelectedAsync(), 2, 0);
        AddAction(actionPanel, "Mount + Open", async () => await OpenDriveSelectedAsync(), 3, 0);
        AddAction(actionPanel, "Status Popup", async () => await ShowSelectedStatusPopupAsync(), 4, 0);
        AddAction(actionPanel, "SSH Terminal", () => { OpenSshTerminalSelected(); return Task.CompletedTask; }, 0, 1);
        AddAction(actionPanel, "VS Code", () => { OpenVsCodeSelected(); return Task.CompletedTask; }, 1, 1);
        AddAction(actionPanel, "Web", () => { OpenWebSelected(); return Task.CompletedTask; }, 2, 1);
        AddAction(actionPanel, "Open Config", () => { Process.Start("explorer.exe", _store.AppDir); return Task.CompletedTask; }, 3, 1);
        AddAction(actionPanel, "Hub", () => { OpenDeviceHub(); return Task.CompletedTask; }, 4, 1);
        AddAction(actionPanel, "Diagnose", async () => await DiagnoseSelectedAsync(), 0, 2);
        AddAction(actionPanel, "Favorites", () => { OpenFavoritePathSelected(); return Task.CompletedTask; }, 1, 2);
        AddAction(actionPanel, "Run Cmd", async () => await RunCommandPresetSelectedAsync(), 2, 2);
        AddAction(actionPanel, "Svc/Docker", async () => await ShowServiceDockerStatusAsync(), 3, 2);
        AddAction(actionPanel, "Backup", async () => await BackupSelectedAsync(), 4, 2);

        var outputGroup = new GroupBox { Text = "Output / Status", Dock = DockStyle.Fill, Padding = new Padding(8), Margin = new Padding(0, 0, 0, 6) };
        Theme.StyleGroup(outputGroup);
        right.Controls.Add(outputGroup, 0, 2);
        _output = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, ReadOnly = true, WordWrap = false };
        Theme.StyleReadout(_output);
        outputGroup.Controls.Add(_output);
    }

    private Panel BuildKeyPicker()
    {
        var panel = new Panel { Dock = DockStyle.Fill, AllowDrop = true, BackColor = Theme.Canvas };
        _sshKey = new TextBox { Dock = DockStyle.Fill, AllowDrop = true };
        Theme.StyleInput(_sshKey);
        var btnBrowse = MakeButton("...", 38, 28); btnBrowse.Dock = DockStyle.Right;
        var btnCopy = MakeButton("Copy", 56, 28, ButtonTone.Ghost); btnCopy.Dock = DockStyle.Right;
        btnBrowse.Click += (_, _) => BrowseAndImportKey();
        btnCopy.Click += (_, _) => CopyTypedKeyIntoStore();
        _sshKey.DragEnter += KeyDragEnter;
        _sshKey.DragDrop += KeyDragDrop;
        panel.DragEnter += KeyDragEnter;
        panel.DragDrop += KeyDragDrop;
        panel.Controls.Add(_sshKey);
        panel.Controls.Add(btnCopy);
        panel.Controls.Add(btnBrowse);
        return panel;
    }

    private void KeyDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
    }

    private void KeyDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            ImportKeyPathToSelected(files[0]);
    }

    private void BrowseAndImportKey()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select SSH private key",
            Filter = "SSH private keys|id_rsa;id_ed25519;*.pem;*.key;*.*|All files|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) ImportKeyPathToSelected(dialog.FileName);
    }

    private void CopyTypedKeyIntoStore()
    {
        var path = _sshKey.Text.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path)) return;
        ImportKeyPathToSelected(path);
    }

    private void ImportKeyPathToSelected(string sourcePath)
    {
        var p = SelectedProfile();
        if (p == null)
        {
            MessageBox.Show(this, "Select a profile first, or click New before importing an SSH key.", "RemoteDock", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            var copied = _store.ImportKeyForProfile(p, sourcePath);
            p.SshKeyPath = copied;
            _sshKey.Text = copied;
            _store.Save(_profiles);
            AppendOutput($"SSH key copied into profile store: {copied}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "SSH key import failed:\n" + ex.Message, "RemoteDock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AdjustLayout()
    {
        if (_root == null || _root.IsDisposed || _root.ColumnStyles.Count < 2) return;
        var rightWidth = Math.Min(700, Math.Max(640, ClientSize.Width / 3));
        if (ClientSize.Width < 1300) rightWidth = 640;
        _root.ColumnStyles[1].SizeType = SizeType.Absolute;
        _root.ColumnStyles[1].Width = rightWidth;
    }

    private static ThemedButton MakeButton(string text, int width, int height, ButtonTone tone = ButtonTone.Default)
    {
        return new ThemedButton
        {
            Text = text,
            Width = width,
            Height = height,
            Tone = tone,
            Margin = new Padding(3)
        };
    }

    private static void AddLabel(TableLayoutPanel parent, string text, int col, int row)
    {
        parent.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.InkSoft, BackColor = Theme.Canvas, Font = Theme.Label }, col, row);
    }

    private static TextBox AddTextField(TableLayoutPanel parent, string label, int col, int row)
    {
        AddLabel(parent, label, col, row);
        var tb = new TextBox { Dock = DockStyle.Fill };
        Theme.StyleInput(tb);
        parent.Controls.Add(tb, col + 1, row);
        return tb;
    }

    private void AddAction(TableLayoutPanel panel, string text, Func<Task> action, int col, int row)
    {
        var tone = text is "Mount" or "Mount + Open" ? ButtonTone.Primary : text is "Unmount" ? ButtonTone.Danger : ButtonTone.Default;
        var button = MakeButton(text, 0, 32, tone);
        button.Dock = DockStyle.Fill;
        button.Click += async (_, _) =>
        {
            try { await action(); }
            catch (Exception ex)
            {
                AppendOutput($"Action '{text}' failed: {ex.Message}");
                MessageBox.Show(this, ex.Message, "RemoteDock", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        panel.Controls.Add(button, col, row);
    }

    private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        // v15: row state chrome is painted in RowPostPaint so the border is a full rectangle, not a clipped partial cell outline.
    }

    private void Grid_RowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
        if (_grid.Rows[e.RowIndex].Tag is not DeviceProfile p) return;

        var status = _status.TryGetValue(p.Id, out var s) ? s : "Unknown";
        var mounted = IsDriveAvailable(NormalizeDrive(p.DriveLetter));
        var stateColor = mounted ? Theme.Online : status == "Online" ? Theme.Online : status == "Offline" ? Theme.Offline : Theme.Idle;

        var rowRect = new Rectangle(2, e.RowBounds.Top + 3, _grid.ClientSize.Width - 5, e.RowBounds.Height - 7);
        using (var pen = new Pen(stateColor, 2)) e.Graphics.DrawRectangle(pen, rowRect);

        using (var strip = new SolidBrush(Color.FromArgb(175, stateColor)))
            e.Graphics.FillRectangle(strip, new Rectangle(rowRect.Left + 2, rowRect.Top + 2, 5, rowRect.Height - 4));

        var dotSize = 10;
        var dotRect = new Rectangle(rowRect.Left + 14, rowRect.Top + (rowRect.Height - dotSize) / 2, dotSize, dotSize);
        using (var dot = new SolidBrush(stateColor)) e.Graphics.FillEllipse(dot, dotRect);
        using (var dotPen = new Pen(Theme.Surface, 1)) e.Graphics.DrawEllipse(dotPen, dotRect);
    }

    private void EnsureSortOrders()
    {
        var ordered = _profiles.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToList();
        var needsFix = ordered.Count > 1 && ordered.Select(p => p.SortOrder).Distinct().Count() < ordered.Count;
        if (!needsFix) return;
        for (var i = 0; i < ordered.Count; i++) ordered[i].SortOrder = i;
        _store.Save(_profiles);
    }

    private int NextSortOrder() => _profiles.Count == 0 ? 0 : _profiles.Max(p => p.SortOrder) + 1;

    private void Grid_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) { _dragRowIndex = -1; return; }
        var hit = _grid.HitTest(e.X, e.Y);
        if (hit.RowIndex < 0)
        {
            _dragRowIndex = -1;
            _grid.ClearSelection();
            try { _grid.CurrentCell = null; } catch { }
            return;
        }
        _dragRowIndex = hit.RowIndex;
    }

    private void Grid_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_dragRowIndex < 0 || e.Button != MouseButtons.Left) return;
        if (_dragRowIndex >= _grid.Rows.Count) return;
        _grid.DoDragDrop(_dragRowIndex, DragDropEffects.Move);
    }

    private void Grid_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(typeof(int)) is not int fromIndex) return;
        if (fromIndex < 0 || fromIndex >= _grid.Rows.Count) return;
        var clientPoint = _grid.PointToClient(new Point(e.X, e.Y));
        var hit = _grid.HitTest(clientPoint.X, clientPoint.Y);
        var toIndex = hit.RowIndex < 0 ? _grid.Rows.Count - 1 : hit.RowIndex;
        if (fromIndex == toIndex) return;
        MoveProfileRow(fromIndex, toIndex);
    }

    private void MoveProfileRow(int fromIndex, int toIndex)
    {
        if (_grid.Rows[fromIndex].Tag is not DeviceProfile moving) return;
        var ordered = _profiles.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToList();
        ordered.Remove(moving);
        toIndex = Math.Clamp(toIndex, 0, ordered.Count);
        ordered.Insert(toIndex, moving);
        for (var i = 0; i < ordered.Count; i++) ordered[i].SortOrder = i;
        _store.Save(_profiles);
        RefreshGrid();
        SelectProfile(moving.Id);
    }

    private DeviceProfile? SelectedProfile() => _grid.CurrentRow?.Tag as DeviceProfile;

    private void RefreshGrid()
    {
        var selectedId = SelectedProfile()?.Id;
        _grid.Columns.Clear();
        _grid.Rows.Clear();
        _grid.Columns.Add("State", "");
        _grid.Columns["State"].Width = 38;
        _grid.Columns.Add("Name", "Device / Mount");
        _grid.Columns.Add("Group", "Group");
        _grid.Columns.Add("Host", "Host");
        _grid.Columns.Add("Mount", "Mount");
        _grid.Columns.Add("Cpu", "CPU");
        _grid.Columns.Add("Ram", "RAM");
        _grid.Columns.Add("Disk", "DISK");
        _grid.Columns.Add("Drive", "Drive");
        foreach (var p in _profiles.OrderBy(p => p.SortOrder).ThenBy(p => p.Name))
        {
            var status = _status.TryGetValue(p.Id, out var s) ? s : "Unknown";
            var mounted = IsDriveAvailable(NormalizeDrive(p.DriveLetter));
            var dot = "";
            var display = DisplayName(p) + (string.IsNullOrWhiteSpace(p.MountName) ? "" : $"\n↳ {p.Name}");
            _metrics.TryGetValue(p.Id, out var m);
            var rowIndex = _grid.Rows.Add(dot, display, string.IsNullOrWhiteSpace(p.GroupName) ? "Default" : p.GroupName, p.Host, mounted ? "Mounted" : "Not mounted", PercentBarText(m?.CpuPercent), PercentBarText(m?.RamPercent), PercentBarText(m?.DiskPercent), NormalizeDrive(p.DriveLetter) + ":");
            var row = _grid.Rows[rowIndex];
            row.Tag = p;
            row.Cells[0].Style.ForeColor = status == "Online" ? Theme.Online : status == "Offline" ? Theme.Offline : Theme.Idle;
            row.Cells[0].Style.Font = new Font("Segoe UI", 1, FontStyle.Regular);
            row.DefaultCellStyle.BackColor = mounted ? Theme.OnlineSoft : status == "Offline" ? Theme.OfflineSoft : Theme.Surface;
        }
        if (!string.IsNullOrWhiteSpace(selectedId)) SelectProfile(selectedId);
    }

    private static string PercentText(int? value) => value.HasValue && value.Value >= 0 ? value.Value + "%" : "-";
    private static string PercentBarText(int? value)
    {
        if (!value.HasValue || value.Value < 0) return "-";
        var v = Math.Clamp(value.Value, 0, 100);
        var filled = Math.Clamp((int)Math.Round(v / 20.0), 0, 5);
        return $"{v,3}% {new string('■', filled)}{new string('□', 5 - filled)}";
    }
    private static string DisplayName(DeviceProfile p) => string.IsNullOrWhiteSpace(p.MountName) ? p.Name : p.MountName;

    private void LoadSelectedIntoForm()
    {
        var p = SelectedProfile();
        if (p == null) return;
        _name.Text = p.Name;
        _mountName.Text = p.MountName;
        _host.Text = p.Host;
        _port.Value = Math.Clamp(p.Port, 1, 65535);
        _user.Text = p.User;
        _remotePath.Text = p.RemotePath;
        _drive.Text = p.DriveLetter;
        _webUrl.Text = p.WebUrl;
        _vsCodePath.Text = p.VsCodeRemotePath;
        _sshKey.Text = p.SshKeyPath;
        _password.Text = string.IsNullOrWhiteSpace(p.EncryptedPasswordBase64) ? "" : "********";
        _deviceType.SelectedItem = string.IsNullOrWhiteSpace(p.DeviceType) ? "Auto" : p.DeviceType;
        _autoMount.Checked = p.AutoMount;
    }

    private void NewProfile()
    {
        var p = new DeviceProfile { Name = "New Device", MountName = "", GroupName = "Default", DriveLetter = NextDriveLetter(), SortOrder = NextSortOrder() };
        _profiles.Add(p);
        _store.Save(_profiles);
        RefreshGrid();
        SelectProfile(p.Id);
    }

    private void SelectProfile(string id)
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is DeviceProfile p && p.Id == id)
            {
                row.Selected = true;
                _grid.CurrentCell = row.Cells[Math.Min(1, row.Cells.Count - 1)];
                break;
            }
        }
    }

    private void DeleteSelected()
    {
        var p = SelectedProfile();
        if (p == null) return;
        if (MessageBox.Show($"Delete profile '{p.Name}'?", "RemoteDock", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _profiles.Remove(p);
        _status.Remove(p.Id);
        _metrics.Remove(p.Id);
        _store.Save(_profiles);
        RefreshGrid();
    }

    private void SaveCurrent()
    {
        var p = SelectedProfile();
        if (p == null) return;
        p.Name = _name.Text.Trim();
        p.MountName = _mountName.Text.Trim();
        p.Host = _host.Text.Trim();
        p.Port = (int)_port.Value;
        p.User = _user.Text.Trim();
        p.RemotePath = string.IsNullOrWhiteSpace(_remotePath.Text) ? "/" : _remotePath.Text.Trim();
        p.DriveLetter = NormalizeDrive(_drive.Text);
        p.WebUrl = _webUrl.Text.Trim();
        p.VsCodeRemotePath = _vsCodePath.Text.Trim();
        p.DeviceType = _deviceType.SelectedItem?.ToString() ?? "Auto";
        p.AutoMount = _autoMount.Checked;
        var keyText = _sshKey.Text.Trim().Trim('"');
        if (!string.IsNullOrWhiteSpace(keyText) && File.Exists(keyText) && !_store.IsInsideKeyStore(keyText))
        {
            try { keyText = _store.ImportKeyForProfile(p, keyText); AppendOutput($"SSH key copied into profile store: {keyText}"); }
            catch (Exception ex) { AppendOutput("SSH key copy failed: " + ex.Message); }
        }
        p.SshKeyPath = keyText;
        if (!string.IsNullOrWhiteSpace(p.SshKeyPath) && File.Exists(p.SshKeyPath)) ProfileStore.TryFixPrivateKeyAcl(p.SshKeyPath);
        _sshKey.Text = p.SshKeyPath;
        if (!string.IsNullOrWhiteSpace(_password.Text) && _password.Text != "********") p.EncryptedPasswordBase64 = ProfileStore.EncryptPassword(_password.Text);
        _store.Save(_profiles);
        AppendOutput($"Saved: {p.Name}");
        RefreshGrid();
        SelectProfile(p.Id);
    }

    private async Task CheckAllStatusesAsync(bool verbose)
    {
        foreach (var p in _profiles.ToList())
        {
            await CheckStatusAsync(p, verbose);
            if (_status.TryGetValue(p.Id, out var s) && s == "Online" && p.DeviceType != "Network/Web")
            {
                try { _metrics[p.Id] = await FetchMetricsAsync(p); }
                catch { }
            }
            if (_status.TryGetValue(p.Id, out s) && s == "Online" && p.AutoMount && !IsDriveAvailable(NormalizeDrive(p.DriveLetter)))
                await MountProfileAsync(p, interactive: false);
        }
        RefreshGrid();
        _lastAutoCheck = DateTime.Now;
        UpdateStatusBar();
    }

    private async Task CheckSelectedStatusAsync(bool verbose)
    {
        var p = SelectedProfile();
        if (p == null) return;
        await RunBusyAsync("Checking", $"Checking {DisplayName(p)}...", async () =>
        {
            await CheckStatusAsync(p, verbose);
            if (_status.TryGetValue(p.Id, out var st) && st == "Online" && p.DeviceType != "Network/Web")
            {
                var m = await FetchMetricsAsync(p);
                _metrics[p.Id] = m;
            }
        });
        RefreshGrid();
        SelectProfile(p.Id);
        _lastAutoCheck = DateTime.Now;
        UpdateStatusBar();
    }

    private async Task CheckStatusAsync(DeviceProfile p, bool verbose)
    {
        if (string.IsNullOrWhiteSpace(p.Host)) { _status[p.Id] = "No host"; return; }
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(p.Host, p.Port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(3000));
            _status[p.Id] = completed == connectTask && client.Connected ? "Online" : "Offline";
        }
        catch { _status[p.Id] = "Offline"; }
        if (verbose) AppendOutput($"[{DisplayName(p)}] {p.Host}:{p.Port} = {_status[p.Id]}");
    }

    private async Task MountSelectedAsync()
    {
        var p = SelectedProfile();
        if (p == null) return;
        await RunBusyAsync("Mounting", $"Mounting {DisplayName(p)}...", async () => await MountProfileAsync(p, interactive: true));
    }

    private async Task<bool> MountProfileAsync(DeviceProfile p, bool interactive)
    {
        var drive = NormalizeDrive(p.DriveLetter);
        if (string.IsNullOrWhiteSpace(drive)) return WarnMount(interactive, "Drive Letter is empty. Enter a letter like R and save the profile.");
        if (string.IsNullOrWhiteSpace(p.Host)) return WarnMount(interactive, "Host is empty. Enter an IP address or hostname and save the profile.");
        if (string.IsNullOrWhiteSpace(p.User)) return WarnMount(interactive, "User is empty. Enter the SSH user and save the profile.");
        if (IsDriveAvailable(drive))
        {
            await ApplyExplorerLabelAsync(drive, BuildSshfsUnc(p, ResolveMountHostForSshfs(p.Host), false, out _), DisplayName(p));
            if (interactive) CelebrateMount(p, drive, already: true);
            return true;
        }

        var password = ProfileStore.DecryptPassword(p.EncryptedPasswordBase64);
        var useKeyAuth = string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(p.SshKeyPath);
        var mountHost = ResolveMountHostForSshfs(p.Host);
        var unc = BuildSshfsUnc(p, mountHost, useKeyAuth, out var mountNote);

        var cleanup = await RunProcessResultAsync("net", $"use {drive}: /delete /y", 10_000);
        if (cleanup.ExitCode == 0) AppendOutput($"Cleared existing network mapping: {drive}:");

        var args = string.IsNullOrWhiteSpace(password) ? $"use {drive}: \"{unc}\" /persistent:no" : $"use {drive}: \"{unc}\" \"{password}\" /persistent:no";
        var displayArgs = string.IsNullOrEmpty(password) ? args : args.Replace(password, "********");
        AppendOutput($"Mount target: {drive}: -> {unc}");
        if (!string.Equals(mountHost, p.Host, StringComparison.OrdinalIgnoreCase)) AppendOutput($"Mount host resolved: {p.Host} -> {mountHost}");
        AppendOutput(mountNote);
        if (useKeyAuth && !IsDefaultSshKey(p.SshKeyPath)) AppendOutput("Note: SSHFS-Win key provider usually uses the default OpenSSH key/config. Status/SSH Terminal uses the copied key path directly, but drive mount may still require password or SSH config alias.");
        AppendOutput($"> net {displayArgs}");

        var result = await RunProcessResultAsync("net", args, 30_000);
        AppendOutput(FormatProcessResult(result));
        var netUseCombined = ((result.Output ?? "") + "\n" + (result.Error ?? "")).Trim();
        var netUseGuidance = RemoteDock.Services.MountService.DescribeNetUseError(netUseCombined);
        if (!string.IsNullOrWhiteSpace(netUseGuidance)) AppendOutput("Mount guidance: " + netUseGuidance);
        await Task.Delay(700);
        var ok = result.ExitCode == 0 && IsDriveAvailable(drive);
        if (ok)
        {
            await ApplyExplorerLabelAsync(drive, unc, DisplayName(p));
            if (interactive) CelebrateMount(p, drive, already: false);
        }
        else
        {
            if (interactive)
            {
                MessageBox.Show(this,
                    $"Mount did not complete.\n\nTarget: {drive}: -> {unc}\n\n{(string.IsNullOrWhiteSpace(netUseGuidance) ? "Check Output / Status for the net use result." : netUseGuidance)}",
                    "RemoteDock Mount Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                var now = DateTime.Now;
                if (!_lastAutoMountFailLog.TryGetValue(p.Id, out var last) || (now - last).TotalMinutes > 5)
                {
                    _lastAutoMountFailLog[p.Id] = now;
                    AppendOutput($"Auto mount failed silently for {DisplayName(p)}. Popups are suppressed during Auto Detect.");
                }
            }
        }
        RefreshGrid();
        SelectProfile(p.Id);
        return ok;
    }

    private async Task ApplyExplorerLabelAsync(string drive, string unc, string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return;
        label = SanitizeExplorerLabel(label);
        try
        {
            var keyName = "##" + unc.Trim('\\').Replace('\\', '#');
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\MountPoints2\" + keyName);
            key?.SetValue("_LabelFromReg", label, RegistryValueKind.String);
            using var driveKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\DriveIcons\" + drive + @"\DefaultLabel");
            driveKey?.SetValue("", label, RegistryValueKind.String);
            AppendOutput($"Explorer label requested: {drive}: = {label}");
        }
        catch (Exception ex)
        {
            AppendOutput("Explorer label registry update failed: " + ex.Message);
        }

        try
        {
            var labelResult = await RunProcessResultAsync("cmd", $"/c label {drive}: \"{label}\"", 5000);
            if (labelResult.ExitCode == 0) AppendOutput($"Drive label command accepted: {label}");
        }
        catch { }
    }

    private static string SanitizeExplorerLabel(string value)
    {
        var invalid = new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
        var clean = new string((value ?? "").Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        if (clean.Length > 32) clean = clean[..32];
        return string.IsNullOrWhiteSpace(clean) ? "RemoteDock" : clean;
    }

    private bool WarnMount(bool interactive, string text)
    {
        AppendOutput(text);
        if (interactive) MessageBox.Show(this, text, "RemoteDock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private void CelebrateMount(DeviceProfile p, string drive, bool already)
    {
        var name = DisplayName(p);
        var msg = already ? $"{name} is already mounted at {drive}: 😎" : $"{name} mounted at {drive}:  🐾🎉";
        AppendOutput(msg);
        _tray.ShowBalloonTip(2500, "RemoteDock Mount Success", msg, ToolTipIcon.Info);
        MessageBox.Show(this, msg, "Mounted", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task UnmountSelectedAsync()
    {
        var p = SelectedProfile();
        if (p == null) return;
        await RunBusyAsync("Unmounting", $"Unmounting {DisplayName(p)}...", async () =>
        {
            var drive = NormalizeDrive(p.DriveLetter);
            if (string.IsNullOrWhiteSpace(drive)) return;
            var result = await RunProcessTextAsync("net", $"use {drive}: /delete /y", 15_000);
            AppendOutput(result);
            RefreshGrid();
            SelectProfile(p.Id);
        });
    }

    private async Task OpenDriveSelectedAsync()
    {
        var p = SelectedProfile();
        if (p == null) return;
        var drive = NormalizeDrive(p.DriveLetter);
        if (string.IsNullOrWhiteSpace(drive)) { MessageBox.Show(this, "Drive Letter is empty.", "RemoteDock", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (!IsDriveAvailable(drive))
        {
            var mounted = false;
            await RunBusyAsync("Mounting", $"Mounting and opening {DisplayName(p)}...", async () => mounted = await MountProfileAsync(p, interactive: true));
            if (!mounted) return;
        }
        var path = drive + @":\";
        if (!Directory.Exists(path)) { MessageBox.Show(this, $"{path} is not available yet.", "RemoteDock", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        Process.Start("explorer.exe", path);
    }

    private void OpenSshTerminalSelected()
    {
        var p = SelectedProfile();
        if (p == null) return;
        var sshArgs = BuildSshArgs(p, "");
        if (CommandExists("wt.exe")) Process.Start(new ProcessStartInfo("wt.exe", $"ssh {sshArgs}") { UseShellExecute = true });
        else Process.Start(new ProcessStartInfo("cmd.exe", $"/k ssh {sshArgs}") { UseShellExecute = true });
    }

    private void OpenVsCodeSelected()
    {
        var p = SelectedProfile();
        if (p == null) return;
        var path = string.IsNullOrWhiteSpace(p.VsCodeRemotePath) ? p.RemotePath : p.VsCodeRemotePath;
        var hostAlias = string.IsNullOrWhiteSpace(p.User) ? p.Host : $"{p.User}@{p.Host}";
        var args = $"--remote ssh-remote+{QuoteArg(hostAlias)} {QuoteArg(path)}";
        try { Process.Start(new ProcessStartInfo("code", args) { UseShellExecute = true }); }
        catch { AppendOutput("VS Code CLI 'code' was not found. Install VS Code and enable 'code' command."); }
    }

    private void OpenWebSelected()
    {
        var p = SelectedProfile();
        if (p == null || string.IsNullOrWhiteSpace(p.WebUrl)) return;
        Process.Start(new ProcessStartInfo(p.WebUrl) { UseShellExecute = true });
    }

    private async Task ShowSelectedStatusPopupAsync()
    {
        var p = SelectedProfile();
        if (p == null) return;
        var dialog = new StatusPopupForm(DisplayName(p));
        dialog.SetLoading("Loading live dashboard... SSH key/agent authentication is recommended.");
        dialog.Show(this);
        CenterOwnedDialog(dialog);
        var metrics = await FetchMetricsAsync(p);
        _metrics[p.Id] = metrics;
        if (!dialog.IsDisposed) dialog.SetMetrics(metrics);
        RefreshGrid();
        SelectProfile(p.Id);
    }

    private async Task<StatusMetrics> FetchMetricsAsync(DeviceProfile p)
    {
        var m = new StatusMetrics { Mounted = IsDriveAvailable(NormalizeDrive(p.DriveLetter)) };
        await CheckStatusAsync(p, false);
        m.StatusText = _status.TryGetValue(p.Id, out var st) ? st : "Unknown";
        m.Online = m.StatusText == "Online";
        if (!m.Online || p.DeviceType == "Network/Web") return m;

        var type = p.DeviceType;
        if (type == "Auto")
        {
            var uname = await RunSshCommandAsync(p, "uname -s", 10_000);
            type = uname.ExitCode == 0 && uname.Output.Trim().Length > 0 ? "Linux" : "Windows";
        }
        var command = type == "Windows" ? WindowsMetricsCommand() : LinuxMetricsCommand();
        var result = await RunSshCommandAsync(p, command, 20_000);
        m.RawText = $"Command exit code: {result.ExitCode}\n{result.Output}";
        m.ErrorText = result.Error;
        if (result.ExitCode != 0 && string.IsNullOrWhiteSpace(result.Output)) return m;
        ParseMetrics(result.Output, m);
        return m;
    }

    private static void ParseMetrics(string output, StatusMetrics m)
    {
        var inProcesses = false;
        var proc = new StringBuilder();
        double load = -1;
        int cores = 1;
        foreach (var rawLine in output.Replace("\r", "").Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line == "PROCESSES_BEGIN") { inProcesses = true; continue; }
            if (inProcesses) { proc.AppendLine(line); continue; }
            var idx = line.IndexOf('=');
            if (idx < 0) continue;
            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();
            switch (key)
            {
                case "HOST": m.HostName = value; break;
                case "UPTIME": m.Uptime = value; break;
                case "LOAD": double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out load); break;
                case "CPU": if (int.TryParse(value, out var cpu)) m.CpuPercent = Math.Clamp(cpu, 0, 100); break;
                case "CORES": int.TryParse(value, out cores); if (cores <= 0) cores = 1; break;
                case "RAM": if (int.TryParse(value, out var ram)) m.RamPercent = Math.Clamp(ram, 0, 100); break;
                case "RAM_USED": m.RamText = value + " / " + m.RamText; break;
                case "RAM_TOTAL": m.RamText = m.RamText.EndsWith(" / ") ? m.RamText + value : m.RamText + value; break;
                case "DISK": if (int.TryParse(value, out var disk)) m.DiskPercent = Math.Clamp(disk, 0, 100); break;
                case "DISK_USED": m.DiskText = value + " used, " + m.DiskText; break;
                case "DISK_AVAIL": m.DiskText += value + " free"; break;
            }
        }
        if (m.CpuPercent < 0 && load >= 0) m.CpuPercent = Math.Clamp((int)Math.Round(load / Math.Max(1, cores) * 100), 0, 100);
        m.ProcessText = proc.ToString().Trim();
    }

    private static string LinuxMetricsCommand()
    {
        return "printf 'HOST='; hostname; " +
               "printf 'UPTIME='; uptime -p 2>/dev/null || uptime; " +
               "printf 'LOAD='; cut -d' ' -f1 /proc/loadavg; " +
               "printf 'CORES='; nproc 2>/dev/null || echo 1; " +
               "free -m | awk '/Mem:/ {printf \"RAM=%d\\nRAM_USED=%dMB\\nRAM_TOTAL=%dMB\\n\", ($3*100)/$2, $3, $2}'; " +
               "df -P -m / | awk 'NR==2 {gsub(\"%\",\"\",$5); printf \"DISK=%d\\nDISK_USED=%dMB\\nDISK_AVAIL=%dMB\\nDISK_TOTAL=%dMB\\n\", $5,$3,$4,$2}'; " +
               "echo PROCESSES_BEGIN; ps -eo pid,comm,%cpu,%mem --sort=-%cpu | head -16";
    }

    private static string WindowsMetricsCommand()
    {
        return "powershell -NoProfile -Command \"" +
               "$os=Get-CimInstance Win32_OperatingSystem; " +
               "$cpu=(Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average; " +
               "$ram=[math]::Round((($os.TotalVisibleMemorySize-$os.FreePhysicalMemory)/$os.TotalVisibleMemorySize)*100); " +
               "$disk=Get-CimInstance Win32_LogicalDisk -Filter 'DeviceID=\\\"C:\\\"'; " +
               "$du=[math]::Round((($disk.Size-$disk.FreeSpace)/$disk.Size)*100); " +
               "Write-Output ('HOST=' + $env:COMPUTERNAME); " +
               "Write-Output ('UPTIME=' + ((Get-Date)-$os.LastBootUpTime)); " +
               "Write-Output ('LOAD=0'); Write-Output ('CORES=1'); " +
               "Write-Output ('CPU=' + [int]$cpu); Write-Output ('RAM=' + [int]$ram); Write-Output ('DISK=' + [int]$du); " +
               "Write-Output 'PROCESSES_BEGIN'; Get-Process | Sort-Object CPU -Descending | Select-Object -First 15 Id,ProcessName,CPU,WorkingSet | Format-Table -AutoSize\"";
    }

    private async Task<(int ExitCode, string Output, string Error)> RunSshCommandAsync(DeviceProfile p, string command, int timeoutMs)
    {
        var sshArgs = BuildSshArgs(p, command);
        return await RunProcessResultAsync("ssh", sshArgs, timeoutMs);
    }

    private static string BuildSshArgs(DeviceProfile p, string remoteCommand)
    {
        var sb = new StringBuilder();
        sb.Append("-o BatchMode=yes -o ConnectTimeout=7 ");
        sb.Append("-p ").Append(p.Port).Append(' ');
        if (!string.IsNullOrWhiteSpace(p.SshKeyPath))
        {
            ProfileStore.TryFixPrivateKeyAcl(p.SshKeyPath);
            sb.Append("-i ").Append(QuoteArg(p.SshKeyPath)).Append(' ');
        }
        var target = string.IsNullOrWhiteSpace(p.User) ? p.Host : $"{p.User}@{p.Host}";
        sb.Append(QuoteArg(target));
        if (!string.IsNullOrWhiteSpace(remoteCommand)) sb.Append(' ').Append(QuoteArg(remoteCommand));
        return sb.ToString();
    }

    private static string BuildSshfsUnc(DeviceProfile p, string mountHost, bool useKeyAuth, out string note)
    {
        // Correct SSHFS-Win provider synthesis:
        // password + home-relative -> sshfs
        // key      + home-relative -> sshfs.k
        // password + absolute      -> sshfs.r
        // key      + absolute      -> sshfs.kr
        // The older v12 inline code could generate sshfs.k.r, which SSHFS-Win does not recognize.
        var user = string.IsNullOrWhiteSpace(p.User) ? "user" : p.User;
        var portPart = p.Port == 22 ? "" : "!" + p.Port;
        var rawPath = (string.IsNullOrWhiteSpace(p.RemotePath) ? "" : p.RemotePath).Trim();
        var isAbsolutePath = rawPath.StartsWith("/") || rawPath.StartsWith("\\");
        var suffix = (useKeyAuth ? "k" : "") + (isAbsolutePath ? "r" : "");
        var provider = suffix.Length > 0 ? "sshfs." + suffix : "sshfs";
        var path = rawPath.Replace('/', '\\').TrimStart('\\');
        var baseUnc = $@"\\{provider}\{user}@{mountHost}{portPart}";
        note = $"SSHFS mode: {provider} ({(isAbsolutePath ? "server-root path" : "home-relative path")}, {(useKeyAuth ? "key auth" : "password auth")})";
        return string.IsNullOrWhiteSpace(path) ? baseUnc : $@"{baseUnc}\{path}";
    }

    private static string ResolveMountHostForSshfs(string host)
    {
        var trimmed = (host ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return trimmed;
        if (IPAddress.TryParse(trimmed, out _)) return trimmed;
        try
        {
            var ipv4 = Dns.GetHostAddresses(trimmed).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (ipv4 != null) return ipv4.ToString();
        }
        catch { }
        return trimmed;
    }

    private static bool IsDefaultSshKey(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var full = Path.GetFullPath(path);
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var idRsa = Path.Combine(home, ".ssh", "id_rsa");
            var idEd = Path.Combine(home, ".ssh", "id_ed25519");
            return string.Equals(full, idRsa, StringComparison.OrdinalIgnoreCase) || string.Equals(full, idEd, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private void CenterOwnedDialog(Form dialog)
    {
        try
        {
            var screen = Screen.FromControl(this).WorkingArea;
            var x = Left + (Width - dialog.Width) / 2;
            var y = Top + (Height - dialog.Height) / 2;
            x = Math.Max(screen.Left, Math.Min(x, screen.Right - dialog.Width));
            y = Math.Max(screen.Top, Math.Min(y, screen.Bottom - dialog.Height));
            dialog.StartPosition = FormStartPosition.Manual;
            dialog.Location = new Point(x, y);
        }
        catch { }
    }

    private async Task RunBusyAsync(string title, string message, Func<Task> work)
    {
        using var busy = new BusyForm(title, message);
        busy.Show(this);
        busy.Refresh();
        try { await work(); }
        finally { if (!busy.IsDisposed) busy.Close(); }
    }

    private async Task OpenDiscoveryAsync()
    {
        using var form = new DiscoveryForm(result =>
        {
            var p = new DeviceProfile
            {
                Name = result.SuggestedName,
                MountName = result.SuggestedName,
                Host = result.IpAddress,
                WebUrl = result.Services.Contains("HTTPS") ? $"https://{result.IpAddress}" : result.Services.Contains("HTTP") ? $"http://{result.IpAddress}" : "",
                DeviceType = result.Services.Contains("SSH") ? "Auto" : "Network/Web",
                RemotePath = result.Services.Contains("SSH") ? "/home" : "/",
                DriveLetter = NextDriveLetter(),
                SortOrder = NextSortOrder(),
                GroupName = "Discovered"
            };
            _profiles.Add(p);
            _store.Save(_profiles);
            RefreshGrid();
            SelectProfile(p.Id);
        });
        form.Show(this);
        await form.StartScanAsync();
    }

    private static async Task<string> RunProcessTextAsync(string fileName, string args, int timeoutMs)
    {
        var result = await RunProcessResultAsync(fileName, args, timeoutMs);
        return FormatProcessResult(result);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunProcessResultAsync(string fileName, string args, int timeoutMs)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            try
            {
                var enc = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
                process.StartInfo.StandardOutputEncoding = enc;
                process.StartInfo.StandardErrorEncoding = enc;
            }
            catch { }
            var output = new StringBuilder();
            var error = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) error.AppendLine(e.Data); };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            var waitTask = process.WaitForExitAsync();
            var completed = await Task.WhenAny(waitTask, Task.Delay(timeoutMs));
            if (completed != waitTask)
            {
                try { process.Kill(true); } catch { }
                return (-1, output.ToString().Trim(), "Timed out.");
            }
            await waitTask;
            return (process.ExitCode, output.ToString().Trim(), error.ToString().Trim());
        }
        catch (Exception ex) { return (-1, "", ex.Message); }
    }

    private static bool IsDriveAvailable(string drive)
    {
        drive = NormalizeDrive(drive);
        if (string.IsNullOrWhiteSpace(drive)) return false;
        try { if (Directory.Exists(drive + @":\")) return true; } catch { }
        try { return DriveInfo.GetDrives().Any(d => d.Name.StartsWith(drive + ":", StringComparison.OrdinalIgnoreCase)); } catch { return false; }
    }

    private static string FormatProcessResult((int ExitCode, string Output, string Error) result)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(result.Output)) sb.AppendLine(result.Output);
        if (!string.IsNullOrWhiteSpace(result.Error)) sb.AppendLine(result.Error);
        sb.AppendLine($"ExitCode: {result.ExitCode}");
        return sb.ToString().Trim();
    }

    private static string NormalizeDrive(string value)
    {
        var trimmed = (value ?? "").Trim().TrimEnd(':');
        if (trimmed.Length == 0) return "";
        return trimmed[0].ToString().ToUpperInvariant();
    }

    private static string NextDriveLetter()
    {
        var used = DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])).ToHashSet();
        for (var c = 'R'; c <= 'Z'; c++) if (!used.Contains(c)) return c.ToString();
        return "R";
    }

    private static string QuoteArg(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static bool CommandExists(string command)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("where", command) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true });
            p?.WaitForExit(1500);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }


    private void OpenDeviceHub()
    {
        var p = SelectedProfile();
        if (p == null) return;
        using var hub = new DeviceHubForm(p, () => { _store.Save(_profiles); RefreshGrid(); SelectProfile(p.Id); AppendOutput("Hub settings saved: " + DisplayName(p)); });
        hub.ShowDialog(this);
    }

    private void ExportProfilesBundle()
    {
        try
        {
            using var dialog = new SaveFileDialog { Title = "Export RemoteDock profiles", Filter = "RemoteDock JSON|*.json", FileName = "RemoteDock_profiles_export.json" };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            var export = JsonSerializer.Serialize(_profiles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dialog.FileName, export, Encoding.UTF8);
            AppendOutput("Profiles exported: " + dialog.FileName);
            MessageBox.Show(this, "Profiles exported.\n\n" + dialog.FileName, "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private async Task DiagnoseSelectedAsync()
    {
        var p = SelectedProfile();
        if (p == null) return;
        var dialog = new DiagnosticForm("Mount Diagnostics - " + DisplayName(p));
        dialog.SetText("Running diagnostics...");
        dialog.Show(this);
        var report = await BuildDiagnosticReportAsync(p);
        if (!dialog.IsDisposed) dialog.SetText(report);
    }

    private async Task<string> BuildDiagnosticReportAsync(DeviceProfile p)
    {
        var sb = new StringBuilder();
        sb.AppendLine("RemoteDock Mount Diagnostics");
        sb.AppendLine("============================");
        sb.AppendLine($"Device: {DisplayName(p)}");
        sb.AppendLine($"Host: {p.Host}:{p.Port}");
        sb.AppendLine($"User: {p.User}");
        sb.AppendLine($"Remote path: {p.RemotePath}");
        sb.AppendLine($"Drive: {NormalizeDrive(p.DriveLetter)}:");
        sb.AppendLine();
        var drive = NormalizeDrive(p.DriveLetter);
        bool driveMounted = IsDriveAvailable(drive);
        sb.AppendLine((driveMounted ? "✅" : "⚪") + $" Drive mounted: {driveMounted}");
        try { sb.AppendLine((CommandExists("ssh") ? "✅" : "❌") + " OpenSSH client found"); } catch { sb.AppendLine("❌ OpenSSH check failed"); }
        var winfsp = Directory.Exists(@"C:\Program Files (x86)\WinFsp") || Directory.Exists(@"C:\Program Files\WinFsp");
        sb.AppendLine((winfsp ? "✅" : "❌") + " WinFsp install folder check");
        var sshfsWin = Directory.Exists(@"C:\Program Files\SSHFS-Win") || Directory.Exists(@"C:\Program Files (x86)\SSHFS-Win");
        sb.AppendLine((sshfsWin ? "✅" : "❌") + " SSHFS-Win install folder check");
        sb.AppendLine();
        await CheckStatusAsync(p, false);
        var online = _status.TryGetValue(p.Id, out var st) && st == "Online";
        sb.AppendLine((online ? "✅" : "❌") + $" TCP {p.Host}:{p.Port} = {st}");
        var mountHost = ResolveMountHostForSshfs(p.Host);
        var password = ProfileStore.DecryptPassword(p.EncryptedPasswordBase64);
        var useKeyAuth = string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(p.SshKeyPath);
        var unc = BuildSshfsUnc(p, mountHost, useKeyAuth, out var note);
        sb.AppendLine($"UNC: {unc}");
        sb.AppendLine(note);
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(p.SshKeyPath))
        {
            var exists = File.Exists(p.SshKeyPath);
            sb.AppendLine((exists ? "✅" : "❌") + " SSH key copy exists: " + p.SshKeyPath);
            if (exists) ProfileStore.TryFixPrivateKeyAcl(p.SshKeyPath);
        }
        var remoteCheck = await RunSshCommandAsync(p, "test -e " + QuoteArg(p.RemotePath) + " && echo PATH_OK || echo PATH_MISSING", 10000);
        sb.AppendLine((remoteCheck.Output.Contains("PATH_OK") ? "✅" : "❌") + " Remote path exists over SSH");
        if (!string.IsNullOrWhiteSpace(remoteCheck.Error)) sb.AppendLine("SSH note: " + remoteCheck.Error);
        sb.AppendLine();
        sb.AppendLine("Suggested manual test:");
        sb.AppendLine($"net use {drive}: \"{unc}\" /persistent:no");
        return sb.ToString();
    }

    private void OpenFavoritePathSelected()
    {
        var p = SelectedProfile();
        if (p == null) return;
        var items = SplitLines(p.FavoritePathsText).ToList();
        if (items.Count == 0) { MessageBox.Show(this, "No favorite paths. Open Hub > Favorite Paths first.", "RemoteDock", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        using var picker = new ListPickerForm("Favorite Paths - " + DisplayName(p), items);
        if (picker.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(picker.SelectedValueText)) return;
        var drive = NormalizeDrive(p.DriveLetter);
        if (!IsDriveAvailable(drive)) { MessageBox.Show(this, "Drive is not mounted yet.", "RemoteDock", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var rel = picker.SelectedValueText.Trim().Replace('/', '\\').TrimStart('\\');
        if (rel.Contains("=")) rel = rel[(rel.IndexOf('=') + 1)..].Trim().Replace('/', '\\').TrimStart('\\');
        var root = drive + @":\";
        var path = Path.Combine(root, rel);
        if (!Directory.Exists(path)) path = root;
        Process.Start("explorer.exe", path);
    }

    private async Task RunCommandPresetSelectedAsync()
    {
        var p = SelectedProfile();
        if (p == null) return;
        var items = SplitLines(p.CommandPresetsText).ToList();
        if (items.Count == 0) { MessageBox.Show(this, "No command presets. Open Hub > Command Presets first.", "RemoteDock", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        using var picker = new ListPickerForm("Command Presets - " + DisplayName(p), items);
        if (picker.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(picker.SelectedValueText)) return;
        var line = picker.SelectedValueText.Trim();
        var command = line.Contains('=') ? line[(line.IndexOf('=') + 1)..].Trim() : line;
        await RunBusyAsync("Running", "Cat is running command...", async () =>
        {
            var r = await RunSshCommandAsync(p, command, 60000);
            AppendOutput("Command: " + command);
            AppendOutput(FormatProcessResult(r));
        });
    }

    private async Task ShowServiceDockerStatusAsync()
    {
        var p = SelectedProfile();
        if (p == null) return;
        var sb = new StringBuilder();
        await RunBusyAsync("Service Check", "Cat is checking services and containers...", async () =>
        {
            foreach (var svc in SplitLines(p.ServicesText))
            {
                var service = svc.Trim();
                var r = await RunSshCommandAsync(p, $"systemctl is-active {service} 2>/dev/null || true", 10000);
                sb.AppendLine($"{service}: {r.Output.Trim()}");
            }
            if (!string.IsNullOrWhiteSpace(p.DockerComposePath))
            {
                var cmd = $"cd {QuoteArg(p.DockerComposePath)} && (docker compose ps 2>/dev/null || docker ps --format 'table {{{{.Names}}}}\\t{{{{.Status}}}}')";
                var r = await RunSshCommandAsync(p, cmd, 20000);
                sb.AppendLine(); sb.AppendLine("Docker:"); sb.AppendLine(r.Output); if (!string.IsNullOrWhiteSpace(r.Error)) sb.AppendLine(r.Error);
            }
        });
        using var d = new DiagnosticForm("Services / Docker - " + DisplayName(p));
        d.SetText(sb.Length == 0 ? "No services or docker path configured. Open Hub > Services / Docker." : sb.ToString());
        d.ShowDialog(this);
    }

    private async Task BackupSelectedAsync()
    {
        var p = SelectedProfile();
        if (p == null) return;
        if (string.IsNullOrWhiteSpace(p.BackupRemotePath) || string.IsNullOrWhiteSpace(p.BackupLocalPath))
        {
            MessageBox.Show(this, "Set Backup Remote/Local paths in Hub > Backup first.", "RemoteDock", MessageBoxButtons.OK, MessageBoxIcon.Information); return;
        }
        Directory.CreateDirectory(p.BackupLocalPath);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var name = SanitizeExplorerLabel(DisplayName(p)) + "_" + stamp + ".tar.gz";
        var local = Path.Combine(p.BackupLocalPath, name);
        await RunBusyAsync("Backup", "Cat is packing backup...", async () =>
        {
            var tarCmd = $"tar -czf - -C {QuoteArg(Path.GetDirectoryName(p.BackupRemotePath.TrimEnd('/')) ?? "/")} {QuoteArg(Path.GetFileName(p.BackupRemotePath.TrimEnd('/')))}";
            var sshArgs = BuildSshArgs(p, tarCmd);
            await RunProcessToFileAsync("ssh", sshArgs, local, 120000);
        });
        AppendOutput("Backup saved: " + local);
        MessageBox.Show(this, "Backup saved.\n\n" + local, "Backup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static async Task RunProcessToFileAsync(string fileName, string args, string outputFile, int timeoutMs)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo { FileName = fileName, Arguments = args, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        process.Start();
        await using (var fs = File.Create(outputFile)) await process.StandardOutput.BaseStream.CopyToAsync(fs);
        var waitTask = process.WaitForExitAsync();
        var completed = await Task.WhenAny(waitTask, Task.Delay(timeoutMs));
        if (completed != waitTask) { try { process.Kill(true); } catch { } throw new TimeoutException("Backup timed out."); }
        await waitTask;
        if (process.ExitCode != 0) throw new Exception(await process.StandardError.ReadToEndAsync());
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        return (text ?? "").Replace("\r", "").Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0 && !x.StartsWith("#"));
    }

    private void UpdateStatusBar()
    {
        if (_statusLabel == null) return;
        var online = _status.Values.Count(v => v == "Online");
        var offline = _status.Values.Count(v => v == "Offline");
        var noHost = _status.Values.Count(v => v == "No host");
        var mounted = _profiles.Count(p => IsDriveAvailable(NormalizeDrive(p.DriveLetter)));
        var last = _lastAutoCheck == DateTime.MinValue ? "never" : _lastAutoCheck.ToString("HH:mm:ss");
        var auto = _autoDetect != null && _autoDetect.Checked ? "ON" : "OFF";
        var seconds = _autoDetectSeconds == null ? 180 : (int)_autoDetectSeconds.Value;
        _statusLabel.Text = $"Auto Detect: {auto} / every {seconds}s   |   Last: {last}   |   Online: {online}  Offline: {offline}  No host: {noHost}  Mounted: {mounted}   |   Double-click = visual status popup";
    }

    private void AppendOutput(string text)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string>(AppendOutput), text); return; }
        if (string.IsNullOrWhiteSpace(text)) return;
        _output.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}{Environment.NewLine}");
    }
}
