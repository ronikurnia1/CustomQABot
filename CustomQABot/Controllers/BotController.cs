// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace CustomQABot.Controllers;

// This ASP Controller is created to handle a request. Dependency Injection will provide the Adapter and IBot
// implementation at runtime. Multiple different IBot implementations running at different endpoints can be
// achieved by specifying a more specific type for the bot constructor argument.
[Route("api/messages")]
[ApiController]

public class BotController : ControllerBase
{
    private readonly IBotFrameworkHttpAdapter adapter;
    private readonly IBot bot;
    private readonly ILogger<BotController> logger;

    public BotController(IBotFrameworkHttpAdapter adapter, IBot bot, ILogger<BotController> logger)
    {
        this.adapter = adapter;
        this.bot = bot;
        this.logger = logger;
    }

    [HttpPost, HttpGet]
    public async Task PostAsync()
    {
        //var body = string.Empty;
        //using(var streamReader = new StreamReader(Request.Body))
        //{
        //    body = await streamReader.ReadToEndAsync();
        //    logger.LogInformation($"Receiving a request: {body}");
        //}

        //using var injectedRequestStream = new MemoryStream();
        //var bytesToWrite = System.Text.Encoding.UTF8.GetBytes(body);
        //injectedRequestStream.Write(bytesToWrite, 0, bytesToWrite.Length);
        //injectedRequestStream.Seek(0, SeekOrigin.Begin);
        //Request.Body = injectedRequestStream;

        // Delegate the processing of the HTTP POST to the adapter.
        // The adapter will invoke the bot.
        await adapter.ProcessAsync(Request, Response, bot);
    }
}
