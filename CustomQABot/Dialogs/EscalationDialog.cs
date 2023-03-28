using CustomQABot.Cards;
using CustomQABot.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CustomQABot.Dialogs;

public class EscalationDialog : ComponentDialog
{
    private const string ESCALATION_INPUT_TEMPLATE = "CustomQABot.Cards.EscalationInputCard.json";

    private const string ESCALATION_SUBMIT_TEMPLATE = "CustomQABot.Cards.EscalationSubmitCard.json";
    private const string ESCALATION_No_TRANSCRIPT_SUBMIT_TEMPLATE = "CustomQABot.Cards.EscalationNoTranscriptSubmitCard.json";

    private const string ESCALATION_INPUT_DIALOG_ID = "escalationInput";

    private readonly UserState userState;
    private readonly bool includeChatTranscript;

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
        includeChatTranscript = configuration["IncludeChatTranscript"] != null ? configuration.GetValue<bool>("IncludeChatTranscript") : false;
        // The initial child Dialog to run.
        InitialDialogId = nameof(WaterfallDialog);
    }

    private async Task<DialogTurnResult> EscalationInputStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        return await stepContext.PromptAsync(ESCALATION_INPUT_DIALOG_ID,
            GetEscalationInput(GetType().Assembly), cancellationToken).ConfigureAwait(false);
    }

    protected override async Task<DialogTurnResult> OnContinueDialogAsync(DialogContext innerDc, CancellationToken cancellationToken = default)
    {
        if (innerDc.Context.Activity.Value != null)
        {
            await Escalate(innerDc.Parent.Context, cancellationToken).ConfigureAwait(false);
            return await innerDc.EndDialogAsync(null, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (MainDialog.ASK_AGENT.Contains(innerDc.Context.Activity.Text.ToUpper()))
            {
                return await base.OnContinueDialogAsync(innerDc, cancellationToken);
            }
            // user skip, cancel the form, asking new question
            var activity = innerDc.Context.Activity;
            return await innerDc.EndDialogAsync(activity, cancellationToken).ConfigureAwait(false);
        }
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

        var template = includeChatTranscript ? ESCALATION_SUBMIT_TEMPLATE : ESCALATION_No_TRANSCRIPT_SUBMIT_TEMPLATE;
        var card = CardBuilder.CreateAdaptiveCard(template, feedback, GetType().Assembly);

        var message = MessageFactory.Text("Thank you, you will receive your ticket number for your request shortly.");
        await context.SendActivityAsync(message, cancellationToken);

        // Transcript to user
        // await innerDc.Context.SendActivityAsync(MessageFactory.Attachment(card.Attachment), cancellationToken);
        // Escalate to email
        await emailService.EscalateAsync(card.Html, feedback.Title, cancellationToken).ConfigureAwait(false);
        // Escalation to Teams
        await teamsService.EscalateAsync(card.CardJson, feedback.Title, cancellationToken).ConfigureAwait(false);

        feedback.NegativeFeedbackCount = 0;
        feedback.Chats = new List<Chat>();

        await Task.CompletedTask;
    }


}


