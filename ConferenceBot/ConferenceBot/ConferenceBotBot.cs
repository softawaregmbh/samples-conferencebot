// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace ConferenceBot
{
    public class ConferenceBotBot : IBot
    {
        private readonly ConversationState conversationState;
        private IStatePropertyAccessor<ScheduleQuery> scheduleQueryAccessor;

        public ConferenceBotBot(ConversationState conversationState)
        {
            this.scheduleQueryAccessor = conversationState.CreateProperty<ScheduleQuery>(nameof(ScheduleQuery));
            this.conversationState = conversationState ?? throw new System.ArgumentNullException(nameof(conversationState));
        }
        
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                var scheduleQuery = await scheduleQueryAccessor.GetAsync(turnContext, () => new ScheduleQuery());

                var message = turnContext.Activity.Text;

                switch (scheduleQuery.LastQuestion)
                {
                    case QuestionType.Time:
                        scheduleQuery.Time = message;
                        break;
                    case QuestionType.Track:
                        scheduleQuery.Track = message;
                        break;
                    default:
                        break;
                }

                if (scheduleQuery.Time == null)
                {
                    scheduleQuery.LastQuestion = QuestionType.Time;
                    await turnContext.SendActivityAsync("Um welche Uhrzeit?");
                }
                else if (scheduleQuery.Track == null)
                {
                    scheduleQuery.LastQuestion = QuestionType.Track;
                    await turnContext.SendActivityAsync("In welchem Track?");
                }

                await conversationState.SaveChangesAsync(turnContext);
            }
        }
    }
}
