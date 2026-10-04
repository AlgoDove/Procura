namespace Procura.API.AI.Gemini
{
    public interface IGeminiClientFactory
    {
        /// <summary>
        /// Creates a Gemini client configured specifically for the target agent role.
        /// </summary>
        IGeminiClient CreateClient(GeminiAgentRole role);
    }
}
