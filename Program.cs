using System;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Security.Cryptography.X509Certificates;

namespace AlgoModPatreonServer
{
    class PatreonServer
    {
        public static void Main(string[] args)
        {
            Task.WaitAll(Variables.UDPtask, Variables.CheckStatus);
        }


        /// <summary>
        /// Runs UDP listener to receive messages from the client.
        /// </summary>
        /// <remarks>Client messages are handled by <see cref="Response.HandleMessage(string)"/> and the resulting status code is sent back to the client.</remarks>
        public static Task StartListener()
        {
            Console.WriteLine("UdpClient Listener started");
            Log("Server Started\n");

            return Task.Run(() =>
            {
                // Port REDACTED
                int port = 0;
                
                string response = string.Empty;

                try
                {
                    UdpClient udpServer = new(port);

                    while (true)
                    {
                        // Stores client endpoint
                        IPEndPoint clientEndPoint = new(IPAddress.Any, 0);

                        // Receives message from client endpoint
                        byte[] receivedBytes = udpServer.Receive(ref clientEndPoint);

                        // Encodes the received message as a string
                        string receivedMessage = Encoding.ASCII.GetString(receivedBytes);

                        if (receivedMessage.Contains("INFO"))
                        {
                            response = Response.HandleMessage(receivedMessage);
                        }
                        else
                        {
                            // Decrypts message
                            string DecyptedMessage = Encryption.DecryptMessage(receivedMessage);

                            // Cleans input
                            string CleanedMessage = CleanString(DecyptedMessage);

                            // Handles the message and returns response
                            response = Response.HandleMessage(CleanedMessage);
                        }

                        // Encodes into bytes
                        byte[] data = Encoding.ASCII.GetBytes(response);

                        // Sends response
                        udpServer.Send(data, data.Length, clientEndPoint);
                    }
                }
                catch (Exception ex)
                {
                    Log($"!!!Error: StartListener: {ex.Message}\n");
                }
            });
        }


        /// <summary>
        /// Runs timed methods on a clock to manage services.
        /// </summary>
        /// <see cref="Service"/>
        public static Task Timer()
        {
            return Task.Run(() =>
            {
                while (true)
                {
                    try
                    {
                        // Only runs if there are ids in there
                        if (!string.IsNullOrEmpty(File.ReadAllText(Variables.IDSPath)))
                        {
                            _ = Service.CheckPatrons();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"!!!Error: Timer: {ex.Message}\n");
                    }

                    // 1 minute
                    Thread.Sleep(60000);
                }
            });
        }


        /// <summary>
        /// Removes open spaces and new lines from the string input.
        /// </summary>
        /// <returns>Returns cleaned string.</returns>
        public static string CleanString(string input)
        {
            return input.Replace("\n", string.Empty).Replace("\r", string.Empty);
        }


        /// <summary>
        /// Logs to a log file on server.
        /// </summary>
        public static void Log(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                {
                    return;
                }

                string time = DateTime.Now.ToString();
                string logContent = string.Empty;
                string logPath = Variables.LogPath;

                if (!File.Exists(logPath))
                {
                    File.WriteAllText(logPath, message);
                    return;
                }
                if (string.IsNullOrEmpty(message))
                {
                    message = "Log invalid format";
                }

                string currnetContent = File.ReadAllText(logPath);

                // Applies new line to server started and error text
                if (message == "Server Started\n" || message.Contains("!!!Error:"))
                {
                    logContent = $"{currnetContent}\n\n{time} EST | {message}";
                }
                else
                {
                    logContent = $"{currnetContent}\n{time} EST | {message}";
                }

                File.Delete(logPath);
                File.WriteAllText(logPath, logContent);
            }
            catch
            {
                // No handle necessary
            }
        }
    }
}