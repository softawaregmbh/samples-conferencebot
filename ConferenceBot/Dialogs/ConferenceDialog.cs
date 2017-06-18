using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace ConferenceBot.Dialogs
{
    [LuisModel("95e2a6b1-b2ca-4014-9ce8-b90b64a6487e", "2fc4f3b9a64841eba1bce764b6b568cd")]
    [Serializable]
    public class ConferenceDialog : LuisDialog<object>
    {
        [LuisIntent("")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            string message = $"Tut mir leid, das habe ich nicht verstanden: "
                + string.Join(", ", result.Intents.Select(i => i.Intent));
            await context.PostAsync(message);
            context.Wait(MessageReceived);
        }

        [LuisIntent("SessionQuery")]
        public async Task GetSessionInfo(IDialogContext context, LuisResult result)
        {
            var room = result.Entities.FirstOrDefault(p => p.Type == "Raum");
            var time = result.Entities.FirstOrDefault(p => p.Type == "Uhrzeit");

            await context.PostAsync($"Ich sehe nach, was im **{room.Entity}** um **{time.Entity}** läuft...");

            await Task.Delay(1000);

            await context.PostAsync($"Da ist die Keynote!");

            context.Wait(MessageReceived);
        }

        [LuisIntent("ThankYou")]
        public async Task ThankYou(IDialogContext context, LuisResult result)
        {
            await context.PostAsync($"Aber gerne.");
            await Task.Delay(1000);
            await context.PostAsync($"Jederzeit wieder.");

            context.Wait(MessageReceived);
        }

    }
}