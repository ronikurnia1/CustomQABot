// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CustomQABot.Bots;
using CustomQABot.Dialogs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Azure.Blobs;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Declarative;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


namespace CustomQABot;

public class Startup
{

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient().AddControllers().AddNewtonsoftJson(options =>
        {
            options.SerializerSettings.MaxDepth = HttpHelper.BotMessageSerializerSettings.MaxDepth;
        });

        // Create the Bot Framework Adapter with error handling enabled.
        services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

        // Create the storage we'll be using for User and Conversation state. (Memory is great for testing purposes.)
        var storage = new BlobsStorage(Configuration["AzureBlobStorageConnectionString"], "bot-state");
        //services.AddSingleton<IStorage, MemoryStorage>();

        // Create the User state. (Used in this bot's Dialog implementation.)
        var userState = new UserState(storage);
        services.AddSingleton(userState);

        // Create the Conversation state. (Used by the Dialog system itself.)
        var conversationState = new ConversationState(storage);
        services.AddSingleton(conversationState);

        // The Dialog that will be run by the bot.
        services.AddSingleton<RootDialog>();

        // Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
        services.AddTransient<IBot, CustomQABot<RootDialog>>();

        new DialogsBotComponent().ConfigureServices(services, Configuration);
        ComponentRegistration.Add(new DialogsComponentRegistration());
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseDefaultFiles()
            .UseStaticFiles()
            .UseWebSockets()
            .UseRouting()
            .UseAuthorization()
            .UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

        // app.UseHttpsRedirection();
    }
}
