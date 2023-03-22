// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CustomQABot.Bots;

public class CustomQABot<T> : TeamsActivityHandler where T : Dialog
{
    private const string WELCOME_TEMPLATE = "CustomQABot.Cards.WelcomeCard.json";
    private readonly BotState conversationState;
    private readonly BotState userState;

    private readonly Dialog dialog;
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
            turnContext.Activity.RemoveRecipientMention();
        }

        await base.OnTurnAsync(turnContext, cancellationToken);

        //// Save any state changes that might have occurred during the turn.
        //await conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
        //await userState.SaveChangesAsync(turnContext, false, cancellationToken);
    }

    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        // Run the Dialog with the new message Activity.
        await dialog.RunAsync(turnContext, conversationState.CreateProperty<DialogState>
            (nameof(DialogState)), cancellationToken);
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

    protected override async Task OnTeamsMembersAddedAsync(IList<TeamsChannelAccount> membersAdded, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        foreach (var teamMember in membersAdded)
        {
            if (teamMember.Id != turnContext.Activity.Recipient.Id && turnContext.Activity.Conversation.ConversationType != "personal")
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"Welcome to the team {teamMember.GivenName} {teamMember.Surname}."), cancellationToken);
            }
        }
    }

    protected override async Task OnInstallationUpdateActivityAsync(ITurnContext<IInstallationUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        if (turnContext.Activity.Conversation.ConversationType == "channel")
        {
            await turnContext.SendActivityAsync($"Welcome to UOB Bot. This bot is configured in {turnContext.Activity.Conversation.Name}");
        }
        else
        {
            await turnContext.SendActivityAsync("Welcome to UOB Bot.");
        }
    }

    //-----Subscribe to Conversation Events in Bot integration
    protected override async Task OnTeamsChannelCreatedAsync(ChannelInfo channelInfo, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        var heroCard = new HeroCard(text: $"{channelInfo.Name} is the Channel created");
        await turnContext.SendActivityAsync(MessageFactory.Attachment(heroCard.ToAttachment()), cancellationToken);
    }

    protected override async Task OnTeamsChannelRenamedAsync(ChannelInfo channelInfo, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        var heroCard = new HeroCard(text: $"{channelInfo.Name} is the new Channel name");
        await turnContext.SendActivityAsync(MessageFactory.Attachment(heroCard.ToAttachment()), cancellationToken);
    }

    protected override async Task OnTeamsChannelDeletedAsync(ChannelInfo channelInfo, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        var heroCard = new HeroCard(text: $"{channelInfo.Name} is the Channel deleted");
        await turnContext.SendActivityAsync(MessageFactory.Attachment(heroCard.ToAttachment()), cancellationToken);
    }

    protected override async Task OnTeamsMembersRemovedAsync(IList<TeamsChannelAccount> membersRemoved, TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        foreach (TeamsChannelAccount member in membersRemoved)
        {
            if (member.Id == turnContext.Activity.Recipient.Id)
            {
                // The bot was removed
                // You should clear any cached data you have for this team
            }
            else
            {
                var heroCard = new HeroCard(text: $"{member.Name} was removed from {teamInfo.Name}");
                await turnContext.SendActivityAsync(MessageFactory.Attachment(heroCard.ToAttachment()), cancellationToken);
            }
        }
    }

    protected override async Task OnTeamsTeamRenamedAsync(TeamInfo teamInfo, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        var heroCard = new HeroCard(text: $"{teamInfo.Name} is the new Team name");
        await turnContext.SendActivityAsync(MessageFactory.Attachment(heroCard.ToAttachment()), cancellationToken);
    }
    protected override async Task OnReactionsAddedAsync(IList<MessageReaction> messageReactions, ITurnContext<IMessageReactionActivity> turnContext, CancellationToken cancellationToken)
    {
        foreach (var reaction in messageReactions)
        {
            var newReaction = $"You reacted with '{reaction.Type}' to the following message: '{turnContext.Activity.ReplyToId}'";
            var replyActivity = MessageFactory.Text(newReaction);
            await turnContext.SendActivityAsync(replyActivity, cancellationToken);
        }
    }

    protected override async Task OnReactionsRemovedAsync(IList<MessageReaction> messageReactions, ITurnContext<IMessageReactionActivity> turnContext, CancellationToken cancellationToken)
    {
        foreach (var reaction in messageReactions)
        {
            var newReaction = $"You removed the reaction '{reaction.Type}' from the following message: '{turnContext.Activity.ReplyToId}'";
            var replyActivity = MessageFactory.Text(newReaction);
            await turnContext.SendActivityAsync(replyActivity, cancellationToken);
        }
    }

    // This method is invoked when message sent by user is updated in chat.
    protected override async Task OnTeamsMessageEditAsync(ITurnContext<IMessageUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        var replyActivity = MessageFactory.Text("Message is updated");
        await turnContext.SendActivityAsync(replyActivity, cancellationToken);
    }

    // This method is invoked when message sent by user is undeleted in chat.
    protected override async Task OnTeamsMessageUndeleteAsync(ITurnContext<IMessageUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        var replyActivity = MessageFactory.Text("Message is undeleted");
        await turnContext.SendActivityAsync(replyActivity, cancellationToken);
    }

    // This method is invoked when message sent by user is soft deleted in chat.
    protected override async Task OnTeamsMessageSoftDeleteAsync(ITurnContext<IMessageDeleteActivity> turnContext, CancellationToken cancellationToken)
    {
        var replyActivity = MessageFactory.Text("Message is soft deleted");
        await turnContext.SendActivityAsync(replyActivity, cancellationToken);
    }

}


