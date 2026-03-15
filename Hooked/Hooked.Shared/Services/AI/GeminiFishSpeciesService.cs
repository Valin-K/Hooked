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

        public async Task<FishBoundingBoxDto?> DetectObjectBoundingBoxAsync(byte[] imageBytes, string mimeType = "image/jpeg", string objectHint = "fish", CancellationToken cancellationToken = default)
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

            var prompt =
                $"Detect the {objectHint} in this image. " +
                "Return ONLY a JSON object with integer fields y0, x0, y1, x1 (normalized 0-1000) and a boolean field detected. " +
                "Example: {\"detected\":true,\"y0\":120,\"x0\":80,\"y1\":650,\"x1\":920} " +
                $"If no {objectHint} is visible return: {{\"detected\":false,\"y0\":0,\"x0\":0,\"y1\":0,\"x1\":0}}";

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
                                new() { Text = prompt },
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

            var rawText = response?.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return null;
            }

            return TryParseJsonBoundingBox(rawText, imageWidth, imageHeight)
                ?? TryParseArrayBoundingBox(rawText, imageWidth, imageHeight); // fallback for unexpected array format
        }

        /// <summary>
        /// Primary parser — expects {"detected":true,"y0":N,"x0":N,"y1":N,"x1":N}.
        /// Strips any markdown code fences Gemini may add around the JSON.
        /// </summary>
        private static FishBoundingBoxDto? TryParseJsonBoundingBox(string text, int imageWidth, int imageHeight)
        {
            // Strip optional ```json ... ``` fences
            var jsonText = Regex.Replace(text, @"```(?:json)?|```", "").Trim();

            // Extract the first {...} block in case Gemini adds prose around it
            var braceMatch = Regex.Match(jsonText, @"\{[^{}]+\}");
            if (!braceMatch.Success)
            {
                return null;
            }

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(braceMatch.Value);
                var root = doc.RootElement;

                if (root.TryGetProperty("detected", out var detectedProp) && !detectedProp.GetBoolean())
                {
                    return null;
                }

                var y0 = root.GetProperty("y0").GetInt32();
                var x0 = root.GetProperty("x0").GetInt32();
                var y1 = root.GetProperty("y1").GetInt32();
                var x1 = root.GetProperty("x1").GetInt32();

                if (y0 == 0 && x0 == 0 && y1 == 0 && x1 == 0)
                {
                    return null;
                }

                return new FishBoundingBoxDto(y0, x0, y1, x1, imageWidth, imageHeight);
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Fallback parser — handles the legacy [y0, x0, y1, x1] array format.
        /// </summary>
        private static FishBoundingBoxDto? TryParseArrayBoundingBox(string text, int imageWidth, int imageHeight)
        {
            if (text.Contains("none", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var match = Regex.Match(text, @"\[(\d+),\s*(\d+),\s*(\d+),\s*(\d+)\]");
            if (!match.Success)
            {
                return null;
            }

            return new FishBoundingBoxDto(
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value),
                int.Parse(match.Groups[4].Value),
                imageWidth,
                imageHeight);
        }
    }
}