using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Utils.Logging
{
    public class SmallJobLogger : ISimpleLogger
    {
        private readonly List<Tuple<long, long, string>> eventList = new List<Tuple<long, long, string>>();
        private readonly Stopwatch sw = new Stopwatch();
        private long previousElapsedMilliseconds = 0;

        public List<Tuple<long, long, string>> GetEvents()
        {
            return eventList;
        }

        // this allows you to supply a message to your preferred logging mechanism
        private readonly Action<string>? callback;
        private readonly string callbackPrefix;

        public SmallJobLogger(string callbackPrefix, Action<string>? callback)
        {
            this.callbackPrefix = callbackPrefix;
            this.callback = callback;
        }

        public void Start()
        {
            sw.Start();
            Log("Starting SmallJobLogger");
        }

        public void Stop()
        {
            Log("Stopping SmallJobLogger");
            sw.Stop();
        }

        public void LogFormat(string format, params object[] args)
        {
            Log(string.Format(format, args));
        }

        public void Log(string message)
        {
            long total = sw.ElapsedMilliseconds;
            long split = total - previousElapsedMilliseconds;
            previousElapsedMilliseconds = total;

            var ev = new Tuple<long, long, string>(total, split, message);
            eventList.Add(ev);
            callback?.Invoke($"{callbackPrefix} [{total}|{split}] {message}");
        }
    }
}
