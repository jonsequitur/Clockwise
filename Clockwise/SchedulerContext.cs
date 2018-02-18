using System;
using System.Threading;
using System.Threading.Tasks;
using Pocket;

namespace Clockwise
{
    public class SchedulerContext : IDisposable
    {
        private readonly IClock clock;

        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private readonly ClockwiseSynchronizationContext synchronizationContext;

        private readonly Budget budget;

        private SchedulerContext(IClock clock, Budget budget = null)
        {
            this.clock = clock ??
                         throw new ArgumentNullException(nameof(clock));

            this.budget = budget ??
                          new Budget();

            var parentSynchronizationContext = SynchronizationContext.Current;

            synchronizationContext = new ClockwiseSynchronizationContext(clock, this.budget);

            SynchronizationContext.SetSynchronizationContext(synchronizationContext);

            disposables.Add(() =>
            {
                SynchronizationContext.SetSynchronizationContext(parentSynchronizationContext);
            });

            disposables.Add(synchronizationContext);
        }

        public static SchedulerContext Establish(Budget budget = null) =>
            new SchedulerContext(
                Clock.Current,
                budget);

        public void Dispose() => disposables.Dispose();

        public static void Run(Func<Task> func, Budget budget = null)
        {
            var previousContext = SynchronizationContext.Current;

            try
            {
                var syncCtx = new ClockwiseSynchronizationContext(
                    Clock.Current,
                    budget ?? new Budget());

                SynchronizationContext.SetSynchronizationContext(syncCtx);

                var t = func();

                t.ContinueWith(
                    delegate
                    {
                        syncCtx.Dispose();
                    }, TaskScheduler.Default);

                t.WaitAndUnwrapException();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }
    }
}