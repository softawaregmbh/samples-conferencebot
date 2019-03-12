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

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            var secretKey = Configuration.GetSection("botFileSecret")?.Value;

            // Loads .bot configuration file
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
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseDefaultFiles()
                .UseStaticFiles()
                .UseBotFramework();
        }

        private static IStorage ConfigureCosmosDb()
        {
            return new CosmosDbStorage(new CosmosDbStorageOptions()
            {
                DatabaseId = "bot",
                CollectionId = "conversationstate",
                AuthKey = "IazeHmfQPNDW1giK4siIQaherFz09FnV9eM02mLzg65YCFD8QXhvE8Ya0baFag8LAT6lsg8mnaCeuinAFS0YjQ==",
                CosmosDBEndpoint = new Uri("https://bot4demostorage.documents.azure.com:443/")
            });
        }

        private static void ConfigureLuis(IServiceCollection services, BotConfiguration botConfig)
        {
            var luis = botConfig.Services.Where(s => s.Type == "luis").FirstOrDefault() as LuisService;
            if (luis == null)
            {
                throw new InvalidOperationException("The LUIS service is not configured correctly in your '.bot' file.");
            }

            var app = new LuisApplication(luis.AppId, luis.AuthoringKey, luis.GetEndpoint());
            var recognizer = new LuisRecognizer(app);
            services.AddSingleton<IRecognizer>(recognizer);
        }
    }
}
