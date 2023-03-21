using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CustomQABot.Middleware;

public class TranscriptMiddleware : IMiddleware
{
    private static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, MaxDepth = null };
    private readonly ILogger<TranscriptMiddleware> logger;
    private readonly BotState conversationState;
    private readonly BotState userState;

    //private readonly string defaultNoAnswer;

    public TranscriptMiddleware(ConversationState conversationState, UserState userState,
        ILogger<TranscriptMiddleware> logger)
    {
        this.logger = logger;
        this.conversationState = conversationState;
        this.userState = userState;
    }

    /// Records incoming and outgoing activities to the conversation store.
    public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate nextTurn, CancellationToken cancellationToken)
    {
        var chatQueue = new Queue<IMessageActivity>();

        // log incoming activity at beginning of turn
        if (turnContext.Activity != null)
        {
            turnContext.Activity.From ??= new ChannelAccount();
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                LogActivity(chatQueue, CloneActivity(turnContext.Activity));
            }
        }

        // hook up onSend pipeline
        turnContext.OnSendActivities(async (ctx, activities, nextSend) =>
        {
            // run full pipeline
            var responses = await nextSend().ConfigureAwait(false);

            foreach (var activity in activities.Where(a => a.Type == ActivityTypes.Message))
            {
                LogActivity(chatQueue, CloneActivity(activity));
            }
            //_ = TryLogTranscriptAsync(transcript, ctx, logger, cancellationToken);
            return responses;
        });


        // process bot logic
        await nextTurn(cancellationToken).ConfigureAwait(false);

        // Save any state changes that might have occurred during the turn.
        await conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
        await userState.SaveChangesAsync(turnContext, false, cancellationToken);

        // flush transcript at end of turn
        // NOTE: We are not awaiting this task by design, TryLogTranscriptAsync() observes all exceptions
        // and we don't need to or want to block execution on the completion.
        await TryLogTranscriptAsync(chatQueue, turnContext, logger, cancellationToken);
    }

    private async Task TryLogTranscriptAsync(Queue<IMessageActivity> chatQueue, ITurnContext turnContext,
        ILogger<TranscriptMiddleware> logger, CancellationToken cancellationToken)
    {
        var propertyAccessor = userState.CreateProperty<Feedback>(nameof(Feedback));
        var transcript = await propertyAccessor.GetAsync(turnContext, () => new Feedback(), cancellationToken);
        try
        {
            while (chatQueue.Count > 0)
            {
                // Process the queue and log all the activities in parallel.
                var activity = chatQueue.Dequeue();
                BotAssert.ActivityNotNull(activity);
                UpdateFeedbackTranscript(transcript, activity);
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Transcript logActivity failed with {ex}");
        }

        // Save any state changes that might have occurred during the turn.
        await conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
        await userState.SaveChangesAsync(turnContext, false, cancellationToken);
    }

    private static IMessageActivity CloneActivity(IMessageActivity activity)
    {
        activity = JsonConvert.DeserializeObject<Activity>(JsonConvert.SerializeObject(activity, jsonSettings), new JsonSerializerSettings { MaxDepth = null });
        var activityWithId = EnsureActivityHasId(activity);

        return activityWithId;
    }

    private static IMessageActivity EnsureActivityHasId(IMessageActivity activity)
    {
        var activityWithId = activity;

        if (activity == null)
        {
            throw new ArgumentNullException(nameof(activity), "Cannot check or add Id on a null Activity.");
        }

        if (string.IsNullOrEmpty(activity.Id))
        {
            var generatedId = $"g_{Guid.NewGuid()}";
            activity.Id = generatedId;
        }
        return activityWithId;
    }

    private static void LogActivity(Queue<IMessageActivity> transcript, IMessageActivity activity)
    {
        transcript.Enqueue(activity);
    }

    private static void UpdateFeedbackTranscript(Feedback feedback, IMessageActivity activity)
    {
        if (string.IsNullOrWhiteSpace(feedback.Name))
        {
            feedback.Name = string.IsNullOrEmpty(activity.ReplyToId) ? activity.From.Name : string.Empty;
        }
        var message = string.IsNullOrWhiteSpace(activity.Text) ? null : activity.Text.Trim();
        message ??= string.IsNullOrWhiteSpace(activity.Speak) ? null : activity.Speak.Trim();

        if (!string.IsNullOrWhiteSpace(message))
        {
            message = message.Replace("FEEDBACK-YES", "Yes");
            message = message.Replace("FEEDBACK-REPHRASE", "Rephrase");
            message = message.Replace("FEEDBACK-AGENT", "Ask agent");

            int start = message.IndexOf("<a>", 0);
            int end = message.IndexOf("</a>", 0) + 4;
            message = message.Remove(start, end - start);

            if (message == "Did you mean:" && activity.Attachments.Count > 0)
            {
                var card = HeroCard.ContentType == activity.Attachments[0].ContentType
                    ? (Newtonsoft.Json.Linq.JObject)activity.Attachments[0].Content : null;

                if (card != null)
                {
                    message = $"{message}{Environment.NewLine} - {string.Join($"{Environment.NewLine} - ", card["buttons"].ToArray().Select(b => b["title"].ToString()).ToArray())}";
                }
            }

            var sender = string.IsNullOrEmpty(activity.ReplyToId) ? "USR:" : "BOT:";
            feedback.Chats.Add(new Chat { Message = message, Sender = sender });
        }
    }
}