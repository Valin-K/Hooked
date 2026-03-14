using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;
using SixLabors.ImageSharp;

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

        public async Task<string> GetEnvironmentalImpactAsync(string speciesName, double? lengthMeters, string? locationJson, CancellationToken cancellationToken = default)
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

            var prompt = $"Is the fish species '{speciesName}' invasive or endangered";
            if (!string.IsNullOrWhiteSpace(locationJson))
            {
                prompt += $" in the location described by {locationJson}?";
            }
            else
            {
                prompt += "?";
            }

            if (lengthMeters.HasValue)
            {
                prompt += $" The caught fish is {lengthMeters.Value} meters long. Are there any size limits or regulations I should be aware of?";
            }
            
            prompt += " Provide a short, direct answer in 2 or 3 sentences max warning the user if they should not throw it back or if they must throw it back because of regulations.";

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
                                    Text = prompt
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

            var result = response?.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(result))
            {
                throw new InvalidOperationException($"Could not generate an environmental impact for the species '{speciesName}'.");
            }

            // Remove excessive markdown like **bold** usually returned by Gemini
            result = result.Replace("**", "").Replace("*", "");

            return result;
        }

        public async Task<FishBoundingBoxDto?> DetectFishBoundingBoxAsync(byte[] imageBytes, string mimeType = "image/jpeg", CancellationToken cancellationToken = default)
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

            // Get image dimensions
            int imageWidth, imageHeight;
            using (var memoryStream = new MemoryStream(imageBytes))
            using (var image = SixLabors.ImageSharp.Image.Load(memoryStream))
            {
                imageWidth = image.Width;
                imageHeight = image.Height;
            }

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
                                    Text = "Detect the fish in this image and return ONLY the bounding box coordinates in this exact format: [y0, x0, y1, x1] where coordinates are normalized integers between 0 and 1000. Return ONLY the numbers in brackets, nothing else. If no fish is detected, return: none"
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

            var boxText = response?.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(boxText) || boxText.Contains("none", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Parse the bounding box coordinates [y0, x0, y1, x1]
            var match = Regex.Match(boxText, @"\[(\d+),\s*(\d+),\s*(\d+),\s*(\d+)\]");
            if (!match.Success)
            {
                return null;
            }

            var y0 = int.Parse(match.Groups[1].Value);
            var x0 = int.Parse(match.Groups[2].Value);
            var y1 = int.Parse(match.Groups[3].Value);
            var x1 = int.Parse(match.Groups[4].Value);

            return new FishBoundingBoxDto(y0, x0, y1, x1, imageWidth, imageHeight);
        }
    }
}