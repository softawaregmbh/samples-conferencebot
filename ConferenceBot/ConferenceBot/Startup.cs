// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Configuration;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConferenceBot
{
    /// <summary>
    /// The Startup class configures services and the request pipeline.
    /// </summary>
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        /// <summary>
        /// Gets the configuration that represents a set of key/value application configuration properties.
        /// </summary>
        /// <value>
        /// The <see cref="IConfiguration"/> that represents a set of key/value application configuration properties.
        /// </value>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> specifies the contract for a collection of service descriptors.</param>
        /// <seealso cref="IStatePropertyAccessor{T}"/>
        /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/web-api/overview/advanced/dependency-injection"/>
        /// <seealso cref="https://docs.microsoft.com/en-us/azure/bot-service/bot-service-manage-channels?view=azure-bot-service-4.0"/>
        public void ConfigureServices(IServiceCollection services)
        {
            var secretKey = Configuration.GetSection("botFileSecret")?.Value;

            // Loads .bot configuration file and adds a singleton that your Bot can access through dependency injection.
            var botConfig = BotConfiguration.Load(@".\ConferenceBot.bot", secretKey);
            services.AddSingleton(sp => botConfig);

            // Retrieve current endpoint.
            var service = botConfig.Services.Where(s => s.Type == "endpoint" && s.Name == "development").FirstOrDefault();
            if (!(service is EndpointService endpointService))
            {
                throw new InvalidOperationException($"The .bot file does not contain a development endpoint.");
            }

            services.AddBot<ConferenceBotBot>(options =>
            {
                options.CredentialProvider = new SimpleCredentialProvider(endpointService.AppId, endpointService.AppPassword);

                // Catches any errors that occur during a conversation turn and logs them.
                options.OnTurnError = async (context, exception) =>
                {
                    await context.SendActivityAsync("Sorry, it looks like something went wrong.");
                };
            });

            // LUIS
            ConfigureLuis(services, botConfig);

            //IStorage dataStore = new MemoryStorage();
            IStorage dataStore = ConfigureCosmosDb(botConfig);

            var conversationState = new ConversationState(dataStore);
            services.AddSingleton(conversationState);
        }

        private static IStorage ConfigureCosmosDb(BotConfiguration botConfig)
        {
            var cosmosDb = botConfig.Services.Where(s => s.Type == ServiceTypes.CosmosDB).FirstOrDefault() as CosmosDbService;
            if (cosmosDb == null)
            {
                throw new InvalidOperationException("The LUIS service is not configured correctly in your '.bot' file.");
            }
            return new CosmosDbStorage(new CosmosDbStorageOptions()
            {
                CosmosDBEndpoint = new Uri(cosmosDb.Endpoint),
                AuthKey = cosmosDb.Key,
                DatabaseId = cosmosDb.Database,
                CollectionId = cosmosDb.Collection,
            });
        }

        private static void ConfigureLuis(IServiceCollection services, BotConfiguration botConfig)
        {
            var luis = botConfig.Services.Where(s => s.Type == ServiceTypes.Luis).FirstOrDefault() as LuisService;
            if (luis == null)
            {
                throw new InvalidOperationException("The LUIS service is not configured correctly in your '.bot' file.");
            }

            var app = new LuisApplication(luis.AppId, luis.AuthoringKey, luis.GetEndpoint());
            var recognizer = new LuisRecognizer(app);
            services.AddSingleton<IRecognizer>(recognizer);
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseDefaultFiles()
                .UseStaticFiles()
                .UseBotFramework();
        }
    }
}
