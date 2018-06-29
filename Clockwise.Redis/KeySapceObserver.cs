using System;
using System.Threading.Tasks;
using Pocket;
using StackExchange.Redis;

namespace Clockwise.Redis
{
    internal delegate void KeySpaceNotificationHandler(string key, string operation);

    internal class KeySpaceObserver : IDisposable
    {
        private readonly ISubscriber subscriber;
        private readonly string notificationChannel;
        private readonly ConcurrentSet<KeySpaceNotificationHandler> handlers;
        public KeySpaceObserver(int dbId, string key, ISubscriber subscriber)
        {
            this.subscriber = subscriber;
            handlers = new ConcurrentSet<KeySpaceNotificationHandler>();
            var keyToSubscribe = string.IsNullOrWhiteSpace(key) ? "*" : key;
            notificationChannel = $"__keyspace@{dbId}__:{keyToSubscribe}";
        }

        public async Task Initialize()
        {
            await subscriber.SubscribeAsync(notificationChannel, NotificationHandler);
        }

        private void NotificationHandler(RedisChannel channel, RedisValue notificationType)
        {
            var key = GetKey(channel);

            foreach (var observer in handlers)
            {
                observer(key, notificationType);

            }
        }

        private static string GetKey(string channel)
        {
            var index = channel.IndexOf(':');
            if (index >= 0 && index < channel.Length - 1)
            {
                return channel.Substring(index + 1);
            }

            return channel;
        }
        public void Dispose()
        {
            subscriber.Unsubscribe(notificationChannel, NotificationHandler);
            handlers.Clear();
        }

        public IDisposable Subscribe(KeySpaceNotificationHandler notificationHandler)
        {
            if (notificationHandler == null) throw new ArgumentNullException(nameof(notificationHandler));
            handlers.TryAdd(notificationHandler);
            return Disposable.Create(() => { handlers.TryRemove(notificationHandler); });
        }
    }
}