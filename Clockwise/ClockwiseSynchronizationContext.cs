using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Clockwise
{
    internal sealed class ClockwiseSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly IClock clock;

        private readonly Budget budget;

        private readonly BlockingCollection<WorkItem> queue = new BlockingCollection<WorkItem>();

        public ClockwiseSynchronizationContext(
            IClock clock,
            Budget budget)
        {
            this.clock = clock;
            this.budget = budget;
            var thread = new Thread(Run);

            thread.Start();
        }

        public override void Post(SendOrPostCallback callback, object state)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            var workItem = new WorkItem(callback, state);

            try
            {
                queue.Add(workItem);
            }
            catch (InvalidOperationException)
            {
                throw new ObjectDisposedException($"The {nameof(ClockwiseSynchronizationContext)} has been disposed.");
            }
        }

        public override void Send(SendOrPostCallback callback, object state) =>
            throw new NotSupportedException($"Synchronous Send is not supported by {nameof(ClockwiseSynchronizationContext)}.");

        private void Run()
        {
            SetSynchronizationContext(this);

            foreach (var workItem in queue.GetConsumingEnumerable())
            {
                if (!budget.IsExceeded)
                {
                    workItem.Run();
                }
            }
        }

        public void Dispose() => queue.CompleteAdding();

        private struct WorkItem
        {
            public WorkItem(SendOrPostCallback callback, object state)
            {
                Callback = callback;
                State = state;
            }

            private readonly SendOrPostCallback Callback;

            private readonly object State;

            public void Run() => Callback(State);
        }
    }
}
