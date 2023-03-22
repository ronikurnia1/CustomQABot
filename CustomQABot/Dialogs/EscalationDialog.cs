using CustomQABot.Cards;
using CustomQABot.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CustomQABot.Dialogs;

public class EscalationDialog : ComponentDialog
{
    private const string ESCALATION_INPUT_TEMPLATE = "CustomQABot.Cards.EscalationInputCard.json";
    private const string ESCALATION_SUBMIT_TEMPLATE = "CustomQABot.Cards.EscalationSubmitCard.json";
    private const string ESCALATION_INPUT_DIALOG_ID = "escalationInput";

    private readonly UserState userState;


    private readonly IEscalationService emailService;
    private readonly IEscalationService teamsService;

    public EscalationDialog(IConfiguration configuration,
        UserState userState, EmailEscalationService escalationService,
        TeamsEscalationService teamsEscalationService) : base(nameof(EscalationDialog))
    {
        AddDialog(new TextPrompt(ESCALATION_INPUT_DIALOG_ID));

        AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
        {
            EscalationInputStepAsync
        }));

        this.userState = userState;

        emailService = escalationService;
        teamsService = teamsEscalationService;

        // The initial child Dialog to run.
        InitialDialogId = nameof(WaterfallDialog);
    }

    private async Task<DialogTurnResult> EscalationInputStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        return await stepContext.PromptAsync(ESCALATION_INPUT_DIALOG_ID,
            GetEscalationInput(GetType().Assembly), cancellationToken);
    }

    protected override async Task<DialogTurnResult> OnContinueDialogAsync(DialogContext innerDc, CancellationToken cancellationToken = default)
    {
        if (innerDc.Context.Activity.Value != null)
        {
            await Escalate(innerDc.Parent.Context, cancellationToken);
            return await innerDc.EndDialogAsync(null, cancellationToken);
        }
        // user skip, cancel the form, asking new question
        var activity = innerDc.Context.Activity;
        return await innerDc.EndDialogAsync(activity, cancellationToken);
    }

    private static PromptOptions GetEscalationInput(Assembly assembly)
    {
        var card = CardBuilder.CreateAdaptiveCardAttachment(ESCALATION_INPUT_TEMPLATE, assembly);
        var activity = MessageFactory.Attachment(card, ssml: "Fill the form to ask agent");
        return new PromptOptions
        {
            Prompt = (Activity)activity
        };
    }


    private async Task Escalate(ITurnContext context, CancellationToken cancellationToken)
    {
        var accessor = userState.CreateProperty<Feedback>(nameof(Feedback));
        var feedback = await accessor.GetAsync(context, () => new Feedback(), cancellationToken);
        var escalationInput = (Newtonsoft.Json.Linq.JObject)context.Activity.Value;
        feedback.Title = escalationInput["title"].ToString();
        feedback.Details = escalationInput["details"].ToString();
        feedback.DateTime = DateTime.Now.ToString("MMMM dd, yyyy hh:mm tt");
        feedback.Logo = "https://botuob-webapp.azurewebsites.net/images/UOB_transparent.png";
        var card = CardBuilder.CreateAdaptiveCard(ESCALATION_SUBMIT_TEMPLATE, feedback, GetType().Assembly);

        // Transcript to user
        // await innerDc.Context.SendActivityAsync(MessageFactory.Attachment(card.Attachment), cancellationToken);
        // Escalate to email
        await emailService.EscalateAsync(card.Html, cancellationToken);
        // Escalation to Teams
        await teamsService.EscalateAsync(card.CardJson, cancellationToken);
        await context.SendActivityAsync(MessageFactory.Text("Thank you, your input has been sent to agent."), cancellationToken);

        feedback.NegativeFeedbackCount = 0;
        feedback.Chats = new List<Chat>();

        await Task.CompletedTask;
    }


}


