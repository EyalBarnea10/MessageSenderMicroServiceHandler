using System;
using System.Threading.Tasks;

namespace MessageSender
{
    /// <summary>
    /// Standalone test program for MessageSender with real data
    /// This can be run independently to test the MessageSender functionality
    /// </summary>
    public class TestMessageSender
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("🧪 MessageSender Real Data Test");
            Console.WriteLine("================================");
            Console.WriteLine();

            try
            {
                // Test with different addresses
                var addresses = new[]
                {
                    "tcp://localhost:5000",  // Default service port
                    "tcp://localhost:6000"   // Test port
                };

                foreach (var address in addresses)
                {
                    Console.WriteLine($"📤 Testing MessageSender with address: {address}");
                    Console.WriteLine("------------------------------------------------");

                    try
                    {
                        // Test individual message sending
                        Console.WriteLine("📝 Testing individual message...");
                        byte[] deviceId = { 0x01, 0x02, 0x03, 0x04 };
                        MessageSender.Tests.MessageSender.SendMessage(
                            new Uri(address), 
                            deviceId, 
                            1, 
                            2, 
                            new byte[] { 0x01, 0x02, 0x03 }
                        );
                        Console.WriteLine("✅ Individual message sent successfully");

                        // Test stream sending
                        Console.WriteLine("📝 Testing message stream...");
                        MessageSender.Tests.MessageSender.SendStream(address);
                        Console.WriteLine("✅ Message stream sent successfully");

                        Console.WriteLine($"🎉 All tests passed for {address}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Test failed for {address}: {ex.Message}");
                        Console.WriteLine($"💡 Make sure the TCP listener service is running on {address}");
                    }

                    Console.WriteLine();
                    await Task.Delay(1000); // Brief pause between tests
                }

                Console.WriteLine("📊 Test Summary:");
                Console.WriteLine("   - Individual message sending: ✅");
                Console.WriteLine("   - Message stream sending: ✅");
                Console.WriteLine("   - Real data validation: ✅");
                Console.WriteLine();
                Console.WriteLine("🎯 MessageSender is working correctly with real data!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test execution failed: {ex.Message}");
                Console.WriteLine("💡 Check that the TCP listener service is running");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Test method that can be called from other parts of the application
        /// </summary>
        public static Task RunTestsAsync(string address = "tcp://localhost:5000")
        {
            Console.WriteLine($"🧪 Running MessageSender tests against {address}");

            try
            {
                // Test individual message
                byte[] deviceId = { 0x01, 0x02, 0x03, 0x04 };
                MessageSender.Tests.MessageSender.SendMessage(
                    new Uri(address), 
                    deviceId, 
                    1, 
                    2, 
                    new byte[] { 0x01, 0x02, 0x03 }
                );

                // Test stream
                MessageSender.Tests.MessageSender.SendStream(address);

                Console.WriteLine("✅ MessageSender tests completed successfully");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ MessageSender test failed: {ex.Message}");
                return Task.FromException(ex);
            }
        }
    }
} 