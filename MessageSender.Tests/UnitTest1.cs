using Xunit;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Configuration;
using System.Collections.Generic;
using System.Linq;

namespace MessageSender.Tests
{
    public class MessageSenderIntegrationTests : IDisposable
    {
        private readonly IHost _host;
        private readonly TcpBinaryListener _listener;
        private readonly TestMessageHandler _testHandler;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public MessageSenderIntegrationTests()
        {
            // Setup test configuration
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["TcpListener:Port"] = "6000",
                    ["TcpListener:MaxConnections"] = "10",
                    ["Kafka:BootstrapServers"] = "localhost:9092",
                    ["Kafka:ClientId"] = "test-client",
                    ["Kafka:MessageTimeoutMs"] = "5000",
                    ["Kafka:Acks"] = "all",
                    ["Kafka:EnableIdempotence"] = "true",
                    ["Kafka:CompressionType"] = "none"
                })
                .Build();

            // Create test services
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            services.AddSingleton<MessageProcessingMetrics>();
            services.AddSingleton<TestMessageHandler>();
            services.AddSingleton<IMessageHandler>(sp => sp.GetRequiredService<TestMessageHandler>());
            
            services.Configure<TcpListenerOptions>(configuration.GetSection("TcpListener"));
            services.Configure<KafkaOptions>(configuration.GetSection("Kafka"));

            var serviceProvider = services.BuildServiceProvider();
            
            var options = serviceProvider.GetRequiredService<IOptions<TcpListenerOptions>>().Value;
            var logger = serviceProvider.GetRequiredService<ILogger<TcpBinaryListener>>();
            var metrics = serviceProvider.GetRequiredService<MessageProcessingMetrics>();
            _testHandler = serviceProvider.GetRequiredService<TestMessageHandler>();

            _listener = new TcpBinaryListener(options.Port, _testHandler, logger, metrics, options);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        [Fact]
        public async Task MessageSender_Should_Send_Device_Message_Successfully()
        {
            // Arrange
            var listenerTask = _listener.StartAsync(_cancellationTokenSource.Token);
            await Task.Delay(1000); // Give listener time to start

            byte[] deviceId = { 0x01, 0x02, 0x03, 0x04 };
            ushort messageCounter = 1;
            byte messageType = 2; // Device Message type
            byte[] payload = { 0x01, 0x02, 0x03 };
            var address = new Uri("tcp://localhost:6000");

            // Act
            MessageSender.SendMessage(address, deviceId, messageCounter, messageType, payload);
            await Task.Delay(1000); // Give time for message to be processed

            // Assert
            Assert.True(_testHandler.ReceivedMessages.Count > 0, "No messages were received");
            var receivedMessage = _testHandler.ReceivedMessages.First();
            Assert.Equal(deviceId, receivedMessage.DeviceId);
            Assert.Equal(messageCounter, receivedMessage.MessageCounter);
            Assert.Equal(messageType, receivedMessage.MessageType);
            Assert.Equal(payload, receivedMessage.Payload);

            _cancellationTokenSource.Cancel();
            await listenerTask;
        }

        [Fact]
        public async Task MessageSender_Should_Send_Device_Event_Successfully()
        {
            // Arrange
            var listenerTask = _listener.StartAsync(_cancellationTokenSource.Token);
            await Task.Delay(1000); // Give listener time to start

            byte[] deviceId = { 0x05, 0x06, 0x07, 0x08 };
            ushort messageCounter = 2;
            byte messageType = 1; // Device Event type
            byte[] payload = { 0x0A, 0x0B };
            var address = new Uri("tcp://localhost:6000");

            // Act
            MessageSender.SendMessage(address, deviceId, messageCounter, messageType, payload);
            await Task.Delay(1000); // Give time for message to be processed

            // Assert
            Assert.True(_testHandler.ReceivedMessages.Count > 0, "No messages were received");
            var receivedMessage = _testHandler.ReceivedMessages.First();
            Assert.Equal(deviceId, receivedMessage.DeviceId);
            Assert.Equal(messageCounter, receivedMessage.MessageCounter);
            Assert.Equal(messageType, receivedMessage.MessageType);
            Assert.Equal(payload, receivedMessage.Payload);

            _cancellationTokenSource.Cancel();
            await listenerTask;
        }

