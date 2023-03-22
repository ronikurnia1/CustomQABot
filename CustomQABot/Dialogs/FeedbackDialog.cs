using CustomQABot.Cards;
using CustomQABot.Services;
using Microsoft.AspNetCore.Http;
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

public class FeedbackDialog : ComponentDialog
{
    private const string FEEDBACK_DATA = "feedbackData";
    private const string ESCALATION_INPUT_TEMPLATE = "CustomQABot.Cards.EscalationInputCard.json";
    private const string ESCALATION_SUBMIT_TEMPLATE = "CustomQABot.Cards.EscalationSubmitCard.json";
    private const string ESCALATION_INPUT_DIALOG_ID = "escalationInput";

    private readonly UserState userState;
    private readonly int negativeFeedbackThreshold;

    private readonly IEscalationService emailService;
    private readonly IEscalationService teamsService;

    public FeedbackDialog(IConfiguration configuration, UserState userState, EmailEscalationService escalationService,
        TeamsEscalationService teamsEscalationService) : base(nameof(FeedbackDialog))
    {
        AddDialog(new TextPrompt(nameof(TextPrompt)));
        AddDialog(new TextPrompt(ESCALATION_INPUT_DIALOG_ID));
        AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
        {
            AskFeedbackStepAsync,
            FollowUpStepAsync,
            EscalationInputStepAsync
        }));
        negativeFeedbackThreshold = int.TryParse(configuration["NegativeFeedbackThreshold"],
            out int threshold) ? threshold : 0;

        this.userState = userState;
        emailService = escalationService;
        teamsService = teamsEscalationService;

        // The initial child Dialog to run.
        InitialDialogId = nameof(WaterfallDialog);
    }

    private async Task<DialogTurnResult> AskFeedbackStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        return await stepContext.PromptAsync(nameof(TextPrompt), GetFeedbackPrompt(), cancellationToken);
    }

    private async Task<DialogTurnResult> FollowUpStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        var feedbackCode = (string)stepContext.Result;
        var accessor = userState.CreateProperty<Feedback>(nameof(Feedback));
        var feedback = await accessor.GetAsync(stepContext.Context, () => new Feedback(), cancellationToken);

        switch (feedbackCode.ToUpper())
        {
            case "YES":
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Thanks for your feedback!"), cancellationToken);
                return await stepContext.Parent.EndDialogAsync(null, cancellationToken);
            case "REPHRASE":
                if (negativeFeedbackThreshold > 0 && feedback.NegativeFeedbackCount >= negativeFeedbackThreshold)
                {
                    // Reset the counter
                    feedback.NegativeFeedbackCount = 0;
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("You've asked to rephrase for 3 times, why don't just ask agent?"), cancellationToken);
                    return await stepContext.NextAsync(feedback, cancellationToken);
                }
                else
                {
                    // count the effort
                    feedback.NegativeFeedbackCount += 1;
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("Please rephrase your question and try again"), cancellationToken);
                    return await stepContext.Parent.EndDialogAsync(null, cancellationToken);
                }
            case "ASK AGENT":
                return await stepContext.NextAsync(feedback, cancellationToken);
            default:
                // Restart from biggening 
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Please give a feedback first before continue."), cancellationToken);
                return await stepContext.ReplaceDialogAsync(nameof(TextPrompt), GetFeedbackPrompt(), cancellationToken);
        }
    }

    private async Task<DialogTurnResult> EscalationInputStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        return await stepContext.PromptAsync(ESCALATION_INPUT_DIALOG_ID, 
            GetEscalationInput(GetType().Assembly), cancellationToken);
    }

    protected override async Task<DialogTurnResult> OnContinueDialogAsync(DialogContext innerDc, CancellationToken cancellationToken = default)
    {
        if (innerDc.ActiveDialog.Id == ESCALATION_INPUT_DIALOG_ID)
        {
            if (innerDc.Context.Activity.Value == null)
            {
                await innerDc.Context.SendActivityAsync(MessageFactory.Text("Please fill up the form first"), cancellationToken);
                return await innerDc.ReplaceDialogAsync(ESCALATION_INPUT_DIALOG_ID, GetEscalationInput(GetType().Assembly), cancellationToken);
            }
            var accessor = userState.CreateProperty<Feedback>(nameof(Feedback));
            var feedback = await accessor.GetAsync(innerDc.Context, () => new Feedback(), cancellationToken);
            var escalationInput = (Newtonsoft.Json.Linq.JObject)innerDc.Context.Activity.Value;

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

            await innerDc.Context.SendActivityAsync(MessageFactory.Text("Thank you, your input has been sent to agent."), cancellationToken);

            feedback.NegativeFeedbackCount = 0;
            feedback.Chats = new List<Chat>();

            return await innerDc.EndDialogAsync(null, cancellationToken);
        }
        return await base.OnContinueDialogAsync(innerDc, cancellationToken);
    }


    private static PromptOptions GetFeedbackPrompt()
    {
        var promptCard = new HeroCard()
        {
            Text = "Was this answer helpful? \n\nIf not, please rephrase your question or Ask agent",
            Buttons = new List<CardAction> {
                new CardAction {
                Type = "messageBack",
                Title = "Yes",
                Text = "Yes",
                DisplayText = "Yes"
                },
                new CardAction {
                Type = "messageBack",
                Title = "Rephrase",
                Text = "Rephrase",
                DisplayText = "Rephrase"
                },
                new CardAction {
                Type = "messageBack",
                Title = "Ask Agent",
                Text = "Ask Agent",
                DisplayText = "Ask Agent"
                }
            }
        };
        var activity = MessageFactory.Attachment(promptCard.ToAttachment(), ssml: "Was this answer helpful?");
        return new PromptOptions
        {
            Prompt = (Activity)activity,
        };
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
}


