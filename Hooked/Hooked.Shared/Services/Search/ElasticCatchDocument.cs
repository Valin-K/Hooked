using System;

namespace Hooked.Shared.Services.Search
{
    /// <summary>
    /// Elasticsearch document representing a fish catch.
    /// Indexed whenever a catch is logged; supports full-text and geo search.
    /// The <see cref="Location"/> object is serialized as {"lat":x,"lon":y} which
    /// Elasticsearch recognises as a valid geo_point value.
    /// </summary>
    public sealed class ElasticCatchDocument
    {
        public Guid CatchId { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; } = null!;
        public string? DisplayName { get; set; }

        public int SpeciesId { get; set; }
        public string SpeciesCommonName { get; set; } = null!;
        public string? SpeciesScientificName { get; set; }
        public string? ConservationStatus { get; set; }
        public bool IsEndangered { get; set; }
        public bool IsInvasive { get; set; }

        public DateTime CaughtAt { get; set; }
        public double? LengthMeters { get; set; }
        public double? WeightKg { get; set; }
        public string? PhotoPath { get; set; }

        public CatchGeoLocation? Location { get; set; }
    }

    /// <summary>Lat/lon pair serialised to {"lat":y,"lon":x} for Elasticsearch geo_point.</summary>
    public sealed class CatchGeoLocation
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }
}
