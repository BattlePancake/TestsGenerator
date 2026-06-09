using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SauceDemoAutomation.Pipeline
{
    public class LlmClient
    {
        private readonly string _apiKey;
        private readonly string _model;
        private static readonly HttpClient HttpClient = new HttpClient();

        public LlmClient()
        {
            // Read from environment variable
            _apiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY");
            
            // Check for MISTRAL_MODEL env var or default to mistral-medium
            _model = Environment.GetEnvironmentVariable("MISTRAL_MODEL") ?? "mistral-medium";
        }

        public bool IsKeyConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        public async Task<string> SendPromptAsync(string systemInstructions, string promptContent)
        {
            if (!IsKeyConfigured)
            {
                throw new InvalidOperationException("Mistral API Key is not configured in environment variables.");
            }

            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemInstructions },
                    new { role = "user", content = promptContent }
                },
                temperature = 0.2
            };

            var jsonPayload = JsonSerializer.Serialize(requestBody);
            
            using (var request = new HttpRequestMessage(HttpMethod.Post, "https://api.mistral.ai/v1/chat/completions"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await HttpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Mistral API returned status {response.StatusCode}: {errorMsg}");
                }

                string responseString = await response.Content.ReadAsStringAsync();
                using (var doc = JsonDocument.Parse(responseString))
                {
                    var choices = doc.RootElement.GetProperty("choices");
                    if (choices.GetArrayLength() > 0)
                    {
                        var message = choices[0].GetProperty("message");
                        return message.GetProperty("content").GetString();
                    }
                }
            }

            throw new Exception("Received empty response or choices from Mistral API.");
        }
    }
}
