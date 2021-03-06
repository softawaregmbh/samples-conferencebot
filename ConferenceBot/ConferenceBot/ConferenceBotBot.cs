﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.CognitiveServices.Language.LUIS.Runtime.Models;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ConferenceBot
{
    public class ConferenceBotBot : IBot
    {
        private readonly ConversationState conversationState;
        private readonly IRecognizer recognizer;
        private IStatePropertyAccessor<ScheduleQuery> scheduleQueryAccessor;
        private IStatePropertyAccessor<DialogState> dialogStateAccessor;
        private DialogSet dialogs;

        public ConferenceBotBot(ConversationState conversationState, IRecognizer recognizer)
        {
            this.dialogStateAccessor = conversationState.CreateProperty<DialogState>(nameof(DialogState));
            this.scheduleQueryAccessor = conversationState.CreateProperty<ScheduleQuery>(nameof(ScheduleQuery));
            this.conversationState = conversationState ?? throw new System.ArgumentNullException(nameof(conversationState));
            this.recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));

            InitializeWaterfallDialogs();
        }
        
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                if (!await TryWithLuisAsync(turnContext, cancellationToken))
                {
                    await ExecuteWaterfall(turnContext);
                }

            }
        }

        private async Task ExecuteWaterfall(ITurnContext turnContext)
        {
            var dialogContext = await dialogs.CreateContextAsync(turnContext);
            var result = await dialogContext.ContinueDialogAsync();

            if (result.Status == DialogTurnStatus.Empty)
            {
                await dialogContext.BeginDialogAsync("scheduleDialog");
            }

            await conversationState.SaveChangesAsync(turnContext);
        }

        private async Task<bool> TryWithLuisAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var luisResult = await recognizer.RecognizeAsync(turnContext, cancellationToken);
            var topIntent = luisResult.GetTopScoringIntent();

            // check sentiment
            var sentimentScore = ParseSentiment(luisResult);
            if (sentimentScore < 0.3)
            {
                await turnContext.SendActivityAsync("Ups.. da ist wohl jemand schlecht gelaunt. Ein Support-Mitarbeiter meldet sich in Kürze.");

                return true;
            }

            // check top intent
            if (topIntent.intent == "ScheduleQuery" && topIntent.score > 0.8)
            {
                try
                {
                    var time = ParseTime(luisResult.Entities);
                    var track = ParseTrack(luisResult.Entities);

                    if (!string.IsNullOrEmpty(time) && !string.IsNullOrEmpty(track))
                    {
                        await ShowResultingCard(turnContext, new ScheduleQuery()
                        {
                            Time = time,
                            Track = track
                        });

                        return true;
                    }
                }
                catch (Exception)
                {
                    // LUIS failed
                }
            }

            return false;
        }

        private float ParseSentiment(RecognizerResult luisResult)
        {
            return (float)((JObject)luisResult.Properties["sentiment"])["score"];
        }

        private string ParseTrack(JObject entities)
        {
            return entities["track"].First.Value<string>();
        }

        private string ParseTime(JObject entities)
        {
            return entities["datetime"].First["timex"].First.Value<string>();
        }

        private void InitializeWaterfallDialogs()
        {
            this.dialogs = new DialogSet(this.dialogStateAccessor);

            this.dialogs.Add(new DateTimePrompt("time"));
            this.dialogs.Add(new ChoicePrompt("track")
            {
                Style = ListStyle.SuggestedAction
            });

            this.dialogs.Add(new WaterfallDialog("scheduleDialog")
                        .AddStep(TimeStepAsync)
                        .AddStep(TrackStepAsync)
                        .AddStep(CompleteStepAsync));
        }
        private async Task<DialogTurnResult> TimeStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync("time", new PromptOptions
            {
                Prompt = MessageFactory.Text("Um welche Uhrzeit?")
            });
        }

        private async Task<DialogTurnResult> TrackStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var scheduleQuery = await scheduleQueryAccessor.GetAsync(stepContext.Context, () => new ScheduleQuery());

            var dateTimes = stepContext.Result as IList<DateTimeResolution>;
            scheduleQuery.Time = dateTimes?.FirstOrDefault()?.Value;

            return await stepContext.PromptAsync("track", new PromptOptions
            {
                Prompt = MessageFactory.Text("Auf welchem Track?"),
                Choices = new []
                {
                    new Choice("Apps & Infrastructure"),
                    new Choice("Data & AI"),
                    new Choice("Modern Workplace"),
                    new Choice("Hands-on Sessions")
                }
            });
        }

        private async Task<DialogTurnResult> CompleteStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var scheduleQuery = await scheduleQueryAccessor.GetAsync(stepContext.Context);
            scheduleQuery.Track = ((FoundChoice)stepContext.Result).Value;

            await ShowResultingCard(stepContext.Context, scheduleQuery);

            return await stepContext.EndDialogAsync(scheduleQuery);
        }

        private static async Task ShowResultingCard(ITurnContext context, ScheduleQuery scheduleQuery)
        {
            await context.SendActivityAsync($"Wir sehen nach, was um {scheduleQuery.Time} im {scheduleQuery.Track}-Track läuft...");

            await context.SendActivityAsync(Activity.CreateTypingActivity());

            await Task.Delay(2000);

            // send card
            var json = await File.ReadAllTextAsync(@"talkdetails.json");
            var card = JsonConvert.DeserializeObject(json);

            var activity = MessageFactory.Attachment(
                new Attachment("application/vnd.microsoft.card.adaptive", content: card));

            await context.SendActivityAsync(activity);
        }
    }
}
