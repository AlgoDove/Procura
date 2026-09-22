using System.Threading;
using System.Threading.Tasks;

namespace Procura.API.AI.Gemini
{
    public interface IGeminiClient
    {
        Task<GeminiClientResult> GenerateContentAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
    }
}
