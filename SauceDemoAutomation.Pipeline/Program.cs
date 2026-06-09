using System;
using System.IO;
using System.Threading.Tasks;

namespace SauceDemoAutomation.Pipeline
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            // Establish directories
            // Since the executable runs inside bin/Debug/net8.0/ of SauceDemoAutomation.Pipeline,
            // we resolve the root solution directory recursively.
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            string solutionDir = currentDir;
            
            while (!string.IsNullOrEmpty(solutionDir) && !File.Exists(Path.Combine(solutionDir, "SauceDemoAutomation.sln")))
            {
                solutionDir = Path.GetDirectoryName(solutionDir);
            }

            if (string.IsNullOrEmpty(solutionDir))
            {
                // Fallback to parent directory if solution isn't found in parent path
                solutionDir = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("======================================================================");
            Console.WriteLine("                SAUCEDEMO AUTOMATION PIPELINE RUNNER                  ");
            Console.WriteLine("======================================================================");
            Console.ResetColor();
            Console.WriteLine($"Solution Directory Resolved: {solutionDir}\n");

            string inputJsonPath = Path.Combine(solutionDir, "input_test_cases.json");
            string frameworkProjectPath = Path.Combine(solutionDir, "SauceDemoAutomation.Framework", "SauceDemoAutomation.Framework.csproj");
            string generatedTestsPath = Path.Combine(solutionDir, "SauceDemoAutomation.Framework", "GeneratedTests.cs");
            
            // Output paths
            string resultsDir = Path.Combine(solutionDir, "Outputs", "TestResults");
            string promptsInputDir = Path.Combine(solutionDir, "Inputs", "Prompts Input");
            string llmOutputDir = Path.Combine(solutionDir, "Outputs", "LlmOutputs");

            Directory.CreateDirectory(resultsDir);
            Directory.CreateDirectory(promptsInputDir);
            Directory.CreateDirectory(llmOutputDir);

            string codeReviewPromptPath = Path.Combine(promptsInputDir, "llm_code_review_prompt.txt");
            string codeReviewSummaryPath = Path.Combine(llmOutputDir, "code_review_summary.md");
            string runReviewPromptPath = Path.Combine(promptsInputDir, "llm_run_review_prompt.txt");
            string runReviewSummaryPath = Path.Combine(llmOutputDir, "test_run_summary.md");
            string bugReportPromptPath = Path.Combine(promptsInputDir, "llm_bug_report_prompt.txt");
            string bugReportsJsonPath = Path.Combine(llmOutputDir, "bug_reports.json");

            // Initializing helpers
            var piiGuard = new PiiGuardrail();
            var testGen = new TestGenerator();
            var llmClient = new LlmClient();
            var codeReviewer = new LlmCodeReviewer(llmClient);
            var runner = new TestRunner();
            var runReviewer = new LlmRunReviewer(llmClient);
            var bugReporter = new LlmBugReporter(llmClient);

            // ==================================================================
            // STEP 1: PII Guardrail Review
            // ==================================================================
            PrintStepHeader(1, "PII Guardrail Review (Scanning input JSON)");
            bool piiClean = piiGuard.ScanFile(inputJsonPath, out var piiFindings);
            
            if (!piiClean)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[PII WARNING] Found {piiFindings.Count} sensitive items in the input test cases!");
                foreach (var finding in piiFindings)
                {
                    Console.WriteLine($" - Test Case: {finding.TestCaseId} | Location: {finding.Location} | Type: {finding.PatternType} | Value: {finding.MatchedValue}");
                }
                Console.WriteLine("\n[PII GUARD] Note: In a production pipeline, this might trigger a blocking warning or redact values.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[PII CLEAN] No sensitive PII was found in the input JSON.");
                Console.ResetColor();
            }

            // ==================================================================
            // STEP 2: Programmatic C# Test Generation
            // ==================================================================
            PrintStepHeader(2, "Generating C# Selenium NUnit Tests");
            bool genOk = testGen.GenerateTests(inputJsonPath, generatedTestsPath);
            if (!genOk)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Test generation failed. Aborting pipeline.");
                Console.ResetColor();
                return;
            }

            // ==================================================================
            // STEP 3: LLM Code Review
            // ==================================================================
            PrintStepHeader(3, "LLM Code Review");
            await codeReviewer.ReviewCodeAsync(inputJsonPath, generatedTestsPath, codeReviewPromptPath, codeReviewSummaryPath);

            // ==================================================================
            // STEP 4: Run Auto-tests
            // ==================================================================
            PrintStepHeader(4, "Executing Automated Tests");
            bool runOk = runner.RunTests(frameworkProjectPath, resultsDir, out string trxPath, out string stdoutLogPath);
            if (!runOk)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[ERROR] Running tests failed (no TRX file generated). Check framework build errors.");
                Console.ResetColor();
                return;
            }

            // ==================================================================
            // STEP 5: LLM Run Results & Logs Review
            // ==================================================================
            PrintStepHeader(5, "LLM Test Run & Logs Review");
            await runReviewer.ReviewRunAsync(trxPath, stdoutLogPath, runReviewPromptPath, runReviewSummaryPath);

            // ==================================================================
            // STEP 6: LLM Bug Reporting
            // ==================================================================
            PrintStepHeader(6, "LLM Bug Reporting");
            await bugReporter.GenerateBugReportsAsync(trxPath, stdoutLogPath, bugReportPromptPath, bugReportsJsonPath);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n======================================================================");
            Console.WriteLine("                  PIPELINE EXECUTION COMPLETED!                       ");
            Console.WriteLine("======================================================================");
            Console.ResetColor();
            Console.WriteLine($"Test results TRX:    {trxPath}");
            Console.WriteLine($"Test run log:        {stdoutLogPath}");
            Console.WriteLine($"Prompts directory:   {promptsInputDir}");
            Console.WriteLine($"Code review summary: {codeReviewSummaryPath}");
            Console.WriteLine($"Run review summary:  {runReviewSummaryPath}");
            Console.WriteLine($"Bug reports JSON:    {bugReportsJsonPath}");
            Console.WriteLine("======================================================================\n");
        }

        static void PrintStepHeader(int stepNum, string description)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n--- [STEP {stepNum}] {description} ---");
            Console.ResetColor();
        }
    }
}
