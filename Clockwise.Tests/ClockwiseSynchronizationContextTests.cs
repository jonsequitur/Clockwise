using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FluentAssertions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pocket;
using Xunit;
using Xunit.Abstractions;
using static System.Threading.Thread;
using static Pocket.Logger<Clockwise.Tests.ClockwiseSynchronizationContextTests>;

namespace Clockwise.Tests
{
    public class ClockwiseSynchronizationContextTests : IDisposable
    {
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        public ClockwiseSynchronizationContextTests(ITestOutputHelper output)
        {
            disposables.Add(LogEvents.Subscribe(e => output.WriteLine(e.ToLogString())));
            Log.Info("Starting test on thread {id}", CurrentThread.ManagedThreadId);
        }

        public void Dispose() => disposables.Dispose();

        [Fact]
        public async Task Tasks_queued_to_the_synchronization_context_do_not_leak_to_other_threads()
        {
            var threadIds = new ConcurrentBag<int>();

            using (SchedulerContext.Establish())
            {
                await RunTasksAndCollectThreadIds(threadIds);
            }

            threadIds.Distinct().Count().Should().Be(1);
        }

        [Fact]
        public void Tasks_executed_using_SchedulerContext_Run_do_not_leak_to_other_threads()
        {
            var threadIds = new ConcurrentBag<int>();

            SchedulerContext.Run(() => RunTasksAndCollectThreadIds(threadIds));

            threadIds.Distinct().Count().Should().Be(1);
        }

        [Fact]
        public async Task Tasks_queued_on_other_threads_do_not_leak_to_the_synchronization_context()
        {
            var synchronizedThreadIds = new ConcurrentBag<int>();
            var unsynchronizedThreadIds = new ConcurrentBag<int>();

            var synchronized = Task.Run(async () =>
            {
                using (SchedulerContext.Establish())
                {
                    await RunTasksAndCollectThreadIds(synchronizedThreadIds);
                }
            });

            await Task.WhenAll(
                synchronized,
                RunTasksAndCollectThreadIds(unsynchronizedThreadIds),
                RunTasksAndCollectThreadIds(unsynchronizedThreadIds),
                RunTasksAndCollectThreadIds(unsynchronizedThreadIds));

            synchronizedThreadIds.Should()
                                 .NotContain(unsynchronizedThreadIds);
        }

        [Fact]
        public async Task Return_values_of_executed_tasks_are_correctly_returned()
        {
            using (SchedulerContext.Establish())
            {
                (string value, int threadId)[] ts =
                    await Task.WhenAll(
                        RunTask(() => ( "one", CurrentThread.ManagedThreadId )),
                        RunTask(() => ( "two", CurrentThread.ManagedThreadId )),
                        RunTask(() => ( "three", CurrentThread.ManagedThreadId )),
                        RunTask(() => ( "four", CurrentThread.ManagedThreadId )));

                ts.Select(t => t.value).Should().BeEquivalentTo("one", "two", "three", "four");
                ts.Select(t => t.threadId).Distinct().Count().Should().Be(1);
            }
        }

        [Fact]
        public void Exceptions_from_executed_tasks_are_correctly_propagated()
        {
            using (SchedulerContext.Establish())
            {
                Func<Task<(string value, int threadId)[]>> doWork = async () =>
                    await Task.WhenAll(
                        RunTask(() => ( "one", CurrentThread.ManagedThreadId )),
                        RunTask(() => ( "two", CurrentThread.ManagedThreadId )),
                        RunTask(() =>
                        {
                            throw new DataMisalignedException();
                            return ( "three", CurrentThread.ManagedThreadId );
                        }),
                        RunTask(() => ( "four", CurrentThread.ManagedThreadId )));

                doWork.ShouldThrow<DataMisalignedException>();
            }
        }

        [Fact]
        public async Task The_scheduler_stops_processing_work_after_its_budget_is_exceeded()
        {
            var checkpoints = new List<string>();
            var budget = new Budget();

            var t = Task.Run(async () =>
            {
                using (SchedulerContext.Establish(budget))
                {
                    budget.Cancel();

                    checkpoints.Add("one");

                    await Task.Yield();

                    checkpoints.Add("two");
                }
            });

            await Task.Delay(100);

            t.IsCompleted.Should().BeFalse();
            checkpoints.Should().BeEquivalentTo("one");
        }

        [Fact]
        public void The_synchronization_context_does_not_run_new_tasks_after_it_is_disposed()
        {
            SynchronizationContext synchronizationContext;

            using (SchedulerContext.Establish())
            {
                synchronizationContext = SynchronizationContext.Current;
            }

            var ran = false;

            Action post = () => synchronizationContext.Post(_ =>
            {
                ran = true;
            }, null);

            post.ShouldThrow<ObjectDisposedException>();

            ran.Should().BeFalse();
        }

        private static async Task<T> RunTask<T>(Func<T> func)
        {
            await Task.Yield();

            return func();
        }

        private static async Task RunTasksAndCollectThreadIds(ConcurrentBag<int> threadIds)
        {
            await Task.Yield();

            using (var operation = Log.OnEnterAndExit())
            {
                operation.Event("one");

                threadIds.Add(CurrentThread.ManagedThreadId);

                await Task.Delay(20);

                operation.Event("two");

                threadIds.Add(CurrentThread.ManagedThreadId);

                await Task.WhenAll(
                    CreateSubtask(),
                    CreateSubtask(),
                    CreateSubtask());

                async Task CreateSubtask()
                {
                    threadIds.Add(CurrentThread.ManagedThreadId);

                    operation.Event("three");

                    await Task.Delay(20);

                    operation.Event("four");

                    threadIds.Add(CurrentThread.ManagedThreadId);
                }
            }
        }
    }
}
