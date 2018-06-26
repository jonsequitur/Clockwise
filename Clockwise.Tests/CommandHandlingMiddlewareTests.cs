using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Pocket;
using Xunit;
using static Pocket.Logger<Clockwise.Tests.CommandHandlingMiddlewareTests>;

namespace Clockwise.Tests
{
    public class CommandHandlingMiddlewareTests
    {
        [Fact]
        public async Task Middleware_can_be_used_to_log_commands_as_they_are_delivered()
        {
            var handler = CommandHandler.Create<object>(delivery =>
                                        {
                                        })
                                        .UseMiddleware(async (delivery, next) =>
                                        {
                                            using (Log.ConfirmOnExit())
                                            {
                                                return await next(delivery);
                                            }
                                        });

            var log = new List<string>();

            using (LogEvents.Subscribe(e => log.Add(e.ToLogString())))
            {
                await handler.Handle(new CommandDelivery<object>(new object()));
            }

            log.Should().ContainSingle(e => e.Contains(nameof(Middleware_can_be_used_to_log_commands_as_they_are_delivered)));
        }
    }
}
