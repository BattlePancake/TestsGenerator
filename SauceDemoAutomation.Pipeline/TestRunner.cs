using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SauceDemoAutomation.Pipeline
{
    public class TestRunner
    {
        public bool RunTests(string projectPath, string resultsDirectory, out string trxFilePath, out string stdoutLogPath)
        {
            trxFilePath = Path.Combine(resultsDirectory, "TestResults.trx");
            stdoutLogPath = Path.Combine(resultsDirectory, "test_run_log.txt");

            Console.WriteLine("[TEST RUNNER] Cleaning up previous test results...");
            if (Directory.Exists(resultsDirectory))
            {
                try
                {
                    Directory.Delete(resultsDirectory, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TEST RUNNER WARNING] Could not delete directory {resultsDirectory}: {ex.Message}");
                }
            }
            Directory.CreateDirectory(resultsDirectory);

            Console.WriteLine($"[TEST RUNNER] Starting 'dotnet test' for project: {projectPath}");

            // Run: dotnet test <projectPath> --logger "trx;LogFileName=TestResults.trx" --results-directory <resultsDirectory>
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{projectPath}\" --logger \"trx;LogFileName=TestResults.trx\" --results-directory \"{resultsDirectory}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(projectPath)
            };

            var outputBuilder = new StringBuilder();
            
            using (var process = new Process { StartInfo = startInfo })
            {
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        Console.WriteLine($"[dotnet test] {e.Data}");
                        outputBuilder.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        Console.WriteLine($"[dotnet test ERROR] {e.Data}");
                        outputBuilder.AppendLine($"ERROR: {e.Data}");
                    }
                };

                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    
                    // Note: dotnet test returns non-zero exit code if tests fail. This is normal.
                    Console.WriteLine($"[TEST RUNNER] Finished with exit code: {process.ExitCode}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TEST RUNNER ERROR] Failed to start dotnet process: {ex.Message}");
                    return false;
                }
            }

            string fullOutput = outputBuilder.ToString();
            File.WriteAllText(stdoutLogPath, fullOutput);
            Console.WriteLine($"[TEST RUNNER] Full execution log saved to: {stdoutLogPath}");

            // Check if TRX was generated
            if (File.Exists(trxFilePath))
            {
                Console.WriteLine($"[TEST RUNNER] TRX results file generated: {trxFilePath}");
                return true;
            }

            Console.WriteLine($"[TEST RUNNER ERROR] TRX results file was not found at: {trxFilePath}");
            return false;
        }
    }
}
