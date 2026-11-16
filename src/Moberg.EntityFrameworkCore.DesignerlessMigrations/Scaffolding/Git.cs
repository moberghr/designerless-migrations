using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore.Design;

namespace Moberg.EntityFrameworkCore.DesignerlessMigrations.Scaffolding;

/// <summary>Runs git commands. The executable is a parameter so tests can simulate git missing from PATH.</summary>
internal sealed class Git(string executable = "git")
{
    public string Run(string workingDirectory, params string[] arguments) =>
        Encoding.UTF8.GetString(RunForBytes(workingDirectory, arguments));

    /// <summary>Returns stdout as raw bytes, so a file's BOM and line endings come back exactly as git produced them.</summary>
    public byte[] RunForBytes(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process process;

        try
        {
            process = Process.Start(startInfo) ?? throw new GitNotFoundException();
        }
        catch (Win32Exception ex)
        {
            throw new GitNotFoundException(ex);
        }

        using (process)
        {
            var errorTask = process.StandardError.ReadToEndAsync();
            using var output = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(output);
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new OperationException($"git {string.Join(' ', arguments)} failed: {errorTask.Result.Trim()}");
            }

            return output.ToArray();
        }
    }
}

internal sealed class GitNotFoundException : Exception
{
    public GitNotFoundException()
        : base("git could not be started.")
    {
    }

    public GitNotFoundException(Exception innerException)
        : base("git could not be started.", innerException)
    {
    }

    public GitNotFoundException(string message)
        : base(message)
    {
    }

    public GitNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
