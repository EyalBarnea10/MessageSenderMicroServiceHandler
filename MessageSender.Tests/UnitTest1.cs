using Xunit;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace MessageSender.Tests
{

    public class TcpBinaryListenerTests
    {
        [Fact]
        public async Task Listener_Receives_Message_From_MessageSender()
        {
            // Arrange
            int port = 6000;
            var handlerMock = new TestMessageHandler();
            var loggerMock = new TestLogger();
            var listener = new TcpBinaryListener(port, handlerMock, loggerMock);
            var cts = new CancellationTokenSource();
            var listenerTask = listener.StartAsync(cts.Token);
            await Task.Delay(500); // Give listener time to start

            // Act
            byte[] deviceId = { 0x01, 0x02, 0x03, 0x04 };
            ushort messageCounter = 1;
            byte messageType = 0x01;
            byte[] payload = { 0x01, 0x02, 0x03 };
            var address = new Uri($"tcp://localhost:{port}");
            
            // Use modern API with DeviceMessageRequest
            var message = new DeviceMessageRequest(deviceId, messageCounter, messageType, payload);
            await MessageSender.SendMessageAsync(address, message);
            await Task.Delay(500); // Give time for message to be processed
            cts.Cancel();
            await listenerTask;

            // Assert
            Assert.True(handlerMock.Received);
            Assert.NotNull(handlerMock.LastMessage);
            Assert.Equal(deviceId, handlerMock.LastMessage.DeviceId);
            Assert.Equal(messageCounter, handlerMock.LastMessage.MessageCounter);
            Assert.Equal(messageType, handlerMock.LastMessage.MessageType);
            Assert.Equal(payload, handlerMock.LastMessage.Payload);
        }

        private class TestMessageHandler : IMessageHandler
        {
            public bool Received { get; private set; }
            public DeviceMessage LastMessage { get; private set; }
            public Task HandleAsync(DeviceMessage message, CancellationToken cancellationToken)
            {
                Received = true;
                LastMessage = message;
                return Task.CompletedTask;
            }
        }

        private class TestLogger : ILogger<TcpBinaryListener>
        {
            public IDisposable BeginScope<TState>(TState state) => null;
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel,  EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) { }
        }
    }


    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {

        }
    }
}
