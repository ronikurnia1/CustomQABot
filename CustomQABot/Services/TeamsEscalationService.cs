using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CustomQABot.Services;

public class TeamsEscalationService : IEscalationService
{
    private readonly ILogger<TeamsEscalationService> logger;
    private readonly HttpClient httpClient;

    public TeamsEscalationService(IConfiguration configuration, HttpClient httpClient, ILogger<TeamsEscalationService> logger)
    {
        this.logger = logger;
        this.httpClient = httpClient;
        httpClient.BaseAddress = new System.Uri(configuration["TeamsWebHook"]);
    }

    public async Task EscalateAsync(string payload, CancellationToken cancellationToken)
    {
        logger.LogInformation("Send escalation message to Teams channel");
        var content = new StringContent(payload);
        await httpClient.PostAsync("", content, cancellationToken);
        await Task.CompletedTask;
    }
}
