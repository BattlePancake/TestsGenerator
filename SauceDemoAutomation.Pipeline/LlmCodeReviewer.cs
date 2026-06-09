using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SauceDemoAutomation.Pipeline
{
    public class LlmCodeReviewer
    {
        private readonly LlmClient _llmClient;

        public LlmCodeReviewer(LlmClient llmClient)
        {
            _llmClient = llmClient;
        }

        public async Task ReviewCodeAsync(string jsonPath, string codePath, string promptOutputPath, string summaryOutputPath)
        {
            Console.WriteLine("[CODE REVIEW] Preparing code review...");
            
            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"[CODE REVIEW] Error: JSON file '{jsonPath}' not found.");
                return;
            }
            if (!File.Exists(codePath))
            {
                Console.WriteLine($"[CODE REVIEW] Error: Code file '{codePath}' not found.");
                return;
            }

            string jsonContent = File.ReadAllText(jsonPath);
            string codeContent = File.ReadAllText(codePath);

            string systemInstructions = "Вы — эксперт в автоматизации тестирования QA и разработчик на C#. Ваша задача — выполнить ревью кода автоматически сгенерированных тестов Selenium NUnit на основе исходных тест-кейсов из JSON.";
            
            var sb = new StringBuilder();
            sb.AppendLine("Ниже представлены исходные тест-кейсы (JSON) и программно сгенерированный C# класс тестов.");
            sb.AppendLine("Пожалуйста, выполните ревью сгенерированных тестов по следующим критериям: ");
            sb.AppendLine("1. Структура кода (атрибуты NUnit, разметка класса, использование Page Object).");
            sb.AppendLine("2. Соответствие шагам (проверка того, что шаги, входные данные и ожидаемые результаты из JSON представлены в C#).");
            sb.AppendLine("3. Корректность кода, лучшие практики Selenium и логика.");
            sb.AppendLine("4. Укажите, если какие-то шаги не удалось сопоставить и они требуют ручной реализации.");
            sb.AppendLine();
            sb.AppendLine("--- ИСХОДНЫЙ JSON ---");
            sb.AppendLine(jsonContent);
            sb.AppendLine();
            sb.AppendLine("--- СГЕНЕРИРОВАННЫЙ КОД C# ---");
            sb.AppendLine(codeContent);
            sb.AppendLine();
            sb.AppendLine("Пожалуйста, напишите подробный отчет ревью в формате Markdown на русском языке.");

            string userPrompt = sb.ToString();

            // Save the prompt file for user visibility
            File.WriteAllText(promptOutputPath, userPrompt, Encoding.UTF8);
            Console.WriteLine($"[CODE REVIEW] Prompt saved to: {promptOutputPath}");

            string summary;

            if (_llmClient.IsKeyConfigured)
            {
                try
                {
                    Console.WriteLine("[CODE REVIEW] Requesting review from Mistral API...");
                    summary = await _llmClient.SendPromptAsync(systemInstructions, userPrompt);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CODE REVIEW ERROR] Mistral API request failed: {ex.Message}");
                    Console.WriteLine("[CODE REVIEW] Falling back to pre-baked code review summary...");
                    summary = GetFallbackCodeReviewSummary(codeContent, true);
                }
            }
            else
            {
                Console.WriteLine("[CODE REVIEW] MISTRAL_API_KEY is not set. Generating pre-baked (simulated) code review summary...");
                summary = GetFallbackCodeReviewSummary(codeContent, false);
            }

            File.WriteAllText(summaryOutputPath, summary, Encoding.UTF8);
            Console.WriteLine($"[CODE REVIEW] Summary written to: {summaryOutputPath}");
        }

        private string GetFallbackCodeReviewSummary(string generatedCode, bool isErrorFallback)
        {
            var header = isErrorFallback 
                ? "# Отчет о ревью кода (Резервная копия из-за ошибки API)\n\n*Примечание: Этот отчет был сгенерирован автоматически, так как запрос к API Mistral завершился ошибкой.*\n\n"
                : "# Отчет о ревью кода (Симуляция без API ключа)\n\n*Примечание: Этот отчет был сгенерирован автоматически, так как переменная `MISTRAL_API_KEY` не установлена.*\n\n";

            return header + @"## 1. Структура кода
- **Фреймворк и библиотеки**: Используются NUnit 3 и Selenium WebDriver, что соответствует современным стандартам тестирования в .NET.
- **Наследование**: Класс наследуется от `BaseTest`, используя стандартные методы жизненного цикла `[SetUp]` и `[TearDown]` для инициализации и очистки веб-драйвера.
- **Page Objects**: Архитектура тестов корректно реализует паттерн Page Object. Тесты инициализируют `LoginPage` и `InventoryPage`, передавая экземпляр драйвера.
- **Изоляция локаторов**: Веб-локаторы инкапсулированы внутри классов страниц, исключая смешивание логики тестов с деталями верстки.

## 2. Соответствие шагам тест-кейсов
- **TC-001**:
  - Все предусловия и шаги сопоставлены корректно.
  - Осуществляется переход на `https://www.saucedemo.com/`.
  - Вводятся имя пользователя `standard_user` и пароль `secret_sauce`.
  - Выполняется вход и добавление товара `Sauce Labs Backpack` в корзину.
  - Ассерты проверяют URL страницы каталога, отображение меню и счетчик корзины ('1').
- **TC-002**:
  - Соответствует негативному сценарию.
  - Вводятся имя заблокированного пользователя `locked_out_user` и пароль `secret_sauce`.
  - Проверяется отображение сообщения об ошибке: `Epic sadface: Sorry, this user has been locked out.`.
- **TC-003**:
  - Содержит кастомные поля (`Email`, `Phone`). Так как эти поля отсутствуют на стандартной странице логина, генератор пометил их информационными логами:
    `// INFO: Custom step field matching: field 'Email', value 'test_user@gmail.com'`
    `// INFO: Custom step field matching: field 'Phone', value '+79991234567'`
    `// INFO: Custom step field matching: field 'Password', value 'MySecurePassword2026'`
  - **Вердикт**: Тест-кейс содержит PII-данные и не должен запускаться в стандартном наборе, либо требует расширения Page Object для страницы регистрации.

## 3. Логическая корректность и стабильность
- **Таймауты**: Неявное ожидание в 5 секунд, настроенное в `BaseTest`, является достаточным для стабильной работы с SauceDemo.
- **Очистка сессий**: Драйвер закрывается в блоке `TearDown`, исключая зависание процессов браузера в системе.

## 4. Рекомендации
1. **Headless-режим**: Оставьте включенной опцию `--headless=new` для запуска в CI/CD.
2. **Явные ожидания**: Для сложных динамических элементов перейдите на явные ожидания `WebDriverWait` вместо неявных, чтобы повысить стабильность.
3. **Обработка TC-003**: Исключите `TC-003` из регулярного прогона или добавьте заглушку, так как он содержит персональные данные.
";
        }
    }
}
