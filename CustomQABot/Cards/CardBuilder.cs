using AdaptiveCards;
using AdaptiveCards.Rendering.Html;
using AdaptiveCards.Templating;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using System.IO;

namespace CustomQABot.Cards;

public static class CardBuilder
{
    private const string CONTENT_TYPE = "application/vnd.microsoft.card.adaptive";

    // Load attachment from embedded resource.
    public static Attachment CreateAdaptiveCardAttachment(string template, System.Reflection.Assembly assembly)
    {
        var cardResourcePath = template;
        using var stream = assembly.GetManifestResourceStream(cardResourcePath);
        using var reader = new StreamReader(stream);
        var adaptiveCard = reader.ReadToEnd();
        return new Attachment()
        {
            ContentType = CONTENT_TYPE,
            Content = JsonConvert.DeserializeObject(adaptiveCard),
        };
    }


    public static AdaptiveCardResult CreateAdaptiveCard<T>(string template, Transcript payload, System.Reflection.Assembly assembly)
    {
        var cardResourcePath = template;
        using var stream = assembly.GetManifestResourceStream(cardResourcePath);
        using var reader = new StreamReader(stream);
        var templateCard = reader.ReadToEnd();
        AdaptiveCardTemplate cardTemplate = new(templateCard);
        var cardJson = cardTemplate.Expand(payload);


        AdaptiveCardRenderer renderer = new AdaptiveCardRenderer();
        var adaptiveCard = AdaptiveCard.FromJson(cardJson).Card;
        RenderedAdaptiveCard renderedCard = renderer.RenderCard(adaptiveCard);

        var attachment = new Attachment()
        {
            ContentType = CONTENT_TYPE,
            Content = JsonConvert.DeserializeObject(cardJson)
        };

        var cardForTeams = @$"{{""type"":""message"",""attachments"":[{{""contentType"":""{CONTENT_TYPE}"",""contentUrl"":null,""content"":{cardJson}}}]}}";

        return new AdaptiveCardResult
        {
            Attachment = attachment,
            Html = renderedCard.Html.ToString(),
            CardJson = cardForTeams,
        };
    }

    public class AdaptiveCardResult
    {
        public Attachment Attachment { get; set; }
        public string Html { get; set; }
        public string CardJson { get; set; }
    }

}
