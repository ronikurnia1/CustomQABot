using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static CustomQABot.Cards.CardBuilder;

namespace CustomQABot.Services;

public class TeamsEscalationService : IEscalationService
{
    private readonly ILogger<TeamsEscalationService> logger;
    private readonly HttpClient httpClient;
    private readonly bool enableSendingToTeams;

    public TeamsEscalationService(IConfiguration configuration, HttpClient httpClient, ILogger<TeamsEscalationService> logger)
    {
        this.logger = logger;
        this.httpClient = httpClient;
        enableSendingToTeams = string.IsNullOrWhiteSpace(configuration["TeamsWebHook"]) ? false : true;
        httpClient.BaseAddress = new System.Uri(configuration["TeamsWebHook"]);
    }

    public async Task EscalateAsync(string payload, string title, CancellationToken cancellationToken)
    {
        // TODO: send notif to teams channel (not via webhook)

        if (string.IsNullOrWhiteSpace(httpClient.BaseAddress.AbsolutePath)) return;
        logger.LogInformation("Send escalation message to Teams channel");
        var content = new StringContent(payload);

        if (enableSendingToTeams)
        {
            await httpClient.PostAsync("", content, cancellationToken);
        }

        await Task.CompletedTask;
    }
}
