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
        private ScheduleQuery scheduleQuery;

        public ConferenceBotBot()
        {
            this.scheduleQuery = new ScheduleQuery();
        }
        
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                var message = turnContext.Activity.Text;

                switch (this.scheduleQuery.LastQuestion)
                {
                    case QuestionType.Time:
                        this.scheduleQuery.Time = message;
                        break;
                    case QuestionType.Track:
                        this.scheduleQuery.Track = message;
                        break;
                    default:
                        break;
                }

                if (this.scheduleQuery.Time == null)
                {
                    this.scheduleQuery.LastQuestion = QuestionType.Time;
                    await turnContext.SendActivityAsync("Um welche Uhrzeit?");
                }
                else if (this.scheduleQuery.Track == null)
                {
                    this.scheduleQuery.LastQuestion = QuestionType.Track;
                    await turnContext.SendActivityAsync("In welchem Track?");
                }
            }
        }
    }
}
