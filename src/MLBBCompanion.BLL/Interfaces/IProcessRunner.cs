using System.Diagnostics;

namespace MLBBCompanion.BLL.Interfaces;

public interface IProcessRunner
{
    Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(string fileName, string arguments, string? workingDir = null, int timeoutSeconds = 30);
    Process? StartSilent(string fileName, string arguments, string? workingDir = null);
}
