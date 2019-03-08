// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;

namespace ConferenceBot
{
    public class ConferenceBotBot : IBot
    {
        private readonly ConversationState conversationState;
        private IStatePropertyAccessor<ScheduleQuery> scheduleQueryAccessor;
        private IStatePropertyAccessor<DialogState> dialogStateAccessor;
        private DialogSet dialogs;

        public ConferenceBotBot(ConversationState conversationState)
        {
            this.dialogStateAccessor = conversationState.CreateProperty<DialogState>(nameof(DialogState));
            this.scheduleQueryAccessor = conversationState.CreateProperty<ScheduleQuery>(nameof(ScheduleQuery));
            this.conversationState = conversationState ?? throw new System.ArgumentNullException(nameof(conversationState));

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

        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                var dialogContext = await dialogs.CreateContextAsync(turnContext);
                var result = await dialogContext.ContinueDialogAsync();

                if (result.Status == DialogTurnStatus.Empty)
                {
                    await dialogContext.BeginDialogAsync("scheduleDialog");
                }

                await conversationState.SaveChangesAsync(turnContext);
            }
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

            await stepContext.Context.SendActivityAsync($"Wir sehen nach, was um {scheduleQuery.Time} im {scheduleQuery.Track}-Track läuft...");

            await stepContext.Context.SendActivityAsync(Activity.CreateTypingActivity());

            await Task.Delay(2000);

            // send card
            var json = await File.ReadAllTextAsync(@"talkdetails.json");
            var card = JsonConvert.DeserializeObject(json);

            var activity = MessageFactory.Attachment(
                new Attachment("application/vnd.microsoft.card.adaptive", content: card));

            await stepContext.Context.SendActivityAsync(activity);

            return await stepContext.EndDialogAsync(scheduleQuery);
        }
    }
}
