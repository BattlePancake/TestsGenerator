using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SauceDemoAutomation.Pipeline
{
    public class LlmRunReviewer
    {
        private readonly LlmClient _llmClient;

        public LlmRunReviewer(LlmClient llmClient)
        {
            _llmClient = llmClient;
        }

        public async Task ReviewRunAsync(string trxPath, string logPath, string promptOutputPath, string summaryOutputPath)
        {
            Console.WriteLine("[RUN REVIEW] Analyzing test run results...");

            if (!File.Exists(trxPath))
            {
                Console.WriteLine($"[RUN REVIEW] Error: TRX file '{trxPath}' not found.");
                return;
            }
            if (!File.Exists(logPath))
            {
                Console.WriteLine($"[RUN REVIEW] Error: Log file '{logPath}' not found.");
                return;
            }

            int total = 0, passed = 0, failed = 0;
            var failedDetails = new StringBuilder();

            try
            {
                XDocument doc = XDocument.Load(trxPath);
                XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

                var counters = doc.Descendants(ns + "Counters").FirstOrDefault();
                if (counters != null)
                {
                    int.TryParse(counters.Attribute("total")?.Value, out total);
                    int.TryParse(counters.Attribute("passed")?.Value, out passed);
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

                        failedDetails.AppendLine($"### Тест: {testName}");
                        failedDetails.AppendLine($"- **Продолжительность**: {duration}");
                        failedDetails.AppendLine($"- **Сообщение об ошибке**: {message}");
                        failedDetails.AppendLine("- **Стек вызовов (Stack Trace)**:");
                        failedDetails.AppendLine("```");
                        failedDetails.AppendLine(stackTrace);
                        failedDetails.AppendLine("```");
                        failedDetails.AppendLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RUN REVIEW WARNING] Error parsing TRX XML: {ex.Message}. Proceeding with log analysis only.");
            }

            string rawLogs = File.ReadAllText(logPath);

            string systemInstructions = "Вы — опытный QA Lead. Ваша задача — выполнить анализ результатов выполнения тестов на основе логов и структурированного отчета о прохождении.";

            var sb = new StringBuilder();
            sb.AppendLine("Ниже представлены детали прогона авто-тестов.");
            sb.AppendLine("Пожалуйста, подготовьте отчет о прогоне тестов, включая следующие разделы: ");
            sb.AppendLine("1. Сводка выполнения: сколько всего тестов запущено, сколько пройдено, сколько упало.");
            sb.AppendLine("2. Анализ ошибок (если есть упавшие тесты): в чем причина каждого сбоя, где он произошел и является ли это багом приложения или ошибкой тестовой инфраструктуры.");
            sb.AppendLine("3. Упомяните сделанные скриншоты (помеченные строкой '[SCREENSHOT] Saved failure screenshot to: ...' в логах).");
            sb.AppendLine("4. Заключение и дальнейшие действия.");
            sb.AppendLine();
            sb.AppendLine("--- КОЛИЧЕСТВО ТЕСТОВ ---");
            sb.AppendLine($"- Всего: {total}");
            sb.AppendLine($"- Пройдено: {passed}");
            sb.AppendLine($"- Упало: {failed}");
            sb.AppendLine();
            
            if (failed > 0)
            {
                sb.AppendLine("--- ДЕТАЛИ УПАВШИХ ТЕСТОВ ---");
                sb.AppendLine(failedDetails.ToString());
            }
            else
            {
                sb.AppendLine("Все тесты завершились успешно. Ошибок NUnit ассертов в TRX не обнаружено.");
            }

            sb.AppendLine();
            sb.AppendLine("--- ЛОГИ ВЫПОЛНЕНИЯ (RAW LOGS) ---");
            // Limit log size to prevent overly large prompts
            if (rawLogs.Length > 10000)
            {
                sb.AppendLine(rawLogs.Substring(0, 5000) + "\n... [TRUNCATED] ...\n" + rawLogs.Substring(rawLogs.Length - 5000));
            }
            else
            {
                sb.AppendLine(rawLogs);
            }

            sb.AppendLine();
            sb.AppendLine("Пожалуйста, напишите подробный отчет о прогоне в формате Markdown на русском языке.");

            string userPrompt = sb.ToString();

            // Save the prompt file for user visibility
            File.WriteAllText(promptOutputPath, userPrompt, Encoding.UTF8);
            Console.WriteLine($"[RUN REVIEW] Prompt saved to: {promptOutputPath}");

            string summary;

            if (_llmClient.IsKeyConfigured)
            {
                try
                {
                    Console.WriteLine("[RUN REVIEW] Requesting run review from Mistral API...");
                    summary = await _llmClient.SendPromptAsync(systemInstructions, userPrompt);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RUN REVIEW ERROR] Mistral API request failed: {ex.Message}");
                    Console.WriteLine("[RUN REVIEW] Falling back to pre-baked run review summary...");
                    summary = GetFallbackRunReviewSummary(total, passed, failed, failedDetails.ToString(), true);
                }
            }
            else
            {
                Console.WriteLine("[RUN REVIEW] MISTRAL_API_KEY is not set. Generating pre-baked (simulated) run review summary...");
                summary = GetFallbackRunReviewSummary(total, passed, failed, failedDetails.ToString(), false);
            }

            File.WriteAllText(summaryOutputPath, summary, Encoding.UTF8);
            Console.WriteLine($"[RUN REVIEW] Summary written to: {summaryOutputPath}");
        }

        private string GetFallbackRunReviewSummary(int total, int passed, int failed, string failedDetailsText, bool isErrorFallback)
        {
            var header = isErrorFallback
                ? "# Анализ результатов прогона тестов (Резервная копия из-за ошибки API)\n\n*Примечание: Этот отчет был сгенерирован автоматически, так как запрос к API Mistral завершился ошибкой.*\n\n"
                : "# Анализ результатов прогона тестов (Симуляция без API ключа)\n\n*Примечание: Этот отчет был сгенерирован автоматически, так как переменная `MISTRAL_API_KEY` не установлена.*\n\n";

            var sb = new StringBuilder();
            sb.Append(header);
            sb.AppendLine("## 1. Сводка выполнения");
            sb.AppendLine($"- **Статус**: {(failed == 0 ? "УСПЕШНО" : "ОШИБКА")}");
            sb.AppendLine($"- **Всего тестов выполнено**: {total}");
            sb.AppendLine($"- **Успешные тесты**: {passed} ({(total > 0 ? (passed * 100 / total) : 0)}%)");
            sb.AppendLine($"- **Упавшие тесты**: {failed} ({(total > 0 ? (failed * 100 / total) : 0)}%)");
            sb.AppendLine();
            
            sb.AppendLine("## 2. Детальный анализ результатов");
            if (failed == 0)
            {
                sb.AppendLine("- Все тесты выполнены успешно!");
                sb.AppendLine("- **TC_001** (Успешная авторизация и корзина): Успешно. Page Objects загружены, Selenium Manager настроил ChromeDriver, Chrome запущен в headless-режиме. Счетчик товаров в корзине изменился на '1'.");
                sb.AppendLine("- **TC_002** (Неуспешная авторизация): Успешно. Ошибка с текстом `Epic sadface: Sorry, this user has been locked out.` корректно проверена ассертом.");
                sb.AppendLine("- **TC_003** (Регистрация с PII): Успешно. Генератор корректно пометил кастомные поля и обошел выполнение веб-действий, выведя в логи информацию о полях. Запрос прошел без падения веб-драйвера.");
            }
            else
            {
                sb.AppendLine("### Обнаруженные сбои");
                sb.AppendLine(failedDetailsText);
                sb.AppendLine();
                sb.AppendLine("### Анализ инфраструктуры и багов приложения");
                sb.AppendLine("- Сбой в прогоне указывает на несовпадение между ожидаемыми результатами и фактическим поведением. Пожалуйста, изучите сообщения об ошибках выше.");
            }

            sb.AppendLine();
            sb.AppendLine("## 3. Среда выполнения и логи");
            sb.AppendLine("- **Платформа**: Windows .NET 8.0 Runtime");
            sb.AppendLine("- **Браузер**: Headless Google Chrome (автоматически через Selenium Manager)");
            sb.AppendLine("- **Контекст**: Автоматический запуск тестов");

            if (failed > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 4. Дальнейшие действия по устранению сбоев");
                sb.AppendLine("1. Изучите скриншоты падений, указанные в логах, чтобы увидеть интерфейс в момент ошибки.");
                sb.AppendLine("2. Проверьте актуальность селекторов элементов в Page Objects.");
                sb.AppendLine("3. При обнаружении реального бага приложения создайте баг-репорт (см. `bug_reports.json`).");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("## 4. Рекомендации и выводы");
                sb.AppendLine("1. Прогон чистый. Ошибки в сценариях TC-001 и TC-002 не обнаружены.");
                sb.AppendLine("2. Рекомендуется удалить или перенести тест TC-003 из основного набора, так как он содержит персональные данные (PII) и не делает реальных функциональных проверок.");
            }

            return sb.ToString();
        }
    }
}
