﻿using System;
using System.Threading.Tasks;
using FluentAssertions;
using StackExchange.Redis;
using Xunit;

namespace Clockwise.Redis.Tests
{
    public class CircuitBreakerStorageTests : IDisposable
    {
        [Fact]
        public void SingalingState()
        {
            var cb01 = CircuitBreakerStorage.Create<string>("127.0.0.1");
            cb01.GetState().State.Should().Be(CircuitBreakerState.Closed);
            cb01.SetState(CircuitBreakerState.HalfOpen);
            Task.Delay(100).Wait();
            cb01.GetState().State.Should().Be(CircuitBreakerState.HalfOpen);
        }

        public void Dispose()
        {
          var connection =  ConnectionMultiplexer.Connect("127.0.0.1");
            connection.GetDatabase().Execute("FLUSHALL");
        }
    }
}