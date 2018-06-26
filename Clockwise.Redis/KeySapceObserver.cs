using System;
using System.Threading.Tasks;
using Pocket;
using StackExchange.Redis;

namespace Clockwise.Redis
{
    public sealed class KeySpaceObserver : IObservable<(string key, string operation)>, IDisposable
    {
        private readonly ISubscriber subscriber;
        private readonly string notificationChannel;
        private readonly ConcurrentSet<IObserver<(string key, string operation)>> observers;
        public KeySpaceObserver(int dbId, string key,  ISubscriber subscriber)
        {
            this.subscriber = subscriber;
            observers = new ConcurrentSet<IObserver<(string key, string operation)>>();
            var keyToSubscribe = string.IsNullOrWhiteSpace(key) ? "*" : key;
            notificationChannel = $"__keyspace@{dbId}__:{keyToSubscribe}";
           
        }

        public async Task Initialise()
        {
            await subscriber.SubscribeAsync(notificationChannel, Handler);
        }


        private void Handler(RedisChannel channel, RedisValue notificationType)
        {
            var key = GetKey(channel);

            foreach (var observer in observers)
            {
                observer.OnNext((key,notificationType));

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
           subscriber.Unsubscribe(notificationChannel, Handler);
            foreach (var observer in observers)
            {
                observer.OnCompleted();
            }
        }

        public IDisposable Subscribe(IObserver<(string key, string operation)> observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            observers.TryAdd(observer);
            return Disposable.Create(() => { observers.TryRemove(observer); });
        }
    }
}