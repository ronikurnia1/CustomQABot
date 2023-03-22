using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CustomQABot.Dialogs;

public class FeedbackDialog : ComponentDialog
{
    public FeedbackDialog()
        : base(nameof(FeedbackDialog))
    {

        AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
        {
            AskFeedbackStepAsync
        }));

        // The initial child Dialog to run.
        InitialDialogId = nameof(WaterfallDialog);
    }

    private async Task<DialogTurnResult> AskFeedbackStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
    {
        await stepContext.Context.SendActivityAsync(GetFeedbackCard(), cancellationToken);
        return await stepContext.EndDialogAsync(null, cancellationToken);
    }

    private static IMessageActivity GetFeedbackCard()
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

        return MessageFactory.Attachment(promptCard.ToAttachment(), ssml: "Was this answer helpful?");
    }

}


