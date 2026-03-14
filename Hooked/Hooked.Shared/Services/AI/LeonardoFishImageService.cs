using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

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

                    using var downloadClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                    var transparentPngDataUrl = await TryConvertToTransparentPngDataUrlAsync(
                        downloadClient,
                        url,
                        onLog,
                        cancellationToken).ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(transparentPngDataUrl))
                    {
                        await LogAsync(onLog, "Transparent PNG conversion succeeded.").ConfigureAwait(false);
                        return transparentPngDataUrl;
                    }

                    await LogAsync(onLog, "Transparent PNG conversion skipped; using original image URL.").ConfigureAwait(false);
                    return url;
                }

                await Task.Delay(PollDelay, cancellationToken).ConfigureAwait(false);
            }

            throw new InvalidOperationException("Leonardo image generation timed out.");
        }

        /// <summary>
        /// Converts a generated fish image URL into a transparent PNG data URL.
        /// </summary>
        public async Task<string?> ConvertToTransparentPngDataUrlAsync(
            string imageUrl,
            Func<string, Task>? onLog = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                throw new ArgumentException("Image URL is required.", nameof(imageUrl));
            }

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            return await TryConvertToTransparentPngDataUrlAsync(
                httpClient,
                imageUrl,
                onLog,
                cancellationToken).ConfigureAwait(false);
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
                // Explicitly use AlbedoBase XL (an SDXL model perfect for 1024x1024/1024x768 illustrations)
                ["modelId"] = "2067ae52-33fd-4a82-bb92-c2c55e7d2786",
                ["num_images"] = 1,

                // Landscape resolution to prevent the AI from stacking two fish vertically
                ["width"] = 1024,
                ["height"] = 768,

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

            await LogAsync(onLog, "Leonardo settings: AlbedoBase XL, 1024x768 Landscape, ControlNet Style Reference (ID 67), Strength=Low.").ConfigureAwait(false);

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

        private static async Task<string?> TryConvertToTransparentPngDataUrlAsync(
            HttpClient httpClient,
            string imageUrl,
            Func<string, Task>? onLog,
            CancellationToken cancellationToken)
        {
            try
            {
                byte[] imageBytes;
                if (TryReadImageBytesFromDataUrl(imageUrl, out var decodedDataUrlBytes))
                {
                    imageBytes = decodedDataUrlBytes;
                }
                else
                {
                    if (imageUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        await LogAsync(onLog, "Image data URL could not be decoded for transparency pass.").ConfigureAwait(false);
                        return null;
                    }

                    using var imageResponse = await httpClient.GetAsync(imageUrl, cancellationToken).ConfigureAwait(false);
                    if (!imageResponse.IsSuccessStatusCode)
                    {
                        await LogAsync(onLog, $"Image download failed for transparency pass: {(int)imageResponse.StatusCode}.").ConfigureAwait(false);
                        return null;
                    }

                    imageBytes = await imageResponse.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                }

                if (imageBytes.Length == 0)
                {
                    await LogAsync(onLog, "Image download returned empty bytes for transparency pass.").ConfigureAwait(false);
                    return null;
                }

                using var image = Image.Load<Rgba32>(imageBytes);
                RemoveBorderConnectedBackground(image);

                await using var output = new MemoryStream();
                await image.SaveAsPngAsync(output, new PngEncoder(), cancellationToken).ConfigureAwait(false);
                var base64 = Convert.ToBase64String(output.ToArray());
                return $"data:image/png;base64,{base64}";
            }
            catch (HttpRequestException ex)
            {
                await LogAsync(onLog, $"Transparent PNG conversion failed: {Truncate(ex.Message, 200)}").ConfigureAwait(false);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                await LogAsync(onLog, $"Transparent PNG conversion timed out or was canceled: {Truncate(ex.Message, 200)}").ConfigureAwait(false);
                return null;
            }
            catch (InvalidImageContentException ex)
            {
                await LogAsync(onLog, $"Transparent PNG conversion failed due to image format/content: {Truncate(ex.Message, 200)}").ConfigureAwait(false);
                return null;
            }
            catch (IOException ex)
            {
                await LogAsync(onLog, $"Transparent PNG conversion failed while reading/writing image bytes: {Truncate(ex.Message, 200)}").ConfigureAwait(false);
                return null;
            }
        }

        private static bool TryReadImageBytesFromDataUrl(string imageUrl, out byte[] imageBytes)
        {
            imageBytes = Array.Empty<byte>();
            const string base64Marker = ";base64,";

            if (!imageUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var markerIndex = imageUrl.IndexOf(base64Marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                return false;
            }

            var base64 = imageUrl[(markerIndex + base64Marker.Length)..];
            if (string.IsNullOrWhiteSpace(base64))
            {
                return false;
            }

            try
            {
                imageBytes = Convert.FromBase64String(base64);
                return imageBytes.Length > 0;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static void RemoveBorderConnectedBackground(Image<Rgba32> image)
        {
            var width = image.Width;
            var height = image.Height;
            if (width == 0 || height == 0)
            {
                return;
            }

            var backgroundModel = BuildBackgroundModel(image);

            var visited = new bool[width * height];
            var queue = new Queue<(int X, int Y)>();

            static int IndexOf(int x, int y, int imageWidth) => (y * imageWidth) + x;

            void TryEnqueue(int x, int y)
            {
                var index = IndexOf(x, y, width);
                if (visited[index])
                {
                    return;
                }

                visited[index] = true;
                if (IsBackgroundPixel(image[x, y], backgroundModel))
                {
                    queue.Enqueue((x, y));
                }
            }

            for (var x = 0; x < width; x++)
            {
                TryEnqueue(x, 0);
                TryEnqueue(x, height - 1);
            }

            for (var y = 0; y < height; y++)
            {
                TryEnqueue(0, y);
                TryEnqueue(width - 1, y);
            }

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                var pixel = image[x, y];
                pixel.A = 0;
                image[x, y] = pixel;

                if (x > 0)
                {
                    TryEnqueue(x - 1, y);
                }

                if (x < width - 1)
                {
                    TryEnqueue(x + 1, y);
                }

                if (y > 0)
                {
                    TryEnqueue(x, y - 1);
                }

                if (y < height - 1)
                {
                    TryEnqueue(x, y + 1);
                }
            }

            // Soften edge halos by reducing near-background pixels adjacent to transparency.
            FeatherForegroundEdges(image, backgroundModel);

            // Keep only the main fish silhouette to remove leftover background fragments.
            KeepLargestForegroundComponent(image, backgroundModel);
        }

        private static void FeatherForegroundEdges(Image<Rgba32> image, BackgroundModel backgroundModel)
        {
            var width = image.Width;
            var height = image.Height;

            for (var y = 1; y < height - 1; y++)
            {
                for (var x = 1; x < width - 1; x++)
                {
                    var pixel = image[x, y];
                    if (pixel.A == 0)
                    {
                        continue;
                    }

                    var hasTransparentNeighbor =
                        image[x - 1, y].A == 0 ||
                        image[x + 1, y].A == 0 ||
                        image[x, y - 1].A == 0 ||
                        image[x, y + 1].A == 0;

                    if (!hasTransparentNeighbor)
                    {
                        continue;
                    }

                    var closestDistance = GetClosestBackgroundDistance(pixel, backgroundModel.Samples);
                    if (closestDistance <= backgroundModel.Threshold)
                    {
                        pixel.A = 0;
                        image[x, y] = pixel;
                        continue;
                    }

                    // A slightly wider soft threshold for edge anti-aliasing against the green screen
                    var softThreshold = backgroundModel.Threshold + 20d;
                    if (closestDistance <= softThreshold)
                    {
                        var ratio = (closestDistance - backgroundModel.Threshold) / (softThreshold - backgroundModel.Threshold);
                        var adjustedAlpha = (byte)Math.Clamp((int)Math.Round(ratio * 255d), 0, 255);
                        if (adjustedAlpha < pixel.A)
                        {
                            pixel.A = adjustedAlpha;
                            image[x, y] = pixel;
                        }
                    }
                }
            }
        }

        private static void KeepLargestForegroundComponent(Image<Rgba32> image, BackgroundModel backgroundModel)
        {
            var width = image.Width;
            var height = image.Height;
            var total = width * height;
            if (total == 0)
            {
                return;
            }

            var visited = new bool[total];
            var keepMask = new bool[total];
            var bestComponent = Array.Empty<int>();

            static int IndexOf(int x, int y, int imageWidth) => (y * imageWidth) + x;

            var strictForegroundThreshold = backgroundModel.Threshold + 10d;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var startIndex = IndexOf(x, y, width);
                    if (visited[startIndex])
                    {
                        continue;
                    }

                    var startPixel = image[x, y];
                    if (!IsForegroundSeed(startPixel, backgroundModel.Samples, strictForegroundThreshold))
                    {
                        visited[startIndex] = true;
                        continue;
                    }

                    var queue = new Queue<(int X, int Y)>();
                    var component = new List<int>(capacity: 1024);

                    visited[startIndex] = true;
                    queue.Enqueue((x, y));

                    while (queue.Count > 0)
                    {
                        var (cx, cy) = queue.Dequeue();
                        var cIndex = IndexOf(cx, cy, width);
                        component.Add(cIndex);

                        TryVisit(cx - 1, cy);
                        TryVisit(cx + 1, cy);
                        TryVisit(cx, cy - 1);
                        TryVisit(cx, cy + 1);
                        
                        // 8-way connectivity to prevent severed fins/tails
                        TryVisit(cx - 1, cy - 1);
                        TryVisit(cx + 1, cy - 1);
                        TryVisit(cx - 1, cy + 1);
                        TryVisit(cx + 1, cy + 1);
                    }

                    if (component.Count > bestComponent.Length)
                    {
                        bestComponent = component.ToArray();
                    }

                    void TryVisit(int nx, int ny)
                    {
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        {
                            return;
                        }

                        var nIndex = IndexOf(nx, ny, width);
                        if (visited[nIndex])
                        {
                            return;
                        }

                        visited[nIndex] = true;
                        var pixel = image[nx, ny];
                        if (IsForegroundSeed(pixel, backgroundModel.Samples, strictForegroundThreshold))
                        {
                            queue.Enqueue((nx, ny));
                        }
                    }
                }
            }

            if (bestComponent.Length == 0)
            {
                return;
            }

            foreach (var index in bestComponent)
            {
                keepMask[index] = true;
            }

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = IndexOf(x, y, width);
                    if (keepMask[index])
                    {
                        continue;
                    }

                    var pixel = image[x, y];
                    if (pixel.A == 0)
                    {
                        continue;
                    }

                    pixel.A = 0;
                    image[x, y] = pixel;
                }
            }
        }

        private static BackgroundModel BuildBackgroundModel(Image<Rgba32> image)
        {
            var width = image.Width;
            var height = image.Height;
            var sampleSize = Math.Max(8, Math.Min(width, height) / 8);

            long totalR = 0;
            long totalG = 0;
            long totalB = 0;
            long count = 0;

            var cornerSamples = new List<Rgba32>(capacity: 4);

            Rgba32 SampleBlock(int startX, int startY)
            {
                long blockR = 0;
                long blockG = 0;
                long blockB = 0;
                long blockCount = 0;

                for (var y = startY; y < Math.Min(startY + sampleSize, height); y++)
                {
                    for (var x = startX; x < Math.Min(startX + sampleSize, width); x++)
                    {
                        var pixel = image[x, y];
                        if (pixel.A < 8)
                        {
                            continue;
                        }

                        blockR += pixel.R;
                        blockG += pixel.G;
                        blockB += pixel.B;
                        blockCount++;

                        totalR += pixel.R;
                        totalG += pixel.G;
                        totalB += pixel.B;
                        count++;
                    }
                }

                if (blockCount == 0)
                {
                    return new Rgba32(255, 255, 255, 255);
                }

                return new Rgba32(
                    (byte)(blockR / blockCount),
                    (byte)(blockG / blockCount),
                    (byte)(blockB / blockCount),
                    255);
            }

            cornerSamples.Add(SampleBlock(0, 0));
            cornerSamples.Add(SampleBlock(Math.Max(0, width - sampleSize), 0));
            cornerSamples.Add(SampleBlock(0, Math.Max(0, height - sampleSize)));
            cornerSamples.Add(SampleBlock(Math.Max(0, width - sampleSize), Math.Max(0, height - sampleSize)));

            if (count == 0)
            {
                var fallbackSample = new Rgba32(255, 255, 255, 255);
                return new BackgroundModel(new[] { fallbackSample }, 64d);
            }

            var averageBackground = new Rgba32(
                (byte)(totalR / count),
                (byte)(totalG / count),
                (byte)(totalB / count),
                255);

            var totalDistance = 0d;
            var edgeCount = 0;
            for (var x = 0; x < width; x++)
            {
                totalDistance += ColorDistance(image[x, 0], averageBackground);
                totalDistance += ColorDistance(image[x, height - 1], averageBackground);
                edgeCount += 2;
            }

            for (var y = 1; y < height - 1; y++)
            {
                totalDistance += ColorDistance(image[0, y], averageBackground);
                totalDistance += ColorDistance(image[width - 1, y], averageBackground);
                edgeCount += 2;
            }

            var averageEdgeDistance = edgeCount == 0 ? 0d : totalDistance / edgeCount;
            // The AI often generates slightly noisy or compressed backgrounds, so a very tight threshold stops the floodfill early.
            // Since the background is now a highly contrasting chroma-key green, we can afford a very wide threshold
            // to swallow all the green AI noise without worrying about eating the fish (since fish aren't neon green).
            var threshold = Math.Clamp(averageEdgeDistance + 45d, 55d, 120d);

            cornerSamples.Add(averageBackground);
            return new BackgroundModel(cornerSamples.ToArray(), threshold);
        }

        private static bool IsBackgroundPixel(Rgba32 pixel, BackgroundModel backgroundModel)
        {
            if (pixel.A < 8)
            {
                return true;
            }

            var closestDistance = GetClosestBackgroundDistance(pixel, backgroundModel.Samples);
            return closestDistance <= backgroundModel.Threshold;
        }

        private static double GetClosestBackgroundDistance(Rgba32 pixel, IReadOnlyList<Rgba32> backgroundSamples)
        {
            var closestDistance = double.MaxValue;
            foreach (var sample in backgroundSamples)
            {
                var distance = ColorDistance(pixel, sample);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                }
            }

            return closestDistance;
        }

        private static bool IsForegroundSeed(Rgba32 pixel, IReadOnlyList<Rgba32> backgroundSamples, double threshold)
        {
            if (pixel.A < 24)
            {
                return false;
            }

            return GetClosestBackgroundDistance(pixel, backgroundSamples) > threshold;
        }

        private static double ColorDistance(Rgba32 a, Rgba32 b)
        {
            var dR = a.R - b.R;
            var dG = a.G - b.G;
            var dB = a.B - b.B;
            return Math.Sqrt((dR * dR) + (dG * dG) + (dB * dB));
        }

        private readonly record struct BackgroundModel(Rgba32[] Samples, double Threshold);

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
                   $"The fish must be isolated as a clean cutout on a bright, uniform chroma-key green background (#00FF00), with absolutely no shadows, no gradients, and no backdrop elements.";
        }

        private static string BuildNegativePrompt()
        {
            return "duplicate, clone, twin, reflection, mirrored, stacked, multiple fish, two fish, pair, group of fish, school of fish, sticker sheet, collection, collage, split screen, " +
                   "facing right, angled, diagonal, tilted, swimming up, swimming down, perspective, foreshortening, " +
                   "realistic, 3d, photograph, text, watermark, logo, deformed fish anatomy, " +
                   "complicated shading, gradients, drop shadow, white background, gray background, blue background, magenta background, transparent background, checkerboard, underwater scene, seafloor, plants, bubbles";
        }
    }
}