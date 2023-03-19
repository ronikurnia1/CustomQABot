using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CustomQABot.Services;

public class EmailEscalationService : IEscalationService
{
    private readonly ILogger<EmailEscalationService> logger;
    private readonly string communicationServiceConnectionString;
    private readonly string[] recipients;
    private readonly string sender;

    public EmailEscalationService(IConfiguration configuration, ILogger<EmailEscalationService> logger)
    {
        this.logger = logger;
        communicationServiceConnectionString = configuration["CommunicationServiceConnectionString"];
        recipients = configuration["EmailEscalationDestination:Recipients"].Split(',');
        sender = configuration["EmailEscalationDestination:Sender"];
    }


    public async Task EscalateAsync(string payLoad, CancellationToken cancellationToken)
    {
        logger.LogInformation("Send escalation message via e-mail");
        EmailClient emailClient = new(communicationServiceConnectionString);

        // Create the email content
        var emailContent = new EmailContent("Escalation Chat Transcript")
        {
            PlainText = "Chat transcript available on HTML",
            Html = payLoad
        };

        // Create the To list
        var emailRecipients = new EmailRecipients(recipients.Select(s => new EmailAddress(s)).ToList());
        // Create the EmailMessage
        var emailMessage = new EmailMessage(
            senderAddress: sender,
            emailRecipients,
            emailContent);

        EmailSendOperation emailSendOperation = await emailClient.SendAsync(WaitUntil.Completed, emailMessage);
        logger.LogInformation($"Email Sent. Status = {emailSendOperation.Value.Status}");
        logger.LogInformation($"Email operation id = {emailSendOperation.Id}");

        await Task.CompletedTask;
    }
}
