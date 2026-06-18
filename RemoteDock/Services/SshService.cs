using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using RemoteDock.Models;

namespace RemoteDock.Services;

/// <summary>
/// SSH-facing helpers: argument construction, remote command execution, and the
/// metric-gathering scripts for Linux and Windows targets.
/// </summary>
public static class SshService
{
    public static string BuildSshArgs(DeviceProfile p, string remoteCommand)
    {
        var sb = new StringBuilder();
        sb.Append("-o BatchMode=yes -o ConnectTimeout=7 ");
        sb.Append("-p ").Append(p.Port).Append(' ');
        if (!string.IsNullOrWhiteSpace(p.SshKeyPath))
        {
            ProfileStore.TryFixPrivateKeyAcl(p.SshKeyPath);
            sb.Append("-i ").Append(ProcessRunner.QuoteArg(p.SshKeyPath)).Append(' ');
        }
        var target = string.IsNullOrWhiteSpace(p.User) ? p.Host : $"{p.User}@{p.Host}";
        sb.Append(ProcessRunner.QuoteArg(target));
        if (!string.IsNullOrWhiteSpace(remoteCommand)) sb.Append(' ').Append(ProcessRunner.QuoteArg(remoteCommand));
        return sb.ToString();
    }

    public static Task<(int ExitCode, string Output, string Error)> RunCommandAsync(DeviceProfile p, string command, int timeoutMs)
    {
        var sshArgs = BuildSshArgs(p, command);
        return ProcessRunner.RunAsync("ssh", sshArgs, timeoutMs);
    }

    public static bool IsDefaultSshKey(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var full = Path.GetFullPath(path);
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var idRsa = Path.Combine(home, ".ssh", "id_rsa");
            var idEd = Path.Combine(home, ".ssh", "id_ed25519");
            return string.Equals(full, idRsa, StringComparison.OrdinalIgnoreCase)
                || string.Equals(full, idEd, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static string LinuxMetricsCommand()
    {
        return "printf 'HOST='; hostname; " +
               "printf 'UPTIME='; uptime -p 2>/dev/null || uptime; " +
               "printf 'LOAD='; cut -d' ' -f1 /proc/loadavg; " +
               "printf 'CORES='; nproc 2>/dev/null || echo 1; " +
               "free -m | awk '/Mem:/ {printf \"RAM=%d\\nRAM_USED=%dMB\\nRAM_TOTAL=%dMB\\n\", ($3*100)/$2, $3, $2}'; " +
               "df -P -m / | awk 'NR==2 {gsub(\"%\",\"\",$5); printf \"DISK=%d\\nDISK_USED=%dMB\\nDISK_AVAIL=%dMB\\nDISK_TOTAL=%dMB\\n\", $5,$3,$4,$2}'; " +
               "echo PROCESSES_BEGIN; ps -eo pid,comm,%cpu,%mem --sort=-%cpu | head -16";
    }

    public static string WindowsMetricsCommand()
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
}
