namespace MoneyBot.Services
{
    /// <summary>
    /// Service integrating Google Gemini API for conversational banking telemetry 
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


    }
}
