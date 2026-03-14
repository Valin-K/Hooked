using System;
using System.Collections.Generic;

namespace Hooked.Shared.Services.Search
{
    public sealed class ElasticSearchResultDto
    {
        public long TotalHits { get; set; }
        public IReadOnlyList<ElasticCatchDocument> Hits { get; set; } = [];
    }
}
