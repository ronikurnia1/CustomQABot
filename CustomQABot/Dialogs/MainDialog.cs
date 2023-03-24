// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CustomQABot.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.QnA.Dialogs;
using Microsoft.Bot.Builder.AI.QnA.Models;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CustomQABot.Dialogs;

/// <summary>
/// This is an example root dialog. Replace this with your applications.
/// </summary>
public class MainDialog : ComponentDialog
{
    private const string DialogId = "qna-dialog";
    private const string ActiveLearningCardTitle = "Did you mean:";
    private const string ActiveLearningCardNoMatchText = "None of the above.";
    private const string ActiveLearningCardNoMatchResponse = "Thanks for the feedback.";
    private const string FEEDBACK_TEMPLATE = "CustomQABot.Cards.FeedbackCard.json";

    private const float ScoreThreshold = 0.3f;
    private const int TopAnswers = 3;
    private const string RankerType = "Default";
    private const bool IsTest = false;
    private const bool IncludeUnstructuredSources = true;

    private readonly string[] ASK_AGENT = { "ASK AGENT", "ESCALATE TO AGENT" };

    private readonly UserState userState;
    private readonly int negativeFeedbackThreshold;

    private readonly ILogger<MainDialog> logger;


    /// <summary>
    /// Initializes a new instance of the <see cref="MainDialog"/> class.
    /// </summary>
    /// <param name="configuration">An <see cref="IConfiguration"/> instance.</param>
    public MainDialog(IConfiguration configuration, UserState userState,
        EmailEscalationService emailEscalationService,
        TeamsEscalationService teamsEscalationService,
        ILogger<MainDialog> logger) : base(nameof(MainDialog))
    {
        this.logger = logger;

        AddDialog(new TextPrompt(nameof(TextPrompt)));
        AddDialog(CreateQnAMakerDialog(configuration));

        AddDialog(new EscalationDialog(configuration, userState,
            emailEscalationService, teamsEscalationService));
        AddDialog(new FeedbackDialog());

        AddDialog(new WaterfallDialog(DialogId, new WaterfallStep[]
        {
            InitialStepAsync,
            QnaStepAsync,
            FeedbackStepAsync
        }));

        negativeFeedbackThreshold = int.TryParse(configuration["NegativeFeedbackThreshold"],
            out int threshold) ? threshold : 0;
        this.userState = userState;


        // The initial child Dialog to run.
        InitialDialogId = DialogId;
    }

    private QnAMakerDialog CreateQnAMakerDialog(IConfiguration configuration)
    {
        const string missingConfigError = "{0} is missing or empty in configuration.";

        var hostname = configuration["LanguageEndpointHostName"];
        if (string.IsNullOrEmpty(hostname))
        {
            throw new ArgumentException(string.Format(missingConfigError, "LanguageEndpointHostName"));
        }

        var endpointKey = configuration["LanguageEndpointKey"];
        if (string.IsNullOrEmpty(endpointKey))
        {
            throw new ArgumentException(string.Format(missingConfigError, "LanguageEndpointKey"));
        }

        var knowledgeBaseId = configuration["ProjectName"];
        if (string.IsNullOrEmpty(knowledgeBaseId))
        {
            throw new ArgumentException(string.Format(missingConfigError, "ProjectName"));
        }

        var enablePreciseAnswer = bool.Parse(configuration["EnablePreciseAnswer"]);
        var displayPreciseAnswerOnly = bool.Parse(configuration["DisplayPreciseAnswerOnly"]);

        // Create a new instance of QnAMakerDialog with dialogOptions initialized.
        var noAnswer = MessageFactory.Text(configuration["DefaultAnswer"] ?? string.Empty);
        var qnaMakerDialog = new QnAMakerDialog(nameof(QnAMakerDialog), knowledgeBaseId,
            endpointKey, hostname, noAnswer: noAnswer,
            cardNoMatchResponse: MessageFactory.Text(ActiveLearningCardNoMatchResponse))
        {
            Threshold = ScoreThreshold,
            ActiveLearningCardTitle = ActiveLearningCardTitle,
            CardNoMatchText = ActiveLearningCardNoMatchText,
            Top = TopAnswers,
            Filters = { },
            QnAServiceType = ServiceType.Language,
            EnablePreciseAnswer = enablePreciseAnswer,
            DisplayPreciseAnswerOnly = displayPreciseAnswerOnly,
            IncludeUnstructuredSources = IncludeUnstructuredSources,
            RankerType = RankerType,
            IsTest = IsTest,
        };
        return qnaMakerDialog;
    }

    private async Task<DialogTurnResult> InitialStepAsync(WaterfallStepContext stepContext,
        CancellationToken cancellationToken)
    {
        // Check if any feedback from previous dialog cycle
        var feedbackCode = stepContext.Context.Activity.Text;
        var accessor = userState.CreateProperty<Feedback>(nameof(Feedback));
        var feedback = await accessor.GetAsync(stepContext.Context, () => new Feedback(), cancellationToken);

        switch (feedbackCode.ToUpper())
        {
            case "YES":
                // acknowledge the feedback and end the dialog
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("Thanks for your feedback!"), cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);

            case "REPHRASE":
                if (negativeFeedbackThreshold > 0 && feedback.NegativeFeedbackCount >= negativeFeedbackThreshold)
                {
                    // Reset the counter
                    feedback.NegativeFeedbackCount = 0;
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("You've tried to rephrase for 3 times, ask agent might be a good idea?"), cancellationToken);
                    // proceed with the escalation
                    return await stepContext.BeginDialogAsync(nameof(EscalationDialog), null, cancellationToken);
                }
                else
                {
                    // count the rephrase
                    feedback.NegativeFeedbackCount += 1;
                    var options = new PromptOptions
                    {
                        Prompt = MessageFactory.Text("Please rephrase your question and try again"),
                    };
                    return await stepContext.PromptAsync(nameof(TextPrompt), options, cancellationToken);
                }
            case string a when ASK_AGENT.Contains(a.ToUpper()):
                // proceed with the escalation
                return await stepContext.BeginDialogAsync(nameof(EscalationDialog), null, cancellationToken);
            default:
                // User skip the feedback
                // Continue with the QnA Maker Dialog
                var activity = stepContext.Context.Activity;
                return await stepContext.NextAsync(activity, cancellationToken);
        }
    }

    private async Task<DialogTurnResult> QnaStepAsync(WaterfallStepContext stepContext,
        CancellationToken cancellationToken)
    {
        if (stepContext.Result == null)
        {
            return await stepContext.EndDialogAsync(null, cancellationToken);
        }
        else
        {
            return await stepContext.BeginDialogAsync(nameof(QnAMakerDialog), null, cancellationToken);
        }
    }

    private async Task<DialogTurnResult> FeedbackStepAsync(WaterfallStepContext stepContext,
        CancellationToken cancellationToken)
    {
        return await stepContext.BeginDialogAsync(nameof(FeedbackDialog), null, cancellationToken);
    }

}
