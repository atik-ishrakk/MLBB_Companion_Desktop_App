using System.Diagnostics;
using MLBBCompanion.BLL.Interfaces;

namespace MLBBCompanion.DAL.Implementations;

public class ProcessRunner : IProcessRunner
{
    public async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string fileName,
        string arguments,
        string? workingDir = null,
        int timeoutSeconds = 30)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workingDir ?? AppDomain.CurrentDomain.BaseDirectory
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token);
            var output = await outputTask;
            var error = await errorTask;

            return (process.ExitCode, output, error);
        }
        catch (OperationCanceledException)
        {
            return (-1, string.Empty, "Process timed out.");
        }
        catch (Exception ex)
        {
            return (-1, string.Empty, ex.Message);
        }
    }

    public Process? StartSilent(string fileName, string arguments, string? workingDir = null)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                WorkingDirectory = workingDir ?? AppDomain.CurrentDomain.BaseDirectory
            };

            return Process.Start(psi);
        }
        catch
        {
            return null;
        }
    }
}
