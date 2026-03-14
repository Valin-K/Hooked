using Hooked.Shared.Services.AI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;

namespace Hooked.Shared.Services
{
    public sealed class InsightsService : IInsightsService
    {
        private const string ModelName = "gemini-2.5-flash";
        private readonly HttpClient _http;
        private readonly Client? _gemini;

        public InsightsService(HttpClient httpClient, string? geminiApiKey)
        {
            _http = httpClient;
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "HookedApp/1.0");
            if (!string.IsNullOrWhiteSpace(geminiApiKey))
                _gemini = new Client(apiKey: geminiApiKey);
        }

        public async Task<FishingConditionsDto> GetConditionsAsync(double lat, double lng, CancellationToken cancellationToken = default)
        {
            var latS = lat.ToString("F4", CultureInfo.InvariantCulture);
            var lngS = lng.ToString("F4", CultureInfo.InvariantCulture);

            var weatherUrl = $"https://api.open-meteo.com/v1/forecast?latitude={latS}&longitude={lngS}" +
                             "&current=temperature_2m,apparent_temperature,wind_speed_10m,wind_direction_10m,weather_code" +
                             "&wind_speed_unit=kmh&timezone=auto";

            var marineUrl = $"https://marine-api.open-meteo.com/v1/marine?latitude={latS}&longitude={lngS}" +
                            "&current=wave_height,wave_period&timezone=auto";

            var geoUrl = $"https://nominatim.openstreetmap.org/reverse?lat={latS}&lon={lngS}&format=json";

            double tempC = 0, feelsC = 0, windKmh = 0, windDir = 0;
            int weatherCode = 0;
            double waveH = 0, waveP = 0;
            string locationLabel = $"{lat:F2}°, {lng:F2}°";

            try
            {
                using var weatherDoc = await _http.GetFromJsonAsync<JsonDocument>(weatherUrl, cancellationToken).ConfigureAwait(false);
                if (weatherDoc is not null)
                {
                    var cur = weatherDoc.RootElement.GetProperty("current");
                    tempC = cur.GetProperty("temperature_2m").GetDouble();
                    feelsC = cur.GetProperty("apparent_temperature").GetDouble();
                    windKmh = cur.GetProperty("wind_speed_10m").GetDouble();
                    windDir = cur.GetProperty("wind_direction_10m").GetDouble();
                    weatherCode = cur.GetProperty("weather_code").GetInt32();
                }
            }
            catch { }

            try
            {
                using var marineDoc = await _http.GetFromJsonAsync<JsonDocument>(marineUrl, cancellationToken).ConfigureAwait(false);
                if (marineDoc is not null)
                {
                    var cur = marineDoc.RootElement.GetProperty("current");
                    waveH = cur.GetProperty("wave_height").GetDouble();
                    waveP = cur.GetProperty("wave_period").GetDouble();
                }
            }
            catch { }

            try
            {
                using var geoDoc = await _http.GetFromJsonAsync<JsonDocument>(geoUrl, cancellationToken).ConfigureAwait(false);
                if (geoDoc is not null)
                {
                    var addr = geoDoc.RootElement.GetProperty("address");
                    var suburb = addr.TryGetProperty("suburb", out var s) ? s.GetString() :
                                 addr.TryGetProperty("town", out var t) ? t.GetString() :
                                 addr.TryGetProperty("city", out var c) ? c.GetString() : null;
                    var state = addr.TryGetProperty("state", out var st) ? st.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(suburb) && !string.IsNullOrWhiteSpace(state))
                        locationLabel = $"{suburb}, {state}";
                    else if (!string.IsNullOrWhiteSpace(suburb))
                        locationLabel = suburb!;
                }
            }
            catch { }

            var windDirLabel = WindDirectionLabel(windDir);
            var weatherDesc = WmoDescription(weatherCode);
            var (quality, qualityReason) = EvaluateQuality(windKmh, waveH, weatherCode);
            var (tidePhase, tideLabel) = ApproximateTidePhase();
            var (moonPhase, moonLabel) = CalculateMoonPhase(DateTime.UtcNow);
            var bestTime = BestTimeWindow(quality);
            var strategy = StrategyTip(windKmh, waveH);
            var technique = TechniqueTip(lat, tempC);
            var regulation = "Always check local bag limits, size limits, and seasonal closures before heading out.";

