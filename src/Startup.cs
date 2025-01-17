using System;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Bot.Clockify;
using Bot.Clockify.Client;
using Bot.Clockify.Fill;
using Bot.Clockify.Reports;
using Bot.Clockify.User;
using Bot.Common;
using Bot.Common.Recognizer;
using Bot.Data;
using Bot.DIC;
using Bot.Remind;
using Bot.Security;
using Bot.States;
using Bot.Supports;
using F23.StringSimilarity;
using F23.StringSimilarity.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bot
{
    public class Startup
    {
        private readonly string _dataConnectionString;
        private readonly string _containerName;
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
            _dataConnectionString = configuration.GetSection("BlobStorage")["DataConnectionString"];
            _containerName = configuration.GetSection("BlobStorage")["ContainerName"];
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddNewtonsoftJson();
            services.AddHttpClient().AddControllers().AddNewtonsoftJson();

            // Configure app insight if the key exists. New Azure regions require the use of connection strings
            // instead of instrumentation keys.
            string? appInsightConnectionString = _configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
            if (!string.IsNullOrWhiteSpace(appInsightConnectionString))
            {
                services.AddApplicationInsightsTelemetry(appInsightConnectionString);
            }

            ConfigureLocalization(services);

            var clockifyService = new ClockifyService(new ClockifyClientFactory());

            // Create the Bot Framework Authentication to be used with the Bot Adapter.
            services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

            services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
            services.AddSingleton<TextToDateRangeService>();
            services.AddSingleton<ReportDialog>();
            services.AddSingleton<IReportSummaryService, ReportSummaryService>();
            services.AddSingleton<IReportExtractor, ReportExtractor>();
            services.AddSingleton<EntryFillDialog>();
            services.AddSingleton<UserSettingsDialog>();
            services.AddSingleton<LogoutDialog>();
            services.AddSingleton<StopReminderDialog>();
            services.AddSingleton<WorthAskingForTaskService>();
            services.AddSingleton<ITimeEntryStoreService, TimeEntryStoreService>();
            services.AddSingleton<IStringDistance>(new ClockifyEntityDistance(new MetricLCS()));
            services.AddSingleton<ClockifyEntityRecognizer, ClockifyEntityRecognizer>();
            services.AddSingleton<UserProfilesProvider>();
            services.AddSingleton<IClockifyService>(clockifyService);

            services.AddSingleton<NotifyUsersDialog, NotifyUsersDialog>();

            // DIC
            services.AddSingleton<DicSetupDialog, DicSetupDialog>();
            services.AddSingleton<IDipendentiInCloudClient, DipendentiInCloudClient>();
            services.AddSingleton<IDipendentiInCloudService, DipendentiInCloudService>();

            services.AddSingleton<INeedRemindService, EndOfWorkingDay>();
            services.AddSingleton<INeedRemindService, TimeSheetNotFullEnough>();
            services.AddSingleton<INeedRemindService, UserDidNotSayStop>();
            services.AddSingleton<INeedRemindService, NotOnLeave>();
            services.AddSingleton<INeedRemindService, PastDayNotComplete>();
            services.AddSingleton<ICompositeNeedReminderService, CompositeNeedReminderService>();
            services.AddSingleton<ISpecificRemindService, EntryFillRemindService>();
            services.AddSingleton<ISpecificRemindServiceResolver, SpecificRemindServiceResolver>();
            services.AddSingleton<IRemindService, SmartWorkingRemindService>();
            services.AddSingleton<IRemindServiceResolver, RemindServiceResolver>();
            services.AddSingleton<IFollowUpService, FollowUpService>();
            services.AddSingleton<NextWeekRemoteWorkingDialog, NextWeekRemoteWorkingDialog>();
            services.AddSingleton<LongTermRemoteWorkingDialog, LongTermRemoteWorkingDialog>();
            services.AddSingleton<TeamAvailabilityService, TeamAvailabilityService>();

            services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();
            services.AddSingleton<ClockifySetupDialog, ClockifySetupDialog>();

            // TODO use memory storage only for Development
            IStorage storage = new MemoryStorage();
            if (_dataConnectionString != null && _containerName != null)
            {
                storage = new AzureBlobStorage(_dataConnectionString, _containerName);
            }

            if (!int.TryParse(_configuration["TokenCacheSeconds"], out var cacheSeconds))
            {
                cacheSeconds = 30;
            }

            ConfigureAzureKeyVault(services, _configuration["KeyVaultName"], cacheSeconds);

            // var userState = new UserState(storage);
            // var conversationState = new ConversationState(storage);

            services.AddSingleton(storage);
            // services.AddSingleton(conversationState);
            // services.AddSingleton(userState);
            //
            // services.AddSingleton(userState.CreateProperty<UserProfile>("UserProfile"));

            services.AddSingleton<UserState>();
            services.AddSingleton<ConversationState>();
            
            services.AddSingleton<IBot, Supports.Bot>();
            services.AddSingleton<IAzureBlobReader, AzureBlobReader>();
            services.AddSingleton<IUserProfileStorageReader, UserProfileStorageReader>();
            services.AddSingleton<IUserProfilesProvider, UserProfilesProvider>();

            ConfigureRecognizer(services, _configuration["LuisAppId"], _configuration["LuisAPIKey"],
                _configuration["LuisAPIHostName"]);

            // Bot supports
            services.AddSingleton<IBotHandler, UtilityHandler>();
            services.AddSingleton<IBotHandler, DicHandler>();
            services.AddSingleton<IBotHandler, ClockifyHandler>();
            services.AddSingleton<BotHandlerChain>();

            // Security
            services.AddSingleton<IProactiveApiKeyProvider, ProactiveApiKeyProvider>();
            services.AddSingleton<IProactiveBotApiKeyValidator, ProactiveBotApiKeyValidator>();
        }

        private static void ConfigureLocalization(IServiceCollection services)
        {
            services.AddLocalization(o => { o.ResourcesPath = "Common/Resources"; });
            services.AddSingleton<IClockifyMessageSource, ClockifyMessageSource>();
            services.AddSingleton<IDicMessageSource, DicMessageSource>();
            services.AddSingleton<ICommonMessageSource, CommonMessageSource>();
        }

        private static void ConfigureAzureKeyVault(IServiceCollection services, string keyVaultName, int cacheSeconds)
        {
            if (string.IsNullOrEmpty(keyVaultName))
            {
                services.AddSingleton<ITokenRepository, InMemoryTokenRepository>();
                return;
            }

            var options = new SecretClientOptions()
            {
                Retry =
                {
                    Delay = TimeSpan.FromSeconds(2),
                    MaxDelay = TimeSpan.FromSeconds(16),
                    MaxRetries = 5,
                    Mode = RetryMode.Exponential
                }
            };
            var secretClient = new SecretClient(new Uri($"https://{keyVaultName}.vault.azure.net/"),
                new DefaultAzureCredential(), options);
            services.AddSingleton(secretClient);
            services.AddMemoryCache();
            services.AddSingleton<ITokenRepository>(
                sp => new TokenRepository(secretClient, sp.GetRequiredService<IMemoryCache>(), cacheSeconds));
        }

        private static void ConfigureRecognizer(IServiceCollection services, string? luisAppId, string? luisApiKey,
            string? luisApiHostName)
        {
            bool luisIsConfigured = !string.IsNullOrEmpty(luisAppId) &&
                                    !string.IsNullOrEmpty(luisApiKey) &&
                                    !string.IsNullOrEmpty(luisApiHostName);
            if (!luisIsConfigured)
            {
                services.AddSingleton<IRecognizer, InMemoryCommonRecognizer>();
            }
            else
            {
                services.AddSingleton<IRecognizer>(
                    sp => new CommonRecognizer(luisAppId!, luisApiKey!, luisApiHostName!));
            }
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

            app.UseDefaultFiles()
                .UseStaticFiles()
                .UseWebSockets()
                .UseRouting()
                .UseAuthorization()
                .UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}