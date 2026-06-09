using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SauceDemoAutomation.Pipeline
{
    public class PiiGuardrail
    {
        // PII Regex Patterns
        private static readonly Regex EmailRegex = new Regex(
            @"\b[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}\b", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex PhoneRegex = new Regex(
            @"\b(?:\+?\d{1,3}[- .]?)?\(?\d{3}\)?[- .]?\d{3}[- .]?\d{4}\b", 
            RegexOptions.Compiled);

        private static readonly Regex CreditCardRegex = new Regex(
            @"\b(?:\d{4}[- ]?){3}\d{4}\b", 
            RegexOptions.Compiled);

        // Keywords indicating sensitive parameters in steps/text
        private static readonly string[] SensitiveKeywords = { 
            "password", "пароль", "secret", "token", "токен", "api_key", "apikey" 
        };

        public class PiiFinding
        {
            public string TestCaseId { get; set; }
            public string Location { get; set; } // e.g. "Step 2", "Preconditions", "Title"
            public string PatternType { get; set; } // e.g. "Email", "Phone", "Password"
            public string MatchedValue { get; set; }
        }

        public bool ScanFile(string filePath, out List<PiiFinding> findings)
        {
            findings = new List<PiiFinding>();
            
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[PII GUARD] Error: File '{filePath}' does not exist.");
                return false;
            }

            try
            {
                string jsonString = File.ReadAllText(filePath);
                using (JsonDocument doc = JsonDocument.Parse(jsonString))
                {
                    JsonElement root = doc.RootElement;
                    if (!root.TryGetProperty("test_cases", out JsonElement testCases) || testCases.ValueKind != JsonValueKind.Array)
                    {
                        Console.WriteLine("[PII GUARD] Warning: 'test_cases' array not found in JSON root.");
                        return true;
                    }

                    foreach (JsonElement tc in testCases.EnumerateArray())
                    {
                        string id = tc.TryGetProperty("id", out JsonElement idProp) ? idProp.GetString() : "Unknown";
                        string title = tc.TryGetProperty("title", out JsonElement titleProp) ? titleProp.GetString() : "";
                        
                        // Scan title
                        ScanText(id, "Title", title, findings);

                        // Scan preconditions
                        if (tc.TryGetProperty("preconditions", out JsonElement preConds) && preConds.ValueKind == JsonValueKind.Array)
                        {
                            int idx = 1;
                            foreach (JsonElement pc in preConds.EnumerateArray())
                            {
                                ScanText(id, $"Precondition {idx++}", pc.GetString(), findings);
                            }
                        }

                        // Scan steps
                        if (tc.TryGetProperty("steps", out JsonElement steps) && steps.ValueKind == JsonValueKind.Array)
                        {
                            int idx = 1;
                            foreach (JsonElement step in steps.EnumerateArray())
                            {
                                ScanText(id, $"Step {idx++}", step.GetString(), findings);
                            }
                        }

                        // Scan expected result
                        string expected = tc.TryGetProperty("expected_result", out JsonElement expProp) ? expProp.GetString() : "";
                        ScanText(id, "Expected Result", expected, findings);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PII GUARD ERROR] Failed to parse and scan JSON: {ex.Message}");
                return false;
            }

            return findings.Count == 0;
        }

        private void ScanText(string tcId, string location, string text, List<PiiFinding> findings)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // 1. Email Check
            foreach (Match match in EmailRegex.Matches(text))
            {
                findings.Add(new PiiFinding
                {
                    TestCaseId = tcId,
                    Location = location,
                    PatternType = "Email Address",
                    MatchedValue = match.Value
                });
            }

            // 2. Phone Check
            foreach (Match match in PhoneRegex.Matches(text))
            {
                findings.Add(new PiiFinding
                {
                    TestCaseId = tcId,
                    Location = location,
                    PatternType = "Phone Number",
                    MatchedValue = match.Value
                });
            }

            // 3. Credit Card Check
            foreach (Match match in CreditCardRegex.Matches(text))
            {
                findings.Add(new PiiFinding
                {
                    TestCaseId = tcId,
                    Location = location,
                    PatternType = "Credit Card",
                    MatchedValue = match.Value
                });
            }

            // 4. Sensitive password/secret keywords context checks
            foreach (var keyword in SensitiveKeywords)
            {
                if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    // Check if it lists a password value that looks like sensitive raw text
                    // (Ignore standard sauce demo passwords like "secret_sauce" or "standard_user" to avoid false positives,
                    // but flag anything else that looks like a custom test secret)
                    var valueMatch = Regex.Match(text, @"(?:значение|value|равно)\s+""?([^""\s\.]+)", RegexOptions.IgnoreCase);
                    if (valueMatch.Success)
                    {
                        string val = valueMatch.Groups[1].Value;
                        if (!val.Equals("secret_sauce", StringComparison.OrdinalIgnoreCase) && 
                            !val.Equals("standard_user", StringComparison.OrdinalIgnoreCase))
                        {
                            findings.Add(new PiiFinding
                            {
                                TestCaseId = tcId,
                                Location = location,
                                PatternType = "Potential Secret/Credential",
                                MatchedValue = val
                            });
                        }
                    }
                }
            }
        }
    }
}
