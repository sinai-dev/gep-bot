using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace GepBot
{
    public class IntervalTask
    {
        internal static readonly List<IntervalTask> tasks = new();

        public static void Create(TimeSpan interval, Func<Task> task) => tasks.Add(new IntervalTask(interval, task));

        private readonly Func<Task> task;
        private readonly Timer timer;

        private IntervalTask(TimeSpan interval, Func<Task> task)
        {
            this.task = task;

            timer = new Timer(interval.TotalMilliseconds)
            {
                Enabled = true
            };
            timer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            timer.Start();
        }

        private async void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            await this.task();
        }
    }
}
