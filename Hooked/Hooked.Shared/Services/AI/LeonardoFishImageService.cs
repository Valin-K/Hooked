using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hooked.Shared.Services.AI
{
    public sealed class LeonardoFishImageService : ILeonardoFishImageService
    {
        private const int MaxPollAttempts = 20;
        private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(1);
        private static readonly Uri BaseUri = new("https://cloud.leonardo.ai/api/rest/v1/");

        private readonly string _apiKey;
        private readonly string _referenceImageId;
        private readonly IGeminiFishSpeciesService _geminiService;

        public LeonardoFishImageService(
            string? apiKey,
            string? referenceImageId,
            IGeminiFishSpeciesService geminiService)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Leonardo API key is not configured.");
            }

            if (string.IsNullOrWhiteSpace(referenceImageId))
            {
                throw new InvalidOperationException("Leonardo reference image id is not configured.");
            }

            _apiKey = apiKey;
            _referenceImageId = referenceImageId;
            _geminiService = geminiService ?? throw new ArgumentNullException(nameof(geminiService));
        }

        /// <summary>
        /// Generates a fish illustration URL for a species using the configured Leonardo reference image.
        /// </summary>
        public async Task<string> GenerateFishImageUrlAsync(
            string speciesName,
            Func<string, Task>? onLog = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(speciesName))
            {
                throw new ArgumentException("Species name is required.", nameof(speciesName));
            }

            cancellationToken.ThrowIfCancellationRequested();
            await LogAsync(onLog, $"Leonardo start: species='{speciesName}'.").ConfigureAwait(false);

            // 1. Get visual description from Gemini
            await LogAsync(onLog, "Requesting physical description from Gemini...").ConfigureAwait(false);
            var fishDescription = await _geminiService.DescribeFishSpeciesAsync(speciesName, cancellationToken).ConfigureAwait(false);
            await LogAsync(onLog, $"Description received: {fishDescription}").ConfigureAwait(false);

            // 2. Set up Leonardo Client
            using var httpClient = new HttpClient
            {
                BaseAddress = BaseUri,
                Timeout = TimeSpan.FromSeconds(60)
            };

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // 3. Create Generation
            var generationId = await CreateGenerationAsync(httpClient, speciesName, fishDescription, onLog, cancellationToken).ConfigureAwait(false);
            await LogAsync(onLog, $"Leonardo generation created: {generationId}").ConfigureAwait(false);

            // 4. Poll for Result
            for (var attempt = 1; attempt <= MaxPollAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await LogAsync(onLog, $"Leonardo poll {attempt}/{MaxPollAttempts}.").ConfigureAwait(false);

                var url = await TryGetGeneratedImageUrlAsync(httpClient, generationId, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(url))
                {
                    await LogAsync(onLog, "Leonardo image ready.").ConfigureAwait(false);
                    return url;
                }

                await Task.Delay(PollDelay, cancellationToken).ConfigureAwait(false);
            }

            throw new InvalidOperationException("Leonardo image generation timed out.");
        }

        private async Task<string> CreateGenerationAsync(
            HttpClient httpClient,
            string speciesName,
            string fishDescription,
            Func<string, Task>? onLog,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(httpClient);

            var payload = new Dictionary<string, object?>
            {
                ["prompt"] = BuildPrompt(speciesName, fishDescription),
                ["negative_prompt"] = BuildNegativePrompt(),
                // Explicitly use AlbedoBase XL (an SDXL model perfect for 1024x1024 illustrations)
                ["modelId"] = "2067ae52-33fd-4a82-bb92-c2c55e7d2786",
                ["num_images"] = 1,
                ["height"] = 1024,
                ["width"] = 1024,

                // Turn off legacy settings that break SDXL
                ["promptMagic"] = false,
                ["enhancePrompt"] = false,

                // Use the modern ControlNet array for Style Reference
                ["controlnets"] = new[]
                {
                    new
                    {
                        initImageId = _referenceImageId,
                        // IMPORTANT: Change to "GENERATED" if you made the reference image inside Leonardo
                        initImageType = "UPLOADED",
                        preprocessorId = 67, // ID 67 is Leonardo's official 'Style Reference' for SDXL
                        strengthType = "Low" // Options: Low, Mid, High, Ultra, Max. Starts 'Low' so the fish anatomy survives!
                    }
                }
            };

            await LogAsync(onLog, "Leonardo settings: AlbedoBase XL, ControlNet Style Reference (ID 67), Strength=Low.").ConfigureAwait(false);

            var requestJson = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, "generations")
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            await LogAsync(onLog, $"Leonardo create status: {(int)response.StatusCode}").ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Leonardo create generation failed: {(int)response.StatusCode} {response.ReasonPhrase}. Response: {Truncate(responseText, 300)}");
            }

            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            var generationId = GetNestedString(
                root,
                new[] { "sdGenerationJob", "generationId" },
                new[] { "sd_generation_job", "generation_id" });

            if (string.IsNullOrWhiteSpace(generationId))
            {
                throw new InvalidOperationException($"Leonardo did not return a generation id. Response: {Truncate(responseText, 300)}");
            }

            return generationId;
        }

        private static async Task<string?> TryGetGeneratedImageUrlAsync(
            HttpClient httpClient,
            string generationId,
            CancellationToken cancellationToken)
        {
            using var response = await httpClient.GetAsync($"generations/{generationId}", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;

            if (!TryGetNestedElement(root, out var imagesElement,
                    new[] { "generationsByPk", "generatedImages" },
                    new[] { "generations_by_pk", "generated_images" }))
            {
                return null;
            }

            if (imagesElement.ValueKind != JsonValueKind.Array || imagesElement.GetArrayLength() == 0)
            {
                return null;
            }

            var firstImage = imagesElement[0];
            if (TryGetPropertyCaseInsensitive(firstImage, "url", out var urlElement)
                && urlElement.ValueKind == JsonValueKind.String)
            {
                return urlElement.GetString();
            }

            return null;
        }

        private static string? GetNestedString(JsonElement root, params string[][] paths)
        {
            foreach (var path in paths)
            {
                if (TryGetNestedElement(root, out var value, path) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }
            }

            return null;
        }

        private static bool TryGetNestedElement(JsonElement root, out JsonElement result, params string[][] paths)
        {
            foreach (var path in paths)
            {
                if (TryGetNestedElement(root, out result, path))
                {
                    return true;
                }
            }

            result = default;
            return false;
        }

        private static bool TryGetNestedElement(JsonElement root, out JsonElement result, string[] path)
        {
            result = root;

            foreach (var segment in path)
            {
                if (!TryGetPropertyCaseInsensitive(result, segment, out result))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }

        private static Task LogAsync(Func<string, Task>? onLog, string message)
        {
            return onLog is null ? Task.CompletedTask : onLog(message);
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value[..maxLength] + "...";
        }

        private static string BuildPrompt(string speciesName, string fishDescription)
        {
            return $"A single, solo vector art illustration of EXACTLY ONE {speciesName}. " +
                   $"The fish MUST be perfectly horizontal and level on the axis, not angled. " +
                   $"Strict full-body side profile view with the head facing left. " +
                   $"Anatomical and color traits MUST follow this description exactly: ({fishDescription}:1.3). " +
                   $"The drawing style should be a very simple, flat 2D cartoon, with solid colors, clean outlines, absolutely no gradients, and minimalistic details, matching the line-art style of the reference image exactly. " +
                   $"Isolated on a pure, plain white background.";
        }

        private static string BuildNegativePrompt()
        {
            return "multiple fish, two fish, group of fish, school of fish, sticker sheet, collection, collage, split screen, " +
                   "facing right, angled, diagonal, tilted, swimming up, swimming down, perspective, foreshortening, " +
                   "realistic, 3d, photograph, text, watermark, logo, deformed fish anatomy, " +
                   "complicated shading, gradients, shadow, background environment";
        }
    }
}