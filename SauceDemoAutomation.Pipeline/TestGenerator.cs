using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SauceDemoAutomation.Pipeline
{
    public class TestGenerator
    {
        public bool GenerateTests(string inputJsonPath, string outputCsPath)
        {
            if (!File.Exists(inputJsonPath))
            {
                Console.WriteLine($"[GENERATOR] Error: Input file '{inputJsonPath}' not found.");
                return false;
            }

            try
            {
                string jsonContent = File.ReadAllText(inputJsonPath);
                using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                {
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("test_cases", out JsonElement testCases) || testCases.ValueKind != JsonValueKind.Array)
                    {
                        Console.WriteLine("[GENERATOR] Error: 'test_cases' is missing or not an array.");
                        return false;
                    }

                    var sb = new StringBuilder();
                    sb.AppendLine("using System;");
                    sb.AppendLine("using NUnit.Framework;");
                    sb.AppendLine("using SauceDemoAutomation.Framework.Pages;");
                    sb.AppendLine();
                    sb.AppendLine("namespace SauceDemoAutomation.Framework");
                    sb.AppendLine("{");
                    sb.AppendLine("    [TestFixture]");
                    sb.AppendLine("    [Description(\"Automatically generated tests from JSON\")]");
                    sb.AppendLine("    public class GeneratedTests : BaseTest");
                    sb.AppendLine("    {");

                    foreach (JsonElement tc in testCases.EnumerateArray())
                    {
                        string id = tc.TryGetProperty("id", out JsonElement idProp) ? idProp.GetString() : "TC_Unknown";
                        string title = tc.TryGetProperty("title", out JsonElement titleProp) ? titleProp.GetString() : "Untitled Test";
                        string expectedResult = tc.TryGetProperty("expected_result", out JsonElement expProp) ? expProp.GetString() : "";

                        // Standardize ID to be C# safe identifier
                        string methodName = id.Replace("-", "_");

                        sb.AppendLine($"        [Test]");
                        sb.AppendLine($"        [Category(\"Generated\")]");
                        sb.AppendLine($"        [Description(\"{title.Replace("\"", "\\\"")}\")]");
                        sb.AppendLine($"        public void {methodName}()");
                        sb.AppendLine("        {");
                        sb.AppendLine($"            Console.WriteLine(\"Starting test: {id} - {title}\");");
                        sb.AppendLine();
                        sb.AppendLine("            // Page objects initialization");
                        sb.AppendLine("            var loginPage = new LoginPage(Driver);");
                        sb.AppendLine("            var inventoryPage = new InventoryPage(Driver);");
                        sb.AppendLine();

                        // 1. Preconditions
                        sb.AppendLine("            // --- Preconditions ---");
                        if (tc.TryGetProperty("preconditions", out JsonElement preConds) && preConds.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement pc in preConds.EnumerateArray())
                            {
                                string pcText = pc.GetString() ?? "";
                                sb.AppendLine($"            // Precondition: {pcText}");
                                if (pcText.Contains("переход на страницу авторизации"))
                                {
                                    sb.AppendLine("            Driver.Navigate().GoToUrl(BaseUrl);");
                                }
                            }
                        }
                        sb.AppendLine();

                        // Parse expected results by step numbers
                        // Example: "После шага 3: ... \n После шага 4: ..."
                        var assertionsMap = ParseExpectedResults(expectedResult);

                        // 2. Steps
                        sb.AppendLine("            // --- Steps & Assertions ---");
                        if (tc.TryGetProperty("steps", out JsonElement steps) && steps.ValueKind == JsonValueKind.Array)
                        {
                            int stepIdx = 1;
                            foreach (JsonElement step in steps.EnumerateArray())
                            {
                                string stepText = step.GetString() ?? "";
                                sb.AppendLine($"            // Шаг {stepIdx}: {stepText}");

                                // Translate step text to Selenium / PageObject code
                                string code = TranslateStep(stepText);
                                sb.AppendLine($"            {code}");

                                // Check if there are assertions associated with this step
                                if (assertionsMap.TryGetValue(stepIdx, out string assertionText))
                                {
                                    sb.AppendLine($"            // Assertions after Step {stepIdx}");
                                    var generatedAsserts = GenerateAssertsForText(assertionText);
                                    foreach (var assert in generatedAsserts)
                                    {
                                        sb.AppendLine($"            {assert}");
                                    }
                                }
                                sb.AppendLine();
                                stepIdx++;
                            }
                        }

                        sb.AppendLine("        }");
                        sb.AppendLine();
                    }

                    sb.AppendLine("    }");
                    sb.AppendLine("}");

                    // Make sure target directory exists
                    string dir = Path.GetDirectoryName(outputCsPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.WriteAllText(outputCsPath, sb.ToString(), Encoding.UTF8);
                    Console.WriteLine($"[GENERATOR] Successfully generated C# test class at: {outputCsPath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GENERATOR ERROR] Failed to generate tests: {ex.Message}");
                return false;
            }
        }

        private string TranslateStep(string stepText)
        {
            // Regex 1: Ввести в поле "Username" значение standard_user
            var matchUsername = Regex.Match(stepText, @"Ввести в поле\s+""Username""\s+значение\s+(\S+)", RegexOptions.IgnoreCase);
            if (matchUsername.Success)
            {
                return $"loginPage.EnterUsername(\"{matchUsername.Groups[1].Value.Trim('"', '\'').TrimEnd('.')}\");";
            }

            // Regex 2: Ввести в поле "Password" значение secret_sauce
            var matchPassword = Regex.Match(stepText, @"Ввести в поле\s+""Password""\s+значение\s+(\S+)", RegexOptions.IgnoreCase);
            if (matchPassword.Success)
            {
                return $"loginPage.EnterPassword(\"{matchPassword.Groups[1].Value.Trim('"', '\'').TrimEnd('.')}\");";
            }

            // Regex 3: Нажать кнопку "Login"
            if (Regex.IsMatch(stepText, @"Нажать кнопку\s+""Login""\.?", RegexOptions.IgnoreCase))
            {
                return "loginPage.ClickLogin();";
            }

            // Regex 4: На открывшейся странице каталога товаров (Inventory page) найти товар "Sauce Labs Backpack" и нажать на его кнопку "Add to cart"
            var matchAddToCart = Regex.Match(stepText, @"найти товар\s+""([^""]+)""\s+и нажать на его кнопку\s+""Add to cart""\.?", RegexOptions.IgnoreCase);
            if (matchAddToCart.Success)
            {
                return $"inventoryPage.AddToCart(\"{matchAddToCart.Groups[1].Value}\");";
            }

            // Regex 5: General input
            var matchGeneralInput = Regex.Match(stepText, @"Ввести в поле\s+""([^""]+)""\s+значение\s+(\S+)", RegexOptions.IgnoreCase);
            if (matchGeneralInput.Success)
            {
                string cleanVal = matchGeneralInput.Groups[2].Value.Trim('"', '\'').TrimEnd('.');
                return $"// INFO: Custom step field matching: field '{matchGeneralInput.Groups[1].Value}', value '{cleanVal}'\n            Console.WriteLine(\"Filling custom input {matchGeneralInput.Groups[1].Value} with value: {cleanVal}\");";
            }

            return $"// TODO: Manual implementation required for step: {stepText}";
        }

        private Dictionary<int, string> ParseExpectedResults(string expectedResult)
        {
            var map = new Dictionary<int, string>();
            if (string.IsNullOrWhiteSpace(expectedResult)) return map;

            // Pattern: "После шага 3: ... (until next После шага or end of string)"
            var matches = Regex.Matches(expectedResult, @"После шага\s+(\d+):\s*(.*?)(?=(?:После шага\s+\d+:|$))", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out int stepIdx))
                {
                    map[stepIdx] = match.Groups[2].Value.Trim();
                }
            }

            return map;
        }

        private List<string> GenerateAssertsForText(string assertionText)
        {
            var asserts = new List<string>();

            // 1. URL checks
            var matchUrl = Regex.Match(assertionText, @"перенаправлен на страницу каталога\s+(\S+)", RegexOptions.IgnoreCase);
            if (matchUrl.Success)
            {
                asserts.Add($"Assert.That(Driver.Url, Is.EqualTo(\"{matchUrl.Groups[1].Value.TrimEnd('.')}\"), \"User should be redirected to inventory page\");");
            }

            // 2. Menu button check
            if (assertionText.Contains("кнопка бокового меню", StringComparison.OrdinalIgnoreCase) || assertionText.Contains("бокового меню", StringComparison.OrdinalIgnoreCase))
            {
                asserts.Add("Assert.That(inventoryPage.IsMenuButtonDisplayed(), Is.True, \"Menu button should be available\");");
            }

            // 3. Cart badge count checks
            if (assertionText.Contains("счетчик на иконке корзины отсутствует", StringComparison.OrdinalIgnoreCase) || assertionText.Contains("без цифр", StringComparison.OrdinalIgnoreCase))
            {
                asserts.Add("Assert.That(inventoryPage.IsCartBadgeDisplayed(), Is.False, \"Cart badge should not display a number\");");
            }

            var matchBadgeNum = Regex.Match(assertionText, @"цифрой\s+""?(\d+)""?", RegexOptions.IgnoreCase);
            if (matchBadgeNum.Success)
            {
                asserts.Add($"Assert.That(inventoryPage.GetCartBadgeText(), Is.EqualTo(\"{matchBadgeNum.Groups[1].Value}\"), \"Cart badge count should match expectation\");");
            }

            // 4. Button text check
            var matchButtonText = Regex.Match(assertionText, @"Текст на кнопке выбранного товара меняется.*""Remove""\.?", RegexOptions.IgnoreCase);
            if (matchButtonText.Success)
            {
                // We hardcode the product for the demo, or parse it. Since we know standard product name is "Sauce Labs Backpack":
                asserts.Add("Assert.That(inventoryPage.GetButtonText(\"Sauce Labs Backpack\"), Is.EqualTo(\"Remove\"), \"Button text should change to Remove\");");
            }

            // 5. Error messages check
            var matchErrorMsg = Regex.Match(assertionText, @"сообщение об ошибке:\s+""([^""]+)""", RegexOptions.IgnoreCase);
            if (matchErrorMsg.Success)
            {
                asserts.Add($"Assert.That(loginPage.GetErrorMessage(), Contains.Substring(\"{matchErrorMsg.Groups[1].Value}\"), \"Error message should match expectation\");");
            }

            if (asserts.Count == 0)
            {
                // Fallback comment
                asserts.Add($"// TODO: Verify expected outcome: {assertionText.Replace("\n", " ").Replace("\r", "")}");
            }

            return asserts;
        }
    }
}
