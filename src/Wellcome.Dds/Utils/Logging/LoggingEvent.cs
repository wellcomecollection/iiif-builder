using System;
using System.Collections.Generic;
using System.Linq;

namespace Utils.Logging
{

    public class LoggingEvent
    {
        public string Message { get; set; }
        public long Split { get; set; }
        public long Total { get; set; }

        public static LoggingEvent FromTuple(Tuple<long, long, string> eventTuple)
        {
            return new LoggingEvent
            {
                Message = eventTuple.Item3,
                Split = eventTuple.Item2,
                Total = eventTuple.Item1
            };
        }

        public static List<LoggingEvent> FromTuples(IEnumerable<Tuple<long, long, string>> eventTuples)
        {
            return eventTuples.Select(FromTuple).ToList();
        }
    }
}
