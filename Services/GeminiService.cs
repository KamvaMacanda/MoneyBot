using System.Text;
using System.Text.Json;

namespace MoneyBot.Services
{
    /// <summary>
    /// Service integrating Google Gemini API for chatbot banking telemetry and financial guidance.
    /// </summary>
    public class GeminiService
    {
        private readonly HttpClient _http;
        private readonly string? _apiKey;
        private readonly ILogger<GeminiService> _logger;

        /// <summary>
        /// Flag indicating whether Gemini API credits/quota are exhausted for the day.
        /// </summary>
        public bool IsQuotaExceeded { get; private set; }

        public GeminiService(HttpClient http, IConfiguration config, ILogger<GeminiService> logger)
        {
            _http = http;
       
            // Reads from environment variable GEMINI_API_KEY, fallback to appsettings

            _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                      ?? config["Gemini:ApiKey"];

            _logger = logger;
        }

        /// <summary>
        /// Sends user question and recent financial summary to Gemini model.
        /// Returns null if offline, key is missing, or credits/quota are exhausted.
        /// </summary>
        public async Task<string?> AskGeminiAsync(string userMessage, string financialContext)
        {
            if (IsQuotaExceeded || string.IsNullOrWhiteSpace(_apiKey))
            {
                return null;
            }

            try
            {
                var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";
                var systemPrompt = "You are MoneyBot, a friendly and accurate personal financial assistant. " +
                                   "Answer the user's questions clearly based on their South African Rand (ZAR) spending and bank data provided below.\n\n" +
                                   $"Financial Context:\n{financialContext}";

                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = $"{systemPrompt}\n\nUser Question: {userMessage}" }
                            }
                        }
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(endpoint, content);

                //  credit limit reached (HTTP 429 Too Many Requests or 403 Forbidden)
                if ((int)response.StatusCode == 429 || (int)response.StatusCode == 403)
                {
                    _logger.LogWarning("Gemini API quota exceeded or credits depleted. Flagging IsQuotaExceeded = true.");
                    IsQuotaExceeded = true;
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Gemini API call failed with status {StatusCode}: {Details}", response.StatusCode, errorDetails);
                    return null;
                }

                var jsonDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var candidates = jsonDoc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() > 0)
                {
                    var text = candidates[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();
                    return text;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while invoking Gemini API.");
            }

            return null;
        }
    }
}