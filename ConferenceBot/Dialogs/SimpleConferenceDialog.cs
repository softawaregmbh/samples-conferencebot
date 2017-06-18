using ApiSummitBot.Models;
using Microsoft.Bot.Builder.FormFlow;
using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace ConferenceBot.Dialogs
{
    public class SimpleConferenceDialog
    {
        public static IForm<ScheduleQuery> BuildForm()
        {
            return new FormBuilder<ScheduleQuery>()
                .Message("Hi, I need some information from you!")
                .Field(nameof(ScheduleQuery.Time))
                .Field(nameof(ScheduleQuery.Room))
                .OnCompletion(async (context, query) =>
                {
                    var typingMessage = context.MakeMessage();
                    typingMessage.Type = ActivityTypes.Typing;

                    await context.PostAsync(typingMessage);

                    await Task.Delay(3000);

                    var responseMessage = context.MakeMessage();
                    //responseMessage.Text = $"In Raum {query.Room} findet ein Vortrag über Cognitive Services statt.";

                    var card = new HeroCard()
                    {
                        Title = "Cognitive Services",
                        Subtitle = "Roman Schacherl",
                        Text = $"In Raum {query.Room} findet ein Vortrag über Cognitive Services statt.",
                        Images = new List<CardImage>()
                        {
                            new CardImage()
                            {
                                Url = "https://api-summit.de/wp-content/uploads/2017/03/API_Summit-3914.jpg",
                                Tap = new CardAction(ActionTypes.OpenUrl, "Open", null, "https://www.api-summit.de")
                            }
                        }
                    };

                    responseMessage.Attachments.Add(card.ToAttachment());

                    await context.PostAsync(responseMessage);
                })
                .Build();
        }
    }
}