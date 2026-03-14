using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;

namespace Hooked.Shared.Services.AI
{
    public sealed class GeminiFishSpeciesService : IGeminiFishSpeciesService
    {
        private const string ModelName = "gemini-2.5-flash";
        private const string RateLimitMessage = "Gemini rate limit reached. Please wait a moment and try again.";
        private const string ModelNotFoundMessage = "Configured Gemini model is unavailable for this API key.";
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

            GenerateContentResponse response;

            try
            {
                response = await _client.Models.GenerateContentAsync(
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
                                    InlineData = new Blob
                                    {
                                        Data = imageBytes,
                                        MimeType = mimeType
                                    }
                                }
                            }
                        }
                    }).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests || ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(RateLimitMessage, ex);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(ModelNotFoundMessage, ex);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(RateLimitMessage, ex);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(ModelNotFoundMessage, ex);
            }

            var species = response?.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(species))
            {
                throw new InvalidOperationException("Scanner could not identify a species from this photo.");
            }

            return species;
        }

        public async Task<string> DescribeFishSpeciesAsync(string speciesName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(speciesName))
            {
                throw new ArgumentException("Species name is required.", nameof(speciesName));
            }

            if (_client is null)
            {
                throw new InvalidOperationException("Gemini API key is not configured.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            GenerateContentResponse response;

            try
            {
                response = await _client.Models.GenerateContentAsync(
                    model: ModelName,
                    contents: new List<Content>
                    {
                        new()
                        {
                            Parts = new List<Part>
                            {
                                new()
                                {
                                    Text = $"Describe the physical characteristics, exact natural colors, and fin shape of a '{speciesName}' fish in exactly 20 words or less. Focus strictly on visual anatomy."
                                }
                            }
                        }
                    }).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests || ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(RateLimitMessage, ex);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(ModelNotFoundMessage, ex);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(RateLimitMessage, ex);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(ModelNotFoundMessage, ex);
            }

            var description = response?.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new InvalidOperationException($"Could not generate a description for the species '{speciesName}'.");
            }

            return description;
        }
    }
}