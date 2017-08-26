using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Pocket;
using Xunit;
using static Pocket.Logger<Clockwise.Tests.CommandSchedulingMiddlewareTests>;

namespace Clockwise.Tests
{
    public class CommandSchedulingMiddlewareTests
    {
        [Fact]
        public async Task Middleware_can_be_used_to_log_commands_as_they_are_scheduled()
        {
            var scheduler = CommandScheduler.Create<object>(delivery =>
                                            {
                                            })
                                            .UseMiddleware(async (delivery, next) =>
                                            {
                                                using (Log.ConfirmOnExit())
                                                {
                                                    await next(delivery);
                                                }
                                            });

            var log = new List<string>();

            using (LogEvents.Subscribe(e => log.Add(e.ToLogString())))
            {
                await scheduler.Schedule(new object());
            }

            log.Should().ContainSingle(e => e.Contains(nameof(Middleware_can_be_used_to_log_commands_as_they_are_scheduled)));
        }
    }
}
