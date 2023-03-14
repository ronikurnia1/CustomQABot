// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CustomQABot.Bots;

public class CustomQABot<T> : ActivityHandler where T : Dialog
{
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
            if (turnContext.Activity.Id.Contains("f"))
            {
                // replace id of something like f:024f2e57-2b01-d703-0d97-008da7c94fa5 
                // to into something that can be converted to int
                var rnd = new Random();
                turnContext.Activity.Id = rnd.NextInt64().ToString();   
            }
        }

        await base.OnTurnAsync(turnContext, cancellationToken);

        // Save any state changes that might have occurred during the turn.
        await conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
        await userState.SaveChangesAsync(turnContext, false, cancellationToken);
    }

    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        // Get the state properties from the turn context.
        var stateAccessors = userState.CreateProperty<FeedbackCounter>(nameof(FeedbackCounter));
        var feedbackCounter = await stateAccessors.GetAsync(turnContext, () => new FeedbackCounter());

        if (turnContext.Activity.Value != null && int.TryParse(turnContext.Activity.Value.ToString(), out int value) && value == 669)
        {
            feedbackCounter.NegativeFeedbackCount += 1;
            await stateAccessors.SetAsync(turnContext, feedbackCounter);
        }

        // Run the Dialog with the new message Activity.
        await dialog.RunAsync(turnContext, conversationState.CreateProperty<DialogState>
            (nameof(DialogState)), cancellationToken);


        if (feedbackCounter.NegativeFeedbackCount >= 3)
        {
            // TODO: Got 3 negative feedbacks, escalate here
            await turnContext.SendActivityAsync(MessageFactory.Text("Escalation signal triggered!"), cancellationToken);

            feedbackCounter.NegativeFeedbackCount = 0;
            await stateAccessors.SetAsync(turnContext, feedbackCounter);
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
                var welcomeCard = CreateAdaptiveCardAttachment();
                var response = MessageFactory.Attachment(welcomeCard, ssml: "Welcome to UOB Bot");
                await turnContext.SendActivityAsync(response, cancellationToken);
            }
        }
    }


    // Load attachment from embedded resource.
    private Attachment CreateAdaptiveCardAttachment()
    {
        var cardResourcePath = "CustomQABot.Cards.WelcomeCard.json";
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

