using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConferenceBot
{
    public class ScheduleQuery
    {
        public QuestionType LastQuestion { get; set; }

        public string Time { get; set; }

        public string Track { get; set; }
    }
}
