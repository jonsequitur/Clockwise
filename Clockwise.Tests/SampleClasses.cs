using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Clockwise.Tests
{
    public class CommandTarget
    {
        public CommandTarget(string id)
        {
            Id = id;
        }

        public string Id { get; }

        public IList<ICommand> ReceivedCommands { get; } = new List<ICommand>();
    }

    public interface ICommand
    {
    }

    public interface ICommand<in T> : ICommand
    {
        string TargetId { get; }

        Task ApplyTo(T target);
    }

    public class CreateCommandTarget : ICommand<CommandTarget>, IIdempotent
    {
        public CreateCommandTarget(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(targetId));
            }

            TargetId = targetId;

            IdempotencyToken = targetId;
        }

        public string TargetId { get; }

        public async Task ApplyTo(CommandTarget target)
        {
            target.ReceivedCommands.Add(this);
        }

        public string IdempotencyToken { get; }
    }

    public class UpdateCommandTarget : ICommand<CommandTarget>
    {
        public string TargetId { get; }

        public async Task ApplyTo(CommandTarget target)
        {
            target.ReceivedCommands.Add(this);
        }
    }

    public class CreateCommandTargetHandler : ICommandHandler<CreateCommandTarget>
    {
        private readonly IStore<CommandTarget> store;

        public CreateCommandTargetHandler(IStore<CommandTarget> store)
        {
            this.store = store ??
                         throw new ArgumentNullException(nameof(store));
        }

        public async Task<ICommandDeliveryResult> Handle(ICommandDelivery<CreateCommandTarget> delivery)
        {
            await store.Put(new CommandTarget(delivery.Command.TargetId));

            return delivery.Complete();
        }
    }

    public class UpdateCommandTargetHandler : ICommandHandler<UpdateCommandTarget>
    {
        private readonly IStore<CommandTarget> store;

        public UpdateCommandTargetHandler(IStore<CommandTarget> store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public async Task<ICommandDeliveryResult> Handle(ICommandDelivery<UpdateCommandTarget> delivery)
        {
            var commandTarget = await store.Get(delivery.Command.TargetId);

            await delivery.Command.ApplyTo(commandTarget);

            await store.Put(commandTarget);

            return delivery.Complete();
        }
    }

    public interface IStore<T>
        where T : class
    {
        Task<T> Get(string id);

        Task Put(T aggregate);
    }

    public class InMemoryStore<T> : IStore<T>, IEnumerable<T>
        where T : class
    {
        private readonly ConcurrentDictionary<string, T> dictionary = new ConcurrentDictionary<string, T>();

        private readonly Func<T, string> getId;
        private readonly Func<string, T> create;

        public InMemoryStore(Func<T, string> getId = null, Func<string, T> create = null)
        {
            this.create = create;

            if (getId != null)
            {
                this.getId = getId;
            }
            else
            {
                this.getId = t => ((dynamic) t).Id;
            }
        }

        public Task<T> Get(string id)
        {
            T value;

            dictionary.TryGetValue(id, out value);

            if (value == null && create != null)
            {
                value = create(id);
            }

            return Task.FromResult(value);
        }

        public Task Put(T value)
        {
            dictionary.TryAdd(getId(value), value);

            return Task.CompletedTask;
        }

        public void Add(T value) => Put(value);

        public IEnumerator<T> GetEnumerator() => dictionary.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
