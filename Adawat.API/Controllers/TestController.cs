using Microsoft.AspNetCore.Mvc;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using OllamaSharp.Models;
using System.Text;

namespace Adawat.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ILogger<TestController> _logger;
        private readonly OllamaApiClient _ollama;
        public TestController(ILogger<TestController> logger, OllamaApiClient ollama)
        {
            _ollama = ollama;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var models = await _ollama.ListLocalModelsAsync();

            return Ok(models);
        }


        [HttpPost]
        public async Task<IActionResult> Generate([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest("Message cannot be empty.");
            }

            try
            {
                // If a model is specified in the request, use it, otherwise use the default set in DI
                var modelToUse = !string.IsNullOrWhiteSpace(request.Model) ? request.Model : _ollama.SelectedModel;

                if (string.IsNullOrWhiteSpace(modelToUse))
                {
                    return BadRequest("No model specified and no default model configured.");
                }

                // For simple completion (non-chat, no history)
                // You can also use _ollama.Generate(request.Message, new Ollama.Models.Options { Model = modelToUse });
                // But for a chat-like interaction, using the Chat class is better for history management.
                var chat = new Chat(_ollama);

                // Add previous messages to history
                foreach (var msg in request.History)
                {
                    chat.Messages.Add(msg);
                }

                var fullResponse = new StringBuilder();

                // Stream the response and build the full string
                await foreach (var responsePart in chat.SendAsync(request.Message))
                {
                    fullResponse.Append(responsePart);
                    // In a real API, you might send these chunks back to the client
                    // using Server-Sent Events (SSE) or WebSockets (SignalR) for true streaming UX.
                    // For a simple HTTP API, you accumulate and return once.
                }

                return Ok(new { Response = fullResponse.ToString(), History = chat.Messages }); // Return the full response and updated history
            }
            //catch (OllamaApiException ex)
            //{
            //    // Handle specific Ollama API errors (e.g., model not found)
            //    return StatusCode(500, $"Ollama API Error: {ex.Message}");
            //}
            catch (HttpRequestException ex)
            {
                // Handle network errors (e.g., Ollama server not running)
                return StatusCode(500, $"Network Error connecting to Ollama: {ex.Message}. Is Ollama server running?");
            }
            catch (Exception ex)
            {
                // General error handling
                return StatusCode(500, $"An unexpected error occurred: {ex.Message}");
            }
        }

        // --- Example for Streaming Response via Server-Sent Events (SSE) ---
        // This is more advanced and requires client-side handling (e.g., EventSource in JS)
        // [HttpGet("stream-chat")]
        // public async Task StreamChat(string message, string model = "llama2")
        // {
        //     Response.Headers.Add("Content-Type", "text/event-stream");
        //     Response.Headers.Add("Cache-Control", "no-cache");
        //     Response.Headers.Add("Connection", "keep-alive");
        //
        //     var chat = new Chat(_ollama);
        //     chat.Messages.Add(new Message(AuthorRole.User, message));
        //
        //     await foreach (var token in chat.SendAsync(message, model))
        //     {
        //         var data = $"data: {JsonSerializer.Serialize(new { content = token })}\n\n";
        //         await Response.WriteAsync(data);
        //         await Response.Body.FlushAsync();
        //     }
        //     var done = $"data: {JsonSerializer.Serialize(new { done = true })}\n\n";
        //     await Response.WriteAsync(done);
        //     await Response.Body.FlushAsync();
        // }
    }

    public class ChatRequest
    {
        public required string Message { get; set; }
        public string? Model { get; set; } // Optional: allow client to specify model
        public List<Message> History { get; set; } = [];
    }
}
