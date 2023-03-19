// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CustomQABot.Dialogs;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CustomQABot.Bots;

public class CustomQABot<T> : ActivityHandler where T : Dialog
{
    private const string WELCOME_TEMPLATE = "CustomQABot.Cards.WelcomeCard.json";
    private readonly string[] FEEDBACK_RESPONSES = { "YES", "NO" };

    private readonly BotState conversationState;
    private readonly Dialog dialog;
    private readonly BotState userState;
    private readonly ILogger logger;
    private readonly IConfiguration configuration;

    public CustomQABot(IConfiguration configuration, ConversationState conversationState,
        UserState userState, T dialog, ILogger<CustomQABot<T>> logger)
    {
        this.configuration = configuration;
        this.conversationState = conversationState;
        this.userState = userState;
        this.dialog = dialog;
        this.logger = logger;
    }


    public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
    {
        if (turnContext.Activity.ChannelId.Equals(Channels.Msteams))
        {
            // MS Teams specific handling
            turnContext.Activity.Text = turnContext.Activity.RemoveRecipientMention();
        }

        await base.OnTurnAsync(turnContext, cancellationToken);

        //// Save any state changes that might have occurred during the turn.
        //await conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
        //await userState.SaveChangesAsync(turnContext, false, cancellationToken);
    }

    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        // check if contain feedback
        string feedbackCode = string.Empty;

        if (!string.IsNullOrWhiteSpace(turnContext.Activity.Text)
            && FEEDBACK_RESPONSES.Contains(turnContext.Activity.Text.ToUpper())
            && turnContext.Activity.Value != null)
        {
            var feedback = (Newtonsoft.Json.Linq.JObject)turnContext.Activity.Value;
            if (feedback != null)
            {
                feedbackCode = feedback["Feedback"].ToString();
            }
        }

        if (!string.IsNullOrWhiteSpace(feedbackCode) && feedbackCode.StartsWith("FEEDBACK"))
        {
            await turnContext.SendActivityAsync(MessageFactory.Text("Thanks for your feedback!"), cancellationToken);
            var stateAccessor = conversationState.CreateProperty<Transcript>(nameof(Transcript));
            var feedback = await stateAccessor.GetAsync(turnContext, () => new Transcript(), cancellationToken);
            feedback.NegativeFeedbackCount += feedbackCode.ToUpper() == "FEEDBACK-NO" ? 1 : 0;
        }
        else
        {
            // Run the Dialog with the new message Activity.
            await dialog.RunAsync(turnContext, conversationState.CreateProperty<DialogState>
                (nameof(DialogState)), cancellationToken);
        }
    }

    protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        foreach (var member in membersAdded)
        {
            // Greet anyone that was not the target (recipient) of this message.
            // To learn more about Adaptive Cards, see https://aka.ms/msbot-adaptivecards for more details.
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                var welcomeCard = Cards.CardBuilder.CreateAdaptiveCardAttachment(WELCOME_TEMPLATE, GetType().Assembly);
                var response = MessageFactory.Attachment(welcomeCard, ssml: "Welcome to UOB Bot");
                await turnContext.SendActivityAsync(response, cancellationToken);
            }
        }
    }
}


