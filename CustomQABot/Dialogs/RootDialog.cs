// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.QnA.Dialogs;
using Microsoft.Bot.Builder.AI.QnA.Models;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CustomQABot.Dialogs;

/// <summary>
/// This is an example root dialog. Replace this with your applications.
/// </summary>
public class RootDialog : ComponentDialog
{
    private const string DialogId = "initial-dialog";
    private const string ActiveLearningCardTitle = "Did you mean:";
    private const string ActiveLearningCardNoMatchText = "None of the above.";
    private const string ActiveLearningCardNoMatchResponse = "Thanks for the feedback.";
    private const string FEEDBACK_TEMPLATE = "CustomQABot.Cards.FeedbackCard.json";

    private const float ScoreThreshold = 0.3f;
    private const int TopAnswers = 3;
    private const string RankerType = "Default";
    private const bool IsTest = false;
    private const bool IncludeUnstructuredSources = true;

    private readonly ILogger<RootDialog> logger;


    /// <summary>
    /// Initializes a new instance of the <see cref="RootDialog"/> class.
    /// </summary>
    /// <param name="configuration">An <see cref="IConfiguration"/> instance.</param>
    public RootDialog(IConfiguration configuration, ILogger<RootDialog> logger) : base("root")
    {
        this.logger = logger;
        var qnaMakerDialog = CreateQnAMakerDialog(configuration);

        AddDialog(qnaMakerDialog);
        AddDialog(new WaterfallDialog(DialogId, new WaterfallStep[]
        {
            InitialStepAsync,
            FinalStepAsync,
        }));


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

    private async Task<DialogTurnResult> InitialStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        var response = await stepContext.BeginDialogAsync(nameof(QnAMakerDialog), null, cancellationToken);
        return response;
    }

    private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        var investigate = JsonConvert.SerializeObject(stepContext.Context.TurnState["turn"],
            new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore, Formatting = Formatting.Indented });

        logger.LogInformation($"====INVESTIGATTION: {investigate}");

        var feedbackCard = new HeroCard()
        {
            Text = "Was this answer helpful?",
            Buttons = new List<CardAction> {
                new CardAction {
                Type = "messageBack",
                Title = "Yes",
                Text = "Yes",
                DisplayText = "Yes",
                Value = new FeedbackValue {Feedback = "FEEDBACK-YES"}
                },
                new CardAction {
                Type = "messageBack",
                Title = "No",
                Text = "No",
                DisplayText = "No",
                Value = new FeedbackValue {Feedback = "FEEDBACK-NO"}
                }
            }
        };

        var feedback = MessageFactory.Attachment(feedbackCard.ToAttachment(), ssml: "Was this answer helpful?");
        await stepContext.Context.SendActivityAsync(feedback, cancellationToken);
        return await stepContext.EndDialogAsync();
    }


    private Attachment CreateAdaptiveCardAttachment(string template)
    {
        var cardResourcePath = template;
        using var stream = GetType().Assembly.GetManifestResourceStream(cardResourcePath);
        using var reader = new StreamReader(stream);
        var adaptiveCard = reader.ReadToEnd();
        return new Attachment()
        {
            ContentType = "application/vnd.microsoft.card.adaptive",
            Content = JsonConvert.DeserializeObject(adaptiveCard),
        };
    }

}


public class FeedbackValue
{
    public string Feedback { get; set; }
}

