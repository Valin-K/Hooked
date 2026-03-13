using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;

namespace Hooked.Shared.Services.AI
{
    public sealed class GeminiFishSpeciesService : IGeminiFishSpeciesService
    {
        private const string ModelName = "gemini-2.0-flash";
        private readonly Client? _client;

        public GeminiFishSpeciesService(string? apiKey)
        {
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                _client = new Client(apiKey: apiKey);
            }
        }

        public async Task<string> IdentifyFishSpeciesAsync(byte[] imageBytes, string mimeType = "image/jpeg", CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(imageBytes);

            if (imageBytes.Length == 0)
            {
                throw new ArgumentException("Image bytes are required.", nameof(imageBytes));
            }

            if (string.IsNullOrWhiteSpace(mimeType))
            {
                throw new ArgumentException("MIME type is required.", nameof(mimeType));
            }

            if (_client is null)
            {
                throw new InvalidOperationException("Gemini API key is not configured.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var dataUri = $"data:{mimeType};base64,{Convert.ToBase64String(imageBytes)}";
            var response = await _client.Models.GenerateContentAsync(
                model: ModelName,
                contents: new List<Content>
                {
                    new()
                    {
                        Parts = new List<Part>
                        {
                            new()
                            {
                                Text = "Return only the common fish species name shown in this image. Example: Rainbow trout. If unknown, return Unknown."
                            },
                            new()
                            {
                                FileData = new FileData
                                {
                                    FileUri = dataUri,
                                    MimeType = mimeType
                                }
                            }
                        }
                    }
                }).ConfigureAwait(false);

            var species = response?.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim();
            return string.IsNullOrWhiteSpace(species) ? "Unknown" : species;
        }
    }
}
