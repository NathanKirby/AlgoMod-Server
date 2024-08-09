using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlgoModPatreonServer
{
    internal class Variables
    {
        // Tasks
        public static Task UDPtask = PatreonServer.StartListener();
        public static Task CheckStatus = PatreonServer.Timer();

        // Encryption ids
        public static string MessageKey = "REDACTED";
        public static string MessageIV = "REDACTED";
        public static string IDSKey = "REDACTED";

        // Keys for API
        public static string PatreonSecret = "REDACTED";
        public static string PatreonClient = "REDACTED";
        public static string PatreonAccess = "REDACTED";

        // Variables
        public static string IDScachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "idscache.txt");
        public static string IDSPath = "REDACTED";
        public static string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "serverlog.txt");
        public static string NewIDS = string.Empty;
    }
}
