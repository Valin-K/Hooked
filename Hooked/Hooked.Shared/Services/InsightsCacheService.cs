using System;
using System.Collections.Generic;

namespace Hooked.Shared.Services
{
    /// <summary>
    /// Singleton in-memory cache for Insights page data.
    /// Avoids re-fetching when navigating away and back for the same location.
    /// </summary>
    public sealed class InsightsCacheService
    {
        private readonly Lock _lock = new();
        private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(10);

        private string? _locationKey;
        private DateTime _timestamp;

        public FishingConditionsDto? Conditions { get; private set; }
        public string? Regulations { get; private set; }
        public IReadOnlyList<FishingSpotDto>? Spots { get; private set; }

        public List<(string Role, string Text)> ChatHistory { get; } = [];

        public double? Lat { get; private set; }
        public double? Lng { get; private set; }

        public bool HasConditions(double lat, double lng)
        {
            lock (_lock)
            {
                var key = ToKey(lat, lng);
                return _locationKey == key
                       && Conditions is not null
                       && (DateTime.UtcNow - _timestamp) < Expiry;
            }
        }

        public void SetConditions(double lat, double lng, FishingConditionsDto conditions)
        {
            lock (_lock)
            {
                var key = ToKey(lat, lng);
                if (_locationKey != key)
                {
                    Regulations = null;
                    Spots = null;
                }
                _locationKey = key;
                _timestamp = DateTime.UtcNow;
                Lat = lat;
                Lng = lng;
                Conditions = conditions;
            }
        }

        public void SetRegulations(double lat, double lng, string regulations)
        {
            lock (_lock)
            {
                if (_locationKey == ToKey(lat, lng))
                    Regulations = regulations;
            }
        }

        public void SetSpots(double lat, double lng, IReadOnlyList<FishingSpotDto> spots)
        {
            lock (_lock)
            {
                if (_locationKey == ToKey(lat, lng))
                    Spots = spots;
            }
        }

        public void Invalidate()
        {
            lock (_lock)
            {
                _locationKey = null;
                Conditions = null;
                Regulations = null;
                Spots = null;
            }
        }

        /// <summary>Rounds to ~1 km precision so tiny GPS drift doesn't bust the cache.</summary>
        private static string ToKey(double lat, double lng) =>
            $"{Math.Round(lat, 2):F2},{Math.Round(lng, 2):F2}";
    }
}