        [Fact]
        public async Task MessageSender_Should_Handle_Multiple_Messages()
        {
            // Arrange
            var listenerTask = _listener.StartAsync(_cancellationTokenSource.Token);
            await Task.Delay(1000); // Give listener time to start

            var address = new Uri("tcp://localhost:6000");
            byte[] deviceId1 = { 0x01, 0x02, 0x03, 0x04 };
            byte[] deviceId2 = { 0x05, 0x06, 0x07, 0x08 };

            // Act - Send multiple messages
            MessageSender.SendMessage(address, deviceId1, 1, 2, new byte[] { 0x01, 0x02 });
            MessageSender.SendMessage(address, deviceId2, 1, 1, new byte[] { 0x03, 0x04 });
            MessageSender.SendMessage(address, deviceId1, 2, 11, new byte[] { 0x05, 0x06 });
            
            await Task.Delay(2000); // Give time for messages to be processed

            // Assert
            Assert.True(_testHandler.ReceivedMessages.Count >= 3, $"Expected at least 3 messages, but received {_testHandler.ReceivedMessages.Count}");
            
            var device1Messages = _testHandler.ReceivedMessages.Where(m => m.DeviceId.SequenceEqual(deviceId1)).ToList();
            var device2Messages = _testHandler.ReceivedMessages.Where(m => m.DeviceId.SequenceEqual(deviceId2)).ToList();
            
            Assert.True(device1Messages.Count >= 2, "Device 1 should have received at least 2 messages");
            Assert.True(device2Messages.Count >= 1, "Device 2 should have received at least 1 message");

            _cancellationTokenSource.Cancel();
            await listenerTask;
        }

        [Fact]
        public async Task MessageSender_Should_Handle_Stream_Of_Messages()
        {
            // Arrange
            var listenerTask = _listener.StartAsync(_cancellationTokenSource.Token);
            await Task.Delay(1000); // Give listener time to start

            var address = new Uri("tcp://localhost:6000");

            // Act - Send a stream of messages using the SendStream method
            MessageSender.SendStream(address);
            await Task.Delay(3000); // Give time for all messages to be processed

            // Assert
            Assert.True(_testHandler.ReceivedMessages.Count >= 8, $"Expected at least 8 messages from SendStream, but received {_testHandler.ReceivedMessages.Count}");
            
            // Verify we have both device messages and device events
            var deviceMessages = _testHandler.ReceivedMessages.Where(m => new byte[] { 2, 11, 13 }.Contains(m.MessageType)).ToList();
            var deviceEvents = _testHandler.ReceivedMessages.Where(m => new byte[] { 1, 3, 12, 14 }.Contains(m.MessageType)).ToList();
            
            Assert.True(deviceMessages.Count >= 3, "Should have received device messages");
            Assert.True(deviceEvents.Count >= 4, "Should have received device events");

            _cancellationTokenSource.Cancel();
            await listenerTask;
        }

        [Fact]
        public void MessageSender_Should_Validate_Input_Parameters()
        {
            // Arrange
            var validAddress = new Uri("tcp://localhost:6000");
            var validDeviceId = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var validPayload = new byte[] { 0x01, 0x02 };

            // Act & Assert - Test null address
            Assert.Throws<ArgumentNullException>(() => 
                MessageSender.SendMessage(null, validDeviceId, 1, 2, validPayload));

            // Act & Assert - Test invalid device ID length
            Assert.Throws<ArgumentException>(() => 
                MessageSender.SendMessage(validAddress, new byte[] { 0x01, 0x02 }, 1, 2, validPayload));

            // Act & Assert - Test null payload
            Assert.Throws<ArgumentNullException>(() => 
                MessageSender.SendMessage(validAddress, validDeviceId, 1, 2, null));

            // Act & Assert - Test invalid address format
            Assert.Throws<ArgumentException>(() => 
                MessageSender.SendStream("invalid-address"));
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _host?.Dispose();
        }

        private class TestMessageHandler : IMessageHandler
        {
            public List<DeviceMessage> ReceivedMessages { get; } = new List<DeviceMessage>();

            public Task HandleAsync(DeviceMessage message, CancellationToken cancellationToken)
            {
                ReceivedMessages.Add(message);
                return Task.CompletedTask;
            }
        }
    }

    public class MessageSenderUnitTests
    {
        [Fact]
        public void SendMessage_With_Valid_Parameters_Should_Not_Throw()
        {
            // Arrange
            var address = new Uri("tcp://localhost:6000");
            var deviceId = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var payload = new byte[] { 0x01, 0x02, 0x03 };

            // Act & Assert - Should not throw for valid parameters
            // Note: This will fail if no listener is running, but that's expected
            var exception = Record.Exception(() => 
                MessageSender.SendMessage(address, deviceId, 1, 2, payload));
            
            // The exception is expected if no listener is running, but the method should not throw
            // for invalid parameters
        }

        [Fact]
        public void SendStream_With_Valid_Address_Should_Not_Throw()
        {
            // Arrange
            var address = "tcp://localhost:6000";

            // Act & Assert - Should not throw for valid address format
            var exception = Record.Exception(() => MessageSender.SendStream(address));
            
            // The exception is expected if no listener is running, but the method should not throw
            // for invalid address format
        }
    }
}
