using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pocket;

namespace Clockwise
{
    public class InMemoryCommandBus<T> :
        ICommandBus<T>,
        IDisposable
    {
        private readonly VirtualClock clock;

        private readonly List<Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>>> subscribers = new List<Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>>>();

        private readonly ConcurrentDictionary<string, CommandDelivery<T>> pendingDeliveries = new ConcurrentDictionary<string, CommandDelivery<T>>();

        private readonly ConcurrentSet<string> scheduledIdempotencyTokens = new ConcurrentSet<string>();

        private bool isDisposed;

        public InMemoryCommandBus(VirtualClock clock)
        {
            this.clock = clock;
        }

        public async Task Schedule(CommandDelivery<T> item)
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

        public async Task<CommandDeliveryResult<T>> Receive(
            Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>> handle,
            TimeSpan? timeout = null)
        {
            timeout = timeout ??
                      Configuration.For<T>.Default.ReceiveTimeout;

            var howFarToAdvance = ShorterOf(
                timeout,
                clock.TimeUntilNextActionIsDue);

            CommandDeliveryResult<T> result = null;

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

        public IDisposable Subscribe(Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>> onNext)
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

        public IEnumerable<CommandDelivery<T>> Undelivered() => pendingDeliveries.Values;

        private async Task Publish(CommandDelivery<T> item)
        {
            var receivers = GetReceivers();

            if (receivers.Length == 0)
            {
                clock.Schedule(async s => await Publish(item),
                               after: TimeSpan.FromSeconds(1));
                return;
            }

            foreach (var receiver in receivers)
            {
                var result = await receiver.Invoke(item);

                switch (result)
                {
                    case RetryDeliveryResult<T> retry:
                        clock.Schedule(async s => await Publish(item),
                                       result.Delivery.DueTime);
                        break;

                    case CancelDeliveryResult<T> _:
                    case CompleteDeliveryResult<T> _:
                        pendingDeliveries.TryRemove(item.IdempotencyToken, out var _);
                        scheduledIdempotencyTokens.TryAdd(item.IdempotencyToken);
                        break;
                }
            }
        }

        private Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>>[] GetReceivers()
        {
            Func<CommandDelivery<T>, Task<CommandDeliveryResult<T>>>[] receivers;

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
