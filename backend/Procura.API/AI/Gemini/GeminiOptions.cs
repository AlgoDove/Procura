namespace Procura.API.AI.Gemini
{
    public class GeminiOptions
    {
        public string? ApiKey { get; set; }
        public string? Model { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
        public int MaxRetries { get; set; } = 2;
    }
}
