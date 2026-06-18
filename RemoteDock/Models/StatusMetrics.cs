namespace RemoteDock.Models;

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
