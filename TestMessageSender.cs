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
                    

                    try
                    {
                        // Test individual message sending
            
                        byte[] deviceId = { 0x01, 0x02, 0x03, 0x04 };
                        MessageSender.Tests.MessageSender.SendMessage(
                            new Uri(address), 
                            deviceId, 
                            1, 
                            2, 
                            new byte[] { 0x01, 0x02, 0x03 }
                        );
            

                        // Test stream sending
            
                        MessageSender.Tests.MessageSender.SendStream(address);
            

            
                    }
                    catch (Exception ex)
                    {
                        
                    }

                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Test method that can be called from other parts of the application
        /// </summary>
        public static Task RunTestsAsync(string address = "tcp://localhost:5000")
        {
            try
            {
                byte[] deviceId = { 0x01, 0x02, 0x03, 0x04 };
                MessageSender.Tests.MessageSender.SendMessage(
                    new Uri(address), 
                    deviceId, 
                    1, 
                    2, 
                    new byte[] { 0x01, 0x02, 0x03 }
                );

                MessageSender.Tests.MessageSender.SendStream(address);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }
    }
} 