            return new FishingConditionsDto(
                locationLabel, lat, lng,
                tempC, feelsC, windKmh, windDir, windDirLabel,
                waveH, waveP, weatherCode, weatherDesc,
                quality, qualityReason,
                tidePhase, tideLabel,
                moonPhase, moonLabel,
                bestTime, strategy, technique, regulation);
        }

        public async Task<string> GeocodeLocationAsync(string query, CancellationToken cancellationToken = default)
        {
            // Returns "lat,lng" or empty string on failure
            var encoded = Uri.EscapeDataString(query);
            var url = $"https://nominatim.openstreetmap.org/search?q={encoded}&format=json&limit=1";
            try
            {
                using var doc = await _http.GetFromJsonAsync<JsonDocument>(url, cancellationToken).ConfigureAwait(false);
                if (doc is not null)
                {
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    {
                        var first = root[0];
                        var lat = first.GetProperty("lat").GetString();
                        var lng = first.GetProperty("lon").GetString();
                        return $"{lat},{lng}";
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        public async Task<IReadOnlyList<FishingSpotDto>> GetNearbyFishingSpotsAsync(double lat, double lng, CancellationToken cancellationToken = default)
        {
            if (_gemini is null)
                return [];

            var latS = lat.ToString("F4", CultureInfo.InvariantCulture);
            var lngS = lng.ToString("F4", CultureInfo.InvariantCulture);

            var prompt = $"""
                You are a local fishing guide expert.
                List exactly 5 prime fishing locations within ~50 km of coordinates {latS}°N, {lngS}°E.
                For each location respond with ONLY this exact pipe-delimited format on one line, no extra text:
                NAME|TYPE|DISTANCE|TARGET_SPECIES|TIP
                Where TYPE is one of: River, Lake, Bay, Estuary, Reef, Beach, Dam, Creek
                DISTANCE is approximate e.g. "8 km NW"
                TARGET_SPECIES is 2-3 fish species comma separated
                TIP is one short practical sentence (max 12 words)
                Output exactly 5 lines, nothing else.
                """;

            try
            {
                var response = await _gemini.Models.GenerateContentAsync(
                    model: ModelName,
                    contents: new List<Content>
                    {
                        new() { Parts = new List<Part> { new() { Text = prompt } } }
                    }).ConfigureAwait(false);

                var text = response?.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim() ?? string.Empty;
                var spots = new List<FishingSpotDto>();

                foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Trim().Split('|');
                    if (parts.Length >= 5)
                    {
                        spots.Add(new FishingSpotDto(
                            parts[0].Trim(),
                            parts[1].Trim(),
                            parts[2].Trim(),
                            parts[3].Trim(),
                            parts[4].Trim()));
                    }
                }
                return spots;
            }
            catch
            {
                return [];
            }
        }

        public async Task<string> GetLocalRegulationsAsync(double lat, double lng, CancellationToken cancellationToken = default)
        {
            if (_gemini is null)
                return "Check your local fisheries authority website for bag limits, size limits, and seasonal closures.";

            var latS = lat.ToString("F4", CultureInfo.InvariantCulture);
            var lngS = lng.ToString("F4", CultureInfo.InvariantCulture);

            var prompt = $"""
                You are an expert on fishing regulations.
                For the location at coordinates {latS}°, {lngS}°, provide a brief practical summary of key recreational fishing regulations for that region/state/country.
                Cover: common bag limits for popular species, minimum size rules, licensing requirements, and any notable protected areas or seasonal restrictions.
                Use bullet points. Keep it under 120 words. Remind the user to verify with the official local fisheries authority.
                Do not fabricate specific numbers if unsure — say "check locally" instead.
                """;

            try
            {
                var response = await _gemini.Models.GenerateContentAsync(
                    model: ModelName,
                    contents: new List<Content>
                    {
                        new() { Parts = new List<Part> { new() { Text = prompt } } }
                    }).ConfigureAwait(false);

                return response?.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim()
                       ?? "Check your local fisheries authority for regulations.";
            }
            catch
            {
                return "Check your local fisheries authority for regulations.";
            }
        }

        public async Task<string> AskAssistantAsync(string question, double? lat, double? lng, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(question))
                return string.Empty;

            if (_gemini is null)
                return "Gemini API key is not configured. Add `Gemini:ApiKey` to appsettings.json.";

            var locationContext = (lat.HasValue && lng.HasValue)
                ? $"The user is located at approximately {lat.Value:F2}°, {lng.Value:F2}° (lat/lng)."
                : "The user's location is not available.";

            var systemPrompt = $"""
                You are Tide, an expert AI fishing assistant for the Hooked app.
                You help anglers with fishing strategies, species identification, regulations, tide and weather interpretation, and bait/lure selection.
                {locationContext}
                Today is {DateTime.Now:dddd, d MMMM yyyy}.
                Keep responses concise and practical. Use Markdown for formatting where it aids readability (bold key points, bullet lists for tips).
                Do not make up specific regulations — remind users to check their local authority.
                """;

            try
            {
                var response = await _gemini.Models.GenerateContentAsync(
                    model: ModelName,
                    contents: new List<Content>
                    {
                        new() { Parts = new List<Part> { new() { Text = $"{systemPrompt}\n\nUser question: {question}" } } }
                    }).ConfigureAwait(false);

                return response?.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim()
                       ?? "Sorry, I couldn't generate a response. Please try again.";
            }
            catch (Exception ex)
            {
                return $"Assistant error: {ex.Message}";
            }
        }

        // ?? Helpers ??????????????????????????????????????????????????????????

        private static string WindDirectionLabel(double deg) => (deg % 360) switch
        {
            >= 337.5 or < 22.5 => "N",
            >= 22.5 and < 67.5 => "NE",
            >= 67.5 and < 112.5 => "E",
            >= 112.5 and < 157.5 => "SE",
            >= 157.5 and < 202.5 => "S",
            >= 202.5 and < 247.5 => "SW",
            >= 247.5 and < 292.5 => "W",
            _ => "NW"
        };

        private static string WmoDescription(int code) => code switch
        {
            0 => "Clear sky",
            1 => "Mainly clear",
            2 => "Partly cloudy",
            3 => "Overcast",
            45 or 48 => "Foggy",
            51 or 53 or 55 => "Drizzle",
            61 or 63 or 65 => "Rain",
            71 or 73 or 75 => "Snow",
            80 or 81 or 82 => "Rain showers",
            95 => "Thunderstorm",
            96 or 99 => "Thunderstorm with hail",
            _ => "Unknown"
        };

        // Returns WMO code bucket 0–6 for SVG icon selection in the UI
        public static int WmoIconBucket(int code) => code switch
        {
            0 => 0,          // clear
            1 => 1,          // mainly clear
            2 => 2,          // partly cloudy
            3 => 3,          // overcast
            45 or 48 => 4,   // fog
            51 or 53 or 55 or 61 or 63 or 65 or 80 or 81 or 82 => 5, // rain
            95 or 96 or 99 => 6, // storm
            _ => 3
        };

        private static (FishingQuality quality, string reason) EvaluateQuality(double windKmh, double waveH, int weatherCode)
        {
            var isStorm = weatherCode is 95 or 96 or 99;
            var isRain = weatherCode is >= 61 and <= 82;

            if (isStorm || windKmh > 45 || waveH > 2.5)
                return (FishingQuality.Poor, "Dangerous conditions — high winds or swell. Stay safe.");

            if (windKmh > 25 || waveH > 1.5 || isRain)
                return (FishingQuality.Fair, "Challenging conditions. Seek sheltered water.");

            if (windKmh < 15 && waveH < 0.6)
                return (FishingQuality.Excellent, "Calm, ideal conditions. Fish are active.");

            return (FishingQuality.Good, "Good conditions — light wind and manageable swell.");
        }

        private static (string phase, string label) ApproximateTidePhase()
        {
            var hour = DateTime.Now.Hour;
            return hour switch
            {
                >= 5 and < 8   => ("rising", "Rising tide"),
                >= 8 and < 11  => ("high",   "High tide"),
                >= 11 and < 14 => ("falling","Falling tide"),
                >= 14 and < 17 => ("low",    "Low tide"),
                >= 17 and < 20 => ("rising", "Rising tide"),
                >= 20 and < 23 => ("high",   "High tide"),
                _              => ("falling","Falling tide")
            };
        }

        /// <summary>Returns moon phase name and 0-7 bucket using synodic period from known new moon epoch.</summary>
        public static (string phase, string label) CalculateMoonPhase(DateTime utcNow)
        {
            // Known new moon: 6 Jan 2000 18:14 UTC
            var knownNewMoon = new DateTime(2000, 1, 6, 18, 14, 0, DateTimeKind.Utc);
            const double synodicDays = 29.53058867;
            var daysSince = (utcNow - knownNewMoon).TotalDays;
            var cyclePos = ((daysSince % synodicDays) + synodicDays) % synodicDays;
            var bucket = (int)Math.Round(cyclePos / synodicDays * 8) % 8;

            return bucket switch
            {
                0 => ("new",           "New Moon"),
                1 => ("waxing-crescent","Waxing Crescent"),
                2 => ("first-quarter", "First Quarter"),
                3 => ("waxing-gibbous","Waxing Gibbous"),
                4 => ("full",          "Full Moon"),
                5 => ("waning-gibbous","Waning Gibbous"),
                6 => ("last-quarter",  "Last Quarter"),
                _ => ("waning-crescent","Waning Crescent")
            };
        }

        private static string BestTimeWindow(FishingQuality quality) => quality switch
        {
            FishingQuality.Excellent => "Dawn (5–8 AM) and dusk (6–8 PM) — peak feeding windows today.",
            FishingQuality.Good      => "Early morning and late afternoon offer the best bite.",
            FishingQuality.Fair      => "Target the tide change periods for the best chance.",
            _                        => "Wait for conditions to improve — safety first."
        };

        private static string StrategyTip(double windKmh, double waveH)
        {
            if (windKmh > 25 || waveH > 1.5)
                return "Fish sheltered bays, estuaries, or the lee side of headlands to escape the chop. Position with wind at your back for distance and presentation.";
            return "Target structure — rocky ledges, drop-offs, and weed beds at dawn or dusk. Minimal drift lets you work a spot thoroughly; use a sea anchor if needed.";
        }

        private static string TechniqueTip(double lat, double tempC)
        {
            var isTropical = Math.Abs(lat) < 23.5;
            if (isTropical || tempC > 24)
                return "Warm water activates surface feeders. Try surface poppers or stickbaits early morning. Match bait to local baitfish — pilchards or mullet are reliable berley.";
            if (tempC < 14)
                return "Cold water slows metabolism — slow-roll soft plastics near the bottom. Berley with finely chopped pilchard to concentrate fish in a smaller area.";
            return "Moderate temperatures suit a mix of techniques. Jig soft plastics around structure, or float-fish live bait over deeper holes. Use berley to draw fish up in the water column.";
        }
    }
}
