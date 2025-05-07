using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Server.Controllers
{
    public class NoteRequest
    {
        public string Speciality { get; set; }
        public string Symptoms { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class NoteController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public NoteController(IConfiguration config)
        {
            _httpClient = new HttpClient();
            _config = config;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateNote([FromBody] NoteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Speciality) || string.IsNullOrWhiteSpace(request.Symptoms))
                return BadRequest("Speciality and symptoms are required.");

            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var systemPrompt = request.Speciality switch
            {
                "gyne" => "You are a gynecology medical assistant. Generate a templated clinical note. Use placeholders like {age}, {name}, {symptoms}, etc. Suggestions like treatment plan should be added in light grey using HTML <span style='color:grey'>...</span> for easy UI differentiation.",
                "cardio" => "You are a cardiology medical assistant. Generate a templated clinical note. Use placeholders like {age}, {name}, {symptoms}, etc. Suggestions like treatment plan should be added in light grey using HTML <span style='color:grey'>...</span>.",
                _ => "You are a medical assistant. Generate a templated clinical note using placeholders and light grey suggestions."
            };

            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = request.Symptoms }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Failed to get a response from OpenAI.");

            var responseContent = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseContent);
            var note = doc.RootElement
                          .GetProperty("choices")[0]
                          .GetProperty("message")
                          .GetProperty("content")
                          .GetString();

            return Ok(note);
        }
    }
}
