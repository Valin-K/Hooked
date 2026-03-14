using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Hooked.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hooked.Shared.Services.Search
{
    public sealed class ElasticSearchService : IElasticSearchService
    {
        private const string IndexName = "hooked-catches";

        private readonly ElasticsearchClient _client;
        private readonly HookedDbContext _db;
        private readonly ILogger<ElasticSearchService> _logger;

        public ElasticSearchService(ElasticsearchClient client, HookedDbContext db, ILogger<ElasticSearchService> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task EnsureIndexAsync(CancellationToken cancellationToken = default)
        {
            var exists = await _client.Indices.ExistsAsync(IndexName, cancellationToken).ConfigureAwait(false);
            if (exists.Exists) return;

            // Explicitly map only the geo_point field — everything else is auto-mapped.
            var create = await _client.Indices.CreateAsync(IndexName, c => c
                .Mappings(m => m
                    .Properties<ElasticCatchDocument>(p => p
                        .GeoPoint(f => f.Location)
                    )
                ), cancellationToken).ConfigureAwait(false);

            if (!create.IsValidResponse)
                _logger.LogWarning("Failed to create Elasticsearch index '{Index}': {Error}", IndexName, create.ElasticsearchServerError?.Error?.Reason);
            else
                _logger.LogInformation("Created Elasticsearch index '{Index}'", IndexName);
        }

        public async Task IndexCatchAsync(CatchRecord catchRecord, FishSpecies species, User user, CancellationToken cancellationToken = default)
        {
            await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

            var doc = ToDocument(catchRecord, species, user);
            var response = await _client.IndexAsync(doc, i => i
                .Index(IndexName)
                .Id(doc.CatchId.ToString()),
                cancellationToken).ConfigureAwait(false);

            if (!response.IsValidResponse)
                _logger.LogWarning("Failed to index catch {CatchId}: {Error}", doc.CatchId, response.ElasticsearchServerError?.Error?.Reason);
        }

        public async Task<ElasticSearchResultDto> SearchCatchesAsync(
            string? query,
            double? lat = null,
            double? lon = null,
            double? radiusKm = null,
            int limit = 25,
            CancellationToken cancellationToken = default)
        {
            var response = await _client.SearchAsync<ElasticCatchDocument>(s =>
            {
                s.Indices(IndexName).Size(Math.Clamp(limit, 1, 100));

                var hasText = !string.IsNullOrWhiteSpace(query);
                var hasGeo = lat.HasValue && lon.HasValue;

                if (!hasText && !hasGeo)
                {
                    s.Query(q => q.MatchAll(m => { }));
                }
                else
                {
                    s.Query(q => q.Bool(b =>
                    {
                        if (hasText)
                        {
                            b.Must(m => m.MultiMatch(mm => mm
                                .Query(query!)
                                .Fields(new Field[] { "speciesCommonName", "speciesScientificName", "conservationStatus", "username", "displayName" })
                                .Fuzziness(new Fuzziness("AUTO"))
                            ));
                        }

                        if (hasGeo)
                        {
                            var radius = $"{radiusKm ?? 50}km";
                            b.Filter(f => f.GeoDistance(g => g
                                .Field(new Field("location"))
                                .Location(new LatLonGeoLocation { Lat = lat!.Value, Lon = lon!.Value })
                                .Distance(radius)
                            ));
                        }
                    }));
                }

                s.Sort(so => so.Field(
                    new Field("caughtAt"),
                    fso => fso.Order(SortOrder.Desc)
                ));
            }, cancellationToken).ConfigureAwait(false);

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch search failed: {Error}", response.ElasticsearchServerError?.Error?.Reason);
                return new ElasticSearchResultDto { TotalHits = 0, Hits = [] };
            }

            return new ElasticSearchResultDto
            {
                TotalHits = response.Total,
                Hits = response.Documents.ToList()
            };
        }

        public async Task BulkReindexAsync(IEnumerable<CatchRecord> catches, CancellationToken cancellationToken = default)
        {
            await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);

            var catchList = catches.ToList();
            if (catchList.Count == 0) return;

            var speciesIds = catchList.Select(c => c.SpeciesId).Distinct().ToList();
            var userIds = catchList.Select(c => c.UserId).Distinct().ToList();

            var species = await _db.FishSpecies.AsNoTracking()
                .Where(s => speciesIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, cancellationToken).ConfigureAwait(false);

            var users = await _db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, cancellationToken).ConfigureAwait(false);

            var documents = catchList
                .Where(c => species.ContainsKey(c.SpeciesId) && users.ContainsKey(c.UserId))
                .Select(c => ToDocument(c, species[c.SpeciesId], users[c.UserId]))
                .ToList();

            if (documents.Count == 0) return;

            var bulkResponse = await _client.BulkAsync(b => b
                .Index(IndexName)
                .IndexMany(documents, (op, doc) => op.Id(doc.CatchId.ToString()))
            , cancellationToken).ConfigureAwait(false);

            if (bulkResponse.Errors)
                _logger.LogWarning("Bulk reindex had errors: {Count} item(s) failed", bulkResponse.ItemsWithErrors.Count());
            else
                _logger.LogInformation("Bulk reindex complete: {Count} catches indexed", documents.Count);
        }

        private static ElasticCatchDocument ToDocument(CatchRecord c, FishSpecies species, User user)
        {
            CatchGeoLocation? location = null;
            if (c.LocationJson is not null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(c.LocationJson);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("lat", out var latEl) && root.TryGetProperty("lng", out var lngEl))
                        location = new CatchGeoLocation { Lat = latEl.GetDouble(), Lon = lngEl.GetDouble() };
                    else if (root.TryGetProperty("lat", out var lat2) && root.TryGetProperty("lon", out var lon2))
                        location = new CatchGeoLocation { Lat = lat2.GetDouble(), Lon = lon2.GetDouble() };
                }
                catch { /* ignore malformed JSON */ }
            }

            return new ElasticCatchDocument
            {
                CatchId = c.Id,
                UserId = c.UserId,
                Username = user.Username,
                DisplayName = user.DisplayName,
                SpeciesId = species.Id,
                SpeciesCommonName = species.CommonName,
                SpeciesScientificName = species.ScientificName,
                ConservationStatus = species.ConservationStatus,
                IsEndangered = species.IsEndangered,
                IsInvasive = species.IsInvasive,
                CaughtAt = c.CaughtAt,
                LengthMeters = c.LengthMeters,
                WeightKg = c.WeightKg,
                PhotoPath = c.PhotoPath,
                Location = location
            };
        }
    }
}
