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

        private readonly List<CommandHandler<T>> subscribers = new List<CommandHandler<T>>();

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
            CommandHandler<T> handle,
            TimeSpan? timeout = null)
        {
            timeout = timeout ??
                      Settings.For<T>.Default.ReceiveTimeout;

            var stopAt = clock.Now() + timeout;

            ICommandDeliveryResult result = null;

            using (Subscribe(async delivery =>
            {
                result = await handle(delivery);
                return result;
            }))
            {
                while (result == null &&
                       clock.Now() <= stopAt)
                {
                    var timeUntilNextActionIsDue = clock.TimeUntilNextActionIsDue;

                    if (timeUntilNextActionIsDue == null)
                    {
                        return null;
                    }

                    await clock.Wait(timeUntilNextActionIsDue.Value);
                }
            }

            return result;
        }

        public IDisposable Subscribe(CommandHandler<T> handle)
        {
            lock (subscribers)
            {
                if (isDisposed)
                {
                    ThrowObjectDisposed();
                }

                subscribers.Add(handle);
            }

            return Disposable.Create(() =>
            {
                lock (subscribers)
                {
                    subscribers.Remove(handle);
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

        private CommandHandler<T>[] GetReceivers()
        {
            CommandHandler<T>[] receivers;

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
