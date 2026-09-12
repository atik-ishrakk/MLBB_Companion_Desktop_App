using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MLBBCompanion.GUI;

static class Program
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    [STAThread]
    static void Main()
    {
        var currentProc = Process.GetCurrentProcess();
        var existingProcs = Process.GetProcessesByName(currentProc.ProcessName)
            .Where(p => p.Id != currentProc.Id)
            .ToList();

        if (existingProcs.Count > 0)
        {
            foreach (var proc in existingProcs)
            {
                if (proc.MainWindowHandle != IntPtr.Zero)
                {
                    // Existing instance with an active window handle — restore and bring to front
                    ShowWindowAsync(proc.MainWindowHandle, SW_RESTORE);
                    SetForegroundWindow(proc.MainWindowHandle);
                    return;
                }
                else
                {
                    // Orphaned/zombie background process without a visible window — kill cleanly
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(1000);
                    }
                    catch
                    {
                        // Ignore if process cannot be killed
                    }
                }
            }
        }

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), e.ExceptionObject?.ToString() ?? "null"); } catch { }
        };
        Application.ThreadException += (s, e) =>
        {
            try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), e.Exception.ToString()); } catch { }
        };

        try
        {
            ApplicationConfiguration.Initialize();
            var mainForm = new MainForm();
            Application.Run(mainForm);
        }
        catch (Exception ex)
        {
            try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), ex.ToString()); } catch { }
        }
    }    
}