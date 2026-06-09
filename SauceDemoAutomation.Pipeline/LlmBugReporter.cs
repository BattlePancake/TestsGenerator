using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SauceDemoAutomation.Pipeline
{
    public class LlmBugReporter
    {
        private readonly LlmClient _llmClient;

        public LlmBugReporter(LlmClient llmClient)
        {
            _llmClient = llmClient;
        }

        public async Task GenerateBugReportsAsync(string trxPath, string logPath, string promptOutputPath, string jsonOutputPath)
        {
            Console.WriteLine("[BUG REPORTER] Checking for test failures to report...");

            if (!File.Exists(trxPath))
            {
                Console.WriteLine($"[BUG REPORTER] Error: TRX file '{trxPath}' not found.");
                return;
            }

            int failed = 0;
            var failedDetails = new StringBuilder();

            try
            {
                XDocument doc = XDocument.Load(trxPath);
                XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

                var counters = doc.Descendants(ns + "Counters").FirstOrDefault();
                if (counters != null)
                {
                    int.TryParse(counters.Attribute("failed")?.Value, out failed);
                }

                var results = doc.Descendants(ns + "UnitTestResult");
                foreach (var result in results)
                {
                    string outcome = result.Attribute("outcome")?.Value;
                    if (outcome?.Equals("Failed", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        string testName = result.Attribute("testName")?.Value;
                        string duration = result.Attribute("duration")?.Value;
                        var message = result.Descendants(ns + "Message").FirstOrDefault()?.Value;
                        var stackTrace = result.Descendants(ns + "StackTrace").FirstOrDefault()?.Value;

                        failedDetails.AppendLine($"Тест-кейс ID: {testName}");
                        failedDetails.AppendLine($"Сообщение об ошибке: {message}");
                        failedDetails.AppendLine($"Стек вызовов: {stackTrace}");
                        failedDetails.AppendLine("----------------------------------------");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BUG REPORTER WARNING] Error parsing TRX XML: {ex.Message}");
            }

            if (failed == 0)
            {
                Console.WriteLine("[BUG REPORTER] No failures detected. Writing empty bug report list to JSON.");
                File.WriteAllText(jsonOutputPath, "[]", Encoding.UTF8);
                File.WriteAllText(promptOutputPath, "Тесты выполнены без ошибок. Промпт для генерации баг-репортов не требуется.", Encoding.UTF8);
                return;
            }

            string systemInstructions = "Вы — ведущий инженер по тестированию (QA Engineer). Ваша задача — составить структурированные баг-репорты в формате JSON на основе данных о падениях автоматизированных тестов.";
            
            var sb = new StringBuilder();
            sb.AppendLine("Ниже представлены детали упавших автоматизированных тестов.");
            sb.AppendLine("Пожалуйста, сформируйте баг-репорты для этих сбоев в формате JSON. Ответ должен быть строго валидным массивом JSON, содержащим объекты со следующей схемой:");
            sb.AppendLine("[");
            sb.AppendLine("  {");
            sb.AppendLine("    \"bug_id\": \"строка (например, BUG-001)\",");
            sb.AppendLine("    \"summary\": \"строка (понятное и краткое описание бага на русском языке)\",");
            sb.AppendLine("    \"severity\": \"строка (Blocker, Critical, Major, Minor)\",");
            sb.AppendLine("    \"steps_to_reproduce\": [\"шаг 1 на русском\", \"шаг 2 на русском\", ...],");
            sb.AppendLine("    \"expected_behavior\": \"строка на русском языке\",");
            sb.AppendLine("    \"actual_behavior\": \"строка на русском языке\",");
            sb.AppendLine("    \"logs_or_errors\": \"строка с ошибкой/стеком\"");
            sb.AppendLine("  }");
            sb.AppendLine("]");
            sb.AppendLine();
            sb.AppendLine("Пожалуйста, выведите ТОЛЬКО чистый массив JSON в вашем ответе, без каких-либо оберток разметки Markdown (таких как ```json или ```).");
            sb.AppendLine();
            sb.AppendLine("--- ЛОГИ ОШИБОК И СБОЕВ ---");
            sb.AppendLine(failedDetails.ToString());

            string userPrompt = sb.ToString();

            // Save the prompt file
            File.WriteAllText(promptOutputPath, userPrompt, Encoding.UTF8);
            Console.WriteLine($"[BUG REPORTER] Prompt saved to: {promptOutputPath}");

            string jsonReport;

            if (_llmClient.IsKeyConfigured)
            {
                try
                {
                    Console.WriteLine("[BUG REPORTER] Requesting bug reports from Mistral API...");
                    jsonReport = await _llmClient.SendPromptAsync(systemInstructions, userPrompt);
                    
                    // Strip code blocks if LLM still wraps it
                    jsonReport = CleanJsonWrapper(jsonReport);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BUG REPORTER ERROR] Mistral API request failed: {ex.Message}");
                    Console.WriteLine("[BUG REPORTER] Falling back to pre-baked bug report JSON...");
                    jsonReport = GetFallbackBugReportJson();
                }
            }
            else
            {
                Console.WriteLine("[BUG REPORTER] MISTRAL_API_KEY is not set. Generating pre-baked (simulated) bug report JSON...");
                jsonReport = GetFallbackBugReportJson();
            }

            File.WriteAllText(jsonOutputPath, jsonReport, Encoding.UTF8);
            Console.WriteLine($"[BUG REPORTER] Bug reports saved to: {jsonOutputPath}");
        }

        private string CleanJsonWrapper(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText)) return "[]";

            string trimmed = jsonText.Trim();
            
            // Remove ```json wrapper if present
            if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(7);
            }
            else if (trimmed.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(3);
            }

            if (trimmed.EndsWith("```"))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 3);
            }

            return trimmed.Trim();
        }

        private string GetFallbackBugReportJson()
        {
            return @"[
  {
    ""bug_id"": ""BUG-001"",
    ""summary"": ""Не удалось добавить товар в корзину на странице каталога из-за отсутствия элемента или несовпадения селектора кнопки"",
    ""severity"": ""Critical"",
    ""steps_to_reproduce"": [
      ""1. Открыть браузер."",
      ""2. Перейти на страницу https://www.saucedemo.com/."",
      ""3. Авторизоваться под пользователем standard_user / secret_sauce."",
      ""4. Попытаться добавить товар 'Sauce Labs Backpack' в корзину.""
    ],
    ""expected_behavior"": ""Товар добавляется в корзину, текст кнопки меняется на 'Remove', счетчик на иконке корзины увеличивается на 1."",
    ""actual_behavior"": ""Элемент кнопки не найден или не может быть кликнут, выбрасывается исключение NoSuchElementException."",
    ""logs_or_errors"": ""OpenQA.Selenium.NoSuchElementException: Unable to locate element: .inventory_item (mocked trace)""
  }
]";
        }
    }
}
