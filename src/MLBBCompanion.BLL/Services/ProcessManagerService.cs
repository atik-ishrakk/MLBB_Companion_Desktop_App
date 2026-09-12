using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MLBB.Core.Interfaces;

namespace MLBB.Core.Services;

/// <summary>
/// Service controlling Windows processes, BlueStacks lifecycle, port freeing, and memory trimming.
/// </summary>
public class ProcessManagerService : IProcessManagerService
{
    private readonly ILogger<ProcessManagerService> _logger;
    private readonly IAdbService _adbService;

    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr ShellExecute(
        IntPtr hwnd,
        string lpOperation,
        string lpFile,
        string lpParameters,
        string? lpDirectory,
        int nShowCmd);

    private const int SW_SHOWNORMAL = 1;

    public ProcessManagerService(ILogger<ProcessManagerService> logger, IAdbService adbService)
    {
        _logger = logger;
        _adbService = adbService;
    }

    public (bool isRunning, int adbPort) IsBlueStacksRunning()
    {
        bool running = false;
        try
        {
            string[] targetProcesses = ["HD-Player", "BlueStacks", "BlueStacksAppPlayer", "BstkSVC", "dnplayer", "Nox", "MuMuPlayer"];
            foreach (var name in targetProcesses)
            {
                var procs = Process.GetProcessesByName(name);
                if (procs.Length > 0)
                {
                    _logger.LogInformation("Detected active emulator process: {Name} (Count: {Count})", name, procs.Length);
                    running = true;
                    foreach (var p in procs) p.Dispose();
                    break;
                }
                foreach (var p in procs) p.Dispose();
            }

            if (!running && _adbService.IsConnected)
            {
                _logger.LogInformation("ADB Service is connected; marking BlueStacks as running.");
                running = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking BlueStacks process.");
        }

        var ports = _adbService.GetConfiguredPorts();
        int activePort = ports.Count > 0 ? ports.Values.OrderBy(p => p).First() : 5555;
        return (running, activePort);
    }

    public async Task<(bool success, string message, string? instance)> LaunchBlueStacksAsync()
    {
        return await Task.Run<(bool, string, string?)>(() =>
        {
            var ports = _adbService.GetConfiguredPorts();
            string instanceName = ports.Count > 0 ? ports.Keys.First() : "Nougat32";

            var installRoots = new List<string>
            {
                Environment.GetEnvironmentVariable("ProgramW6432") ?? @"C:\Program Files",
                Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files",
                Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)"
            }.Distinct();

            string? bsPlayer = null;
            string? bsManager = null;

            foreach (var root in installRoots)
            {
                var p = Path.Combine(root, "BlueStacks_nxt", "HD-Player.exe");
                if (File.Exists(p)) { bsPlayer = p; break; }
                p = Path.Combine(root, "BlueStacks", "HD-Player.exe");
                if (File.Exists(p)) { bsPlayer = p; break; }
            }

            foreach (var root in installRoots)
            {
                var m = Path.Combine(root, "BlueStacks_nxt", "HD-MultiInstanceManager.exe");
                if (File.Exists(m)) { bsManager = m; break; }
                m = Path.Combine(root, "BlueStacks", "HD-MultiInstanceManager.exe");
                if (File.Exists(m)) { bsManager = m; break; }
            }

            if (bsPlayer != null)
            {
                try
                {
                    string args = $"--instance {instanceName} --package com.mobile.legends";
                    _logger.LogInformation("Launching BlueStacks Player: {Exe} {Args}", bsPlayer, args);
                    
                    IntPtr res = ShellExecute(IntPtr.Zero, "open", bsPlayer, args, null, SW_SHOWNORMAL);
                    if (res.ToInt64() <= 32)
                    {
                        // Fallback to Process.Start
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = bsPlayer,
                            Arguments = args,
                            UseShellExecute = true
                        });
                    }

                    return (true, $"Launched BlueStacks ({instanceName}) visibly: {bsPlayer}", instanceName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to launch BlueStacks Player.");
                    return (false, $"Error launching BlueStacks: {ex.Message}", null);
                }
            }

            if (bsManager != null)
            {
                try
                {
                    _logger.LogInformation("Launching BlueStacks Multi-Instance Manager: {Exe}", bsManager);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = bsManager,
                        UseShellExecute = true
                    });
                    return (true, $"Launched BlueStacks Multi-Instance Manager: {bsManager}", instanceName);
                }
                catch (Exception ex)
                {
                    return (false, $"Error launching BlueStacks manager: {ex.Message}", null);
                }
            }

            return (false, "BlueStacks executable not found in standard installation paths.", null);
        });
    }

    public async Task<(bool success, string message, int processesKilled)> CloseBlueStacksAsync()
    {
        return await Task.Run(() =>
        {
            int killed = 0;
            string[] targets = ["HD-Player", "HD-Adb", "HD-MultiInstanceManager", "BstkSVC"];

            foreach (var name in targets)
            {
                try
                {
                    var procs = Process.GetProcessesByName(name);
                    foreach (var p in procs)
                    {
                        try
                        {
                            p.Kill(true);
                            killed++;
                            _logger.LogInformation("Terminated process: {Name} (PID: {Pid})", name, p.Id);
                        }
                        catch { }
                        finally { p.Dispose(); }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed killing {Name}", name);
                }
            }

            Thread.Sleep(200);
            var (isRunning, _) = IsBlueStacksRunning();
            if (isRunning)
            {
                return (false, "BlueStacks is still running after termination request.", killed);
            }

            return (true, "BlueStacks instance closed successfully.", killed);
        });
    }

    public void PurgeRamAndVram()
    {
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var proc = Process.GetCurrentProcess();
                EmptyWorkingSet(proc.Handle);
            }
            _logger.LogInformation("System RAM working set flushed.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error purging RAM working set.");
        }
    }

    public async Task<int> KillProcessesByPortsAsync(IEnumerable<int> ports, int? excludePid = null)
    {
        return await Task.Run(() =>
        {
            int killedCount = 0;
            var portSet = new HashSet<int>(ports);
            int currentPid = excludePid ?? Process.GetCurrentProcess().Id;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano -p tcp",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return 0;

                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                var pidsToKill = new HashSet<int>();

                foreach (var rawLine in output.Split('\n'))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line) || !line.Contains("LISTENING", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var parts = Regex.Split(line, @"\s+");
                    if (parts.Length >= 5 && int.TryParse(parts[^1], out int pid))
                    {
                        if (pid == 0 || pid == currentPid) continue;

                        string localAddr = parts[1];
                        foreach (var p in portSet)
                        {
                            if (localAddr.EndsWith($":{p}", StringComparison.Ordinal))
                            {
                                pidsToKill.Add(pid);
                            }
                        }
                    }
                }

                foreach (var pid in pidsToKill)
                {
                    try
                    {
                        using var targetProc = Process.GetProcessById(pid);
                        targetProc.Kill(true);
                        killedCount++;
                        _logger.LogInformation("Terminated process on listening port (PID: {Pid})", pid);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during port-based process termination.");
            }

            return killedCount;
        });
    }

    public void TriggerGracefulShutdown(bool closeEmulator = false)
    {
        Task.Run(async () =>
        {
            await Task.Delay(200);
            if (closeEmulator)
            {
                await CloseBlueStacksAsync();
            }
            PurgeRamAndVram();
            Environment.Exit(0);
        });
    }
}
