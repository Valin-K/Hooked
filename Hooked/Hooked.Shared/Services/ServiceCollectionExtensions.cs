using System;
using Hooked.Shared.Services.AI;
using Hooked.Shared.Services.Search;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hooked.Shared.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHookedServices(this IServiceCollection services, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IFishService, FishService>();
            services.AddScoped<ICatchService, CatchService>();
            services.AddScoped<IAchievementService, AchievementService>();
            services.AddScoped<IFishDexService, FishDexService>();
            services.AddScoped<ISocialService, SocialService>();
            services.AddScoped<IFishingQuestService, FishingQuestService>();
            services.AddScoped<ILeaderboardService, LeaderboardService>();
            services.AddScoped<IMapService, MapService>();
            services.AddScoped<ISessionService, SessionService>();
            services.Configure<ProgressionOptions>(configuration.GetSection(ProgressionOptions.SectionName));
            services.AddScoped<IXpNotificationService, XpNotificationService>();
            services.AddScoped<IProgressionService, ProgressionService>();
            services.AddScoped<INotificationService, NotificationService>();

            services.AddSingleton<InsightsCacheService>();

            services.AddScoped<IInsightsService>(sp =>
                new InsightsService(
                    new System.Net.Http.HttpClient(),
                    configuration["Gemini:ApiKey"]));

            services.AddSingleton<IGeminiFishSpeciesService>(_ =>
                new GeminiFishSpeciesService(configuration["Gemini:ApiKey"]));

            services.AddSingleton<ILeonardoFishImageService>(sp =>
                new LeonardoFishImageService(
                    configuration["LeonardoAI:ApiKey"],
                    configuration["ReferenceImageId"],
                    sp.GetRequiredService<IGeminiFishSpeciesService>()));

            // Elasticsearch — optional: skipped when URL or API key is not configured
            var elasticUrl = configuration["Elasticsearch:Url"];
            var elasticApiKey = configuration["Elasticsearch:ApiKey"];
            if (!string.IsNullOrWhiteSpace(elasticUrl) && !string.IsNullOrWhiteSpace(elasticApiKey))
            {
                services.AddSingleton<ElasticsearchClient>(_ =>
                {
                    var settings = new ElasticsearchClientSettings(new Uri(elasticUrl))
                        .Authentication(new ApiKey(elasticApiKey));
                    return new ElasticsearchClient(settings);
                });

                services.AddScoped<IElasticSearchService, ElasticSearchService>();
            }

            return services;
        }
    }
}
