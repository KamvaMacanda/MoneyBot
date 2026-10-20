
using System.Text;
using System.Text.Json;
namespace MoneyBot.Services
{
    /// <summary>
    /// Service integrating Google Gemini API for  chatbot  banking telemetry 
    /// and financial guidance.
    public class GeminiService
    {

        private readonly HttpClient _http;
        private readonly string? _apiKey;
        private readonly ILogger<GeminiService> _logger;
        public GeminiService(HttpClient http, IConfiguration config, ILogger<GeminiService> logger)
        {
            _http = http;
            _apiKey = config["Gemini:ApiKey"];
            _logger = logger;
        } 
    
        /// Sends user question and recent financial summary to Gemini model.
        /// </summary>
        public async Task<string?> AskGeminiAsync(string userMessage, string financialContext)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("Gemini API key is not configured in appsettings.json.");
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
