using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace RemoteDock.Services;

/// <summary>
/// External-process execution helpers. Uses the OEM codepage for stdout/stderr so
/// tool output (net use, ssh, icacls) does not arrive as mojibake.
/// </summary>
public static class ProcessRunner
{
    public static async Task<(int ExitCode, string Output, string Error)> RunAsync(string fileName, string args, int timeoutMs)
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

    public static async Task<string> RunTextAsync(string fileName, string args, int timeoutMs)
    {
        var result = await RunAsync(fileName, args, timeoutMs);
        return FormatResult(result);
    }

    public static async Task RunToFileAsync(string fileName, string args, string outputFile, int timeoutMs)
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
        process.Start();
        await using (var fs = File.Create(outputFile))
            await process.StandardOutput.BaseStream.CopyToAsync(fs);
        var waitTask = process.WaitForExitAsync();
        var completed = await Task.WhenAny(waitTask, Task.Delay(timeoutMs));
        if (completed != waitTask) { try { process.Kill(true); } catch { } throw new TimeoutException("Backup timed out."); }
        await waitTask;
        if (process.ExitCode != 0) throw new Exception(await process.StandardError.ReadToEndAsync());
    }

    public static string FormatResult((int ExitCode, string Output, string Error) result)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(result.Output)) sb.AppendLine(result.Output);
        if (!string.IsNullOrWhiteSpace(result.Error)) sb.AppendLine(result.Error);
        sb.AppendLine($"ExitCode: {result.ExitCode}");
        return sb.ToString().Trim();
    }

    public static bool CommandExists(string command)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("where", command)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            p?.WaitForExit(1500);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    public static string QuoteArg(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
