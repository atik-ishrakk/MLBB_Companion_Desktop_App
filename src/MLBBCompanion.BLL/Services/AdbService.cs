using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MLBB.Core.Interfaces;

namespace MLBB.Core.Services;

/// <summary>
/// Service communicating with BlueStacks Android system via ADB.
/// </summary>
public class AdbService : IAdbService
{
    private readonly ILogger<AdbService> _logger;
    private readonly string _adbBinary;
    private string? _lastDevice;
    private bool _isConnected;
    private DateTime _lastPingAttempt = DateTime.MinValue;
    private static readonly TimeSpan PingCooldown = TimeSpan.FromSeconds(5);

    public bool IsConnected => _isConnected;
    public string? CurrentDevice => _lastDevice;

    public AdbService(ILogger<AdbService> logger)
    {
        _logger = logger;
        _adbBinary = FindAdbBinary();
        _logger.LogInformation("Resolved ADB Binary: {Path}", _adbBinary);
    }

    private static IEnumerable<string> GetBlueStacksConfCandidates()
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\BlueStacks_nxt");
                if (key?.GetValue("UserDefinedDir") is string userDir && !string.IsNullOrWhiteSpace(userDir))
                {
                    candidates.Add(Path.Combine(userDir, "bluestacks.conf"));
                }
            }
            catch { }

            try
            {
                using var key = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry32)
                    .OpenSubKey(@"SOFTWARE\BlueStacks_nxt");
                if (key?.GetValue("UserDefinedDir") is string userDir && !string.IsNullOrWhiteSpace(userDir))
                {
                    candidates.Add(Path.Combine(userDir, "bluestacks.conf"));
                }
            }
            catch { }

            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\BlueStacks");
                if (key?.GetValue("UserDefinedDir") is string userDir && !string.IsNullOrWhiteSpace(userDir))
                {
                    candidates.Add(Path.Combine(userDir, "bluestacks.conf"));
                }
            }
            catch { }
        }

        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrEmpty(programData))
        {
            candidates.Add(Path.Combine(programData, "BlueStacks_nxt", "bluestacks.conf"));
            candidates.Add(Path.Combine(programData, "BlueStacks_nxt_cn", "bluestacks.conf"));
            candidates.Add(Path.Combine(programData, "BlueStacks", "bluestacks.conf"));
        }

        candidates.Add(@"C:\ProgramData\BlueStacks_nxt\bluestacks.conf");
        candidates.Add(@"C:\ProgramData\BlueStacks_nxt_cn\bluestacks.conf");
        candidates.Add(@"C:\ProgramData\BlueStacks\bluestacks.conf");

        return candidates;
    }

    private static string FindAdbBinary()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\BlueStacks_nxt");
                if (key?.GetValue("InstallDir") is string installDir && !string.IsNullOrWhiteSpace(installDir))
                {
                    var p = Path.Combine(installDir, "HD-Adb.exe");
                    if (File.Exists(p)) return p;
                }
            }
            catch { }

            try
            {
                using var key = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry32)
                    .OpenSubKey(@"SOFTWARE\BlueStacks_nxt");
                if (key?.GetValue("InstallDir") is string installDir && !string.IsNullOrWhiteSpace(installDir))
                {
                    var p = Path.Combine(installDir, "HD-Adb.exe");
                    if (File.Exists(p)) return p;
                }
            }
            catch { }

            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\BlueStacks");
                if (key?.GetValue("InstallDir") is string installDir && !string.IsNullOrWhiteSpace(installDir))
                {
                    var p = Path.Combine(installDir, "HD-Adb.exe");
                    if (File.Exists(p)) return p;
                }
            }
            catch { }
        }

        var roots = new[]
        {
            Environment.GetEnvironmentVariable("ProgramW6432") ?? @"C:\Program Files",
            Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files",
            Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)",
            Environment.GetEnvironmentVariable("LocalAppData") ?? @"C:\Users\Default\AppData\Local"
        }.Distinct();

        foreach (var r in roots)
        {
            var p1 = Path.Combine(r, "BlueStacks_nxt", "HD-Adb.exe");
            if (File.Exists(p1)) return p1;

            var p2 = Path.Combine(r, "BlueStacks", "HD-Adb.exe");
            if (File.Exists(p2)) return p2;

            var pAndroid = Path.Combine(r, "Android", "Sdk", "platform-tools", "adb.exe");
            if (File.Exists(pAndroid)) return pAndroid;
        }

        string[] extraPaths =
        [
            @"C:\Program Files\BlueStacks_nxt\HD-Adb.exe",
            @"C:\Program Files (x86)\BlueStacks_nxt\HD-Adb.exe",
            @"C:\Program Files\Nox\bin\nox_adb.exe",
            @"C:\LDPlayer\LDPlayer9\adb.exe",
            @"C:\LDPlayer\LDPlayer4.0\adb.exe",
            @"C:\Program Files\Netease\MuMuPlayerGlobal-12.0\shell\adb.exe"
        ];

        foreach (var ep in extraPaths)
        {
            if (File.Exists(ep)) return ep;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, "adb.exe");
            if (File.Exists(candidate)) return candidate;
        }

        return "adb";
    }

    public Dictionary<string, int> GetConfiguredPorts()
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var confPath in GetBlueStacksConfCandidates())
        {
            if (!File.Exists(confPath)) continue;

            try
            {
                var lines = File.ReadAllLines(confPath);
                foreach (var line in lines)
                {
                    var match = Regex.Match(line, @"bst\.instance\.([a-zA-Z0-9_]+)\.(?:status\.)?adb_port=""(\d+)""");
                    if (match.Success)
                    {
                        var instance = match.Groups[1].Value;
                        if (int.TryParse(match.Groups[2].Value, out int port))
                        {
                            result[instance] = port;
                        }
                    }
                }

                if (result.Count > 0) break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse BlueStacks configuration at {Path}", confPath);
            }
        }

        return result;
    }

    public async Task<bool> PingDeviceAsync()
    {
        if (DateTime.UtcNow - _lastPingAttempt < TimeSpan.FromSeconds(1))
        {
            return _isConnected;
        }
        _lastPingAttempt = DateTime.UtcNow;

        return await Task.Run(() =>
        {
            // 1. If _lastDevice is set, test get-state
            if (!string.IsNullOrEmpty(_lastDevice) && CheckDeviceState(_lastDevice))
            {
                _isConnected = true;
                return true;
            }

            // 2. Query adb devices to find attached devices
            var attached = QueryAttachedDevices();
            if (attached.Count > 0)
            {
                // Prefer emulator devices
                var emu = attached.FirstOrDefault(d => d.Contains("127.0.0.1") || d.Contains("emulator") || d.Contains("localhost"))
                          ?? attached[0];
                if (CheckDeviceState(emu))
                {
                    _lastDevice = emu;
                    _isConnected = true;
                    _logger.LogInformation("Attached to ADB device: {Device}", _lastDevice);
                    return true;
                }
            }

            // 3. Collect all candidate ports to test
            var ports = GetConfiguredPorts();
            var candidatePorts = new List<int>(ports.Values);
            int[] defaultPorts = [5555, 5554, 5556, 5565, 5575, 5585, 16384, 16416, 62001, 62025];
            foreach (var dp in defaultPorts)
            {
                if (!candidatePorts.Contains(dp)) candidatePorts.Add(dp);
            }

            // 4. Try connecting to candidates
            foreach (int port in candidatePorts)
            {
                string endpoint = $"127.0.0.1:{port}";
                if (CheckDeviceState(endpoint) || TryConnectDevice(endpoint))
                {
                    if (CheckDeviceState(endpoint))
                    {
                        _lastDevice = endpoint;
                        _isConnected = true;
                        _logger.LogInformation("Connected to ADB endpoint: {Endpoint}", endpoint);
                        return true;
                    }
                }
            }

            _isConnected = false;
            return false;
        });
    }

    private bool CheckDeviceState(string device)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _adbBinary,
                Arguments = $"-s {device} get-state",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            string outText = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(1000);
            return outText.Contains("device", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private List<string> QueryAttachedDevices()
    {
        var list = new List<string>();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _adbBinary,
                Arguments = "devices",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return list;
            string outText = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(1000);
            foreach (var line in outText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("List of") || line.StartsWith("* daemon")) continue;
                var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && parts[1].Equals("device", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(parts[0]);
                }
            }
        }
        catch { }
        return list;
    }

    private bool TryConnectDevice(string endpoint)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _adbBinary,
                Arguments = $"connect {endpoint}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            string outText = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(1500);
            return outText.Contains("connected", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public async Task<(string appState, string packageName)> GetFocusedAppStateAsync()
    {
        return await Task.Run(() =>
        {
            if (!_isConnected || string.IsNullOrEmpty(_lastDevice))
                return ("STANDBY", "");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _adbBinary,
                    Arguments = $"-s {_lastDevice} shell dumpsys window windows",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null) return ("STANDBY", "");

                string outText = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(1200);

                foreach (var line in outText.Split('\n'))
                {
                    if (line.Contains("mCurrentFocus", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("mFocusedApp", StringComparison.OrdinalIgnoreCase))
                    {
                        var lower = line.ToLowerInvariant();
                        if (lower.Contains("launcher"))
                            return ("STANDBY", "");

                        if (lower.Contains("splashactivity"))
                            return ("LOADING", "com.mobile.legends");

                        if (lower.Contains("mobagameunityactivity") || lower.Contains("mobile.legends"))
                            return ("GAME_ACTIVE", "com.mobile.legends");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error getting focused app state");
            }

            return ("STANDBY", "");
        });
    }

    public async Task<byte[]?> CaptureScreencapAsync()
    {
        if (!_isConnected || string.IsNullOrEmpty(_lastDevice))
        {
            await PingDeviceAsync();
            if (!_isConnected || string.IsNullOrEmpty(_lastDevice))
                return null;
        }

        return await Task.Run(() =>
        {
            byte[]? RunCapture(string device)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = _adbBinary,
                        Arguments = $"-s {device} exec-out screencap -p",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc == null) return null;

                    using var ms = new MemoryStream();
                    proc.StandardOutput.BaseStream.CopyTo(ms);
                    proc.WaitForExit(3500);

                    byte[] data = ms.ToArray();
                    return data.Length > 100 ? data : null;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error in ADB screencap on device {Device}", device);
                    return null;
                }
            }

            var bytes = RunCapture(_lastDevice);
            if (bytes == null || bytes.Length < 100)
            {
                // Re-check attached devices in case port or device changed
                var attached = QueryAttachedDevices();
                var emu = attached.FirstOrDefault(d => d.Contains("127.0.0.1") || d.Contains("emulator") || d.Contains("localhost"))
                          ?? attached.FirstOrDefault();
                if (!string.IsNullOrEmpty(emu) && emu != _lastDevice)
                {
                    _lastDevice = emu;
                    bytes = RunCapture(_lastDevice);
                }
            }

            return bytes;
        });
    }
}

