using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chess.Service
{
    public class IAService
    {
        private readonly HttpClient _httpClient;
        private readonly string _ollamaUrl = "http://localhost:11434/api/generate";

        public IAService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetMove(string fen, string history)
        {
            var requestBody = new
            {
                model = "phi3:mini",
                prompt = $@"Acts as a chess engine.
FEN POSITION: {fen}
HISTORY: {history}

CRITICAL RULE: Respond ONLY with the move in origin-destination format (UCI).
- CORRECT: b8c6
- INCORRECT: Nc6
- Do not include piece names (N, B, R, Q, K).
- DO not answer any recent moves, that are in the history
- Do not include capture symbols (x) or checks (+).
- Do not include introduction, conclusion, explanation
- JUST ANSWER THE MOVE
",
            stream = false,
                options = new { temperature = 0.1 }
            };

            var response = await _httpClient.PostAsJsonAsync(_ollamaUrl, requestBody);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            string aiText = json.GetProperty("response").GetString();
            Console.WriteLine($"AI {history}");

            Console.WriteLine($"AI {fen}");

            Console.WriteLine($"AI {aiText}");
            return ExtractMove(aiText);
        }


        private string ExtractMove(string text)
        {
            var match = Regex.Match(text, @"[a-h][1-8][a-h][1-8][qrbn]?");
            return match.Success ? match.Value.ToLower().Trim() : "";
        }
    }
}