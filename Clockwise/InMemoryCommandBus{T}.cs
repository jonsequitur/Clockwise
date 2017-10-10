using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pocket;

namespace Clockwise
{
    public class InMemoryCommandBus<T> :
        ICommandReceiver<T>,
        ICommandScheduler<T>,
        IDisposable
    {
        private readonly VirtualClock clock;

        private readonly List<Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>>> subscribers = new List<Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>>>();

        private readonly ConcurrentDictionary<string, ICommandDelivery<T>> pendingDeliveries = new ConcurrentDictionary<string, ICommandDelivery<T>>();

        private readonly ConcurrentSet<string> scheduledIdempotencyTokens = new ConcurrentSet<string>();

        private bool isDisposed;

        public InMemoryCommandBus(VirtualClock clock)
        {
            this.clock = clock;
        }

        public async Task Schedule(ICommandDelivery<T> item)
        {
            await Task.Yield();

            if (!scheduledIdempotencyTokens.TryAdd(item.IdempotencyToken))
            {
                return;
            }

            pendingDeliveries.TryAdd(item.IdempotencyToken, item);

            clock.Schedule(async s => await Publish(item),
                           after: item.DueTime);
        }

        public async Task<ICommandDeliveryResult> Receive(
            Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>> handle,
            TimeSpan? timeout = null)
        {
            timeout = timeout ??
                      Settings.For<T>.Default.ReceiveTimeout;

            var howFarToAdvance = ShorterOf(
                timeout,
                clock.TimeUntilNextActionIsDue);

            ICommandDeliveryResult result = null;

            using (Subscribe(async delivery =>
            {
                result = await handle(delivery);
                return result;
            }))
            {
                await clock.Wait(howFarToAdvance);
            }

            return result;
        }

        public IDisposable Subscribe(Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>> onNext)
        {
            lock (subscribers)
            {
                if (isDisposed)
                {
                    ThrowObjectDisposed();
                }

                subscribers.Add(onNext);
            }

            return Disposable.Create(() =>
            {
                lock (subscribers)
                {
                    subscribers.Remove(onNext);
                }
            });
        }

        public IEnumerable<ICommandDelivery<T>> Undelivered() => pendingDeliveries.Values;

        private async Task Publish(ICommandDelivery<T> item)
        {
            var receivers = GetReceivers();

            if (receivers.Length == 0)
            {
                clock.Schedule(async s => await Publish(item),
                               dueAfter: TimeSpan.FromSeconds(1));
                return;
            }

            foreach (var receiver in receivers)
            {
                var result = await receiver.Invoke(item);

                switch (result)
                {
                    case RetryDeliveryResult<T> retry:
                        clock.Schedule(async s => await Publish(item),
                                       item.DueTime);
                        break;

                    case CancelDeliveryResult<T> _:
                    case CompleteDeliveryResult<T> _:
                        pendingDeliveries.TryRemove(item.IdempotencyToken, out var _);
                        scheduledIdempotencyTokens.TryAdd(item.IdempotencyToken);
                        break;
                }
            }
        }

        private Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>>[] GetReceivers()
        {
            Func<ICommandDelivery<T>, Task<ICommandDeliveryResult>>[] receivers;

            lock (subscribers)
            {
                if (isDisposed)
                {
                    ThrowObjectDisposed();
                }

                receivers = subscribers.ToArray();
            }

            return receivers;
        }

        private static TimeSpan ShorterOf(
            TimeSpan? value1,
            TimeSpan? one) =>
            TimeSpan.FromTicks(
                Math.Min(
                    (value1 ?? TimeSpan.MaxValue).Ticks,
                    (one ?? TimeSpan.MaxValue).Ticks));

        private static void ThrowObjectDisposed()
        {
            throw new ObjectDisposedException($"The {nameof(InMemoryCommandBus<T>)} has been disposed.");
        }

        public void Dispose()
        {
            lock (subscribers)
            {
                subscribers.Clear();
                isDisposed = true;
            }
        }
    }
}
