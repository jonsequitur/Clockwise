using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Clockwise.Tests
{
    public class ClockTests
    {
        [Fact]
        public void By_default_Clock_Now_returns_the_current_system_time()
        {
            Clock.Now().Should().BeCloseTo(DateTimeOffset.UtcNow);
        }

        [Fact]
        public async Task When_the_clock_is_overridden_it_is_async_context_specific()
        {
            var barrier = new Barrier(2);

            DateTimeOffset actualTimeInTask1;
            DateTimeOffset expectedTimeInTask1 = DateTimeOffset.MinValue;

            DateTimeOffset actualTimeInTask2;
            DateTimeOffset expectedTimeInTask2 = DateTimeOffset.MaxValue;

            await Task.Run(() =>
            {
                Clock.Current = new FrozenClock(expectedTimeInTask1);
                barrier.SignalAndWait(1000);
                actualTimeInTask1 = Clock.Now();
            });

            await Task.Run(() =>
            {
                Clock.Current = new FrozenClock(expectedTimeInTask2);
                barrier.SignalAndWait(1000);
                actualTimeInTask2 = Clock.Now();
            });

            actualTimeInTask1.Should().Be(expectedTimeInTask1);
            actualTimeInTask2.Should().Be(expectedTimeInTask2);
        }

        public class FrozenClock : IClock
        {
            private readonly DateTimeOffset now;

            public FrozenClock(DateTimeOffset now)
            {
                this.now = now;
            }

            public DateTimeOffset Now()
            {
                return now;
            }

            public void Schedule(Action<IClock> action, DateTimeOffset? after = null)
            {
            }

            public void Schedule(Func<IClock, Task> action, DateTimeOffset? after = null)
            {
            }
        }
    }
}
