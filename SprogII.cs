using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpeedMatcher
{
    public delegate void LogEventDelegate(string message);

    public class SprogII
    {
        private SerialPort? _SprogPort = null;

        public const string PowerOnCommand = "+\r";
        public const string PowerOffCommand = "-\r";
        public const string GetModeCommand = "M\r";
        public const string GetSprogInfoCommand = "?\r";
        public static string ForwardSpeedCommand(byte speed) { return $"> {speed.ToString()}\r"; }
        public static string ReverseSpeedCommand(byte speed) { return $"< {speed.ToString()}\r"; }

        public event LogEventDelegate LogMessageAvailable;

        public SprogII(SerialPort? port) { _SprogPort = port; }
        
        public string SprogTransaction(string command, int timeout = 1000)
        {
            string s = string.Empty;
            if (_SprogPort != null && _SprogPort.IsOpen)
            {
                try
                {
                    _SprogPort.Write(command);
                    DateTime start = DateTime.Now;
                    bool done = false;
                    byte[] buffer = new byte[1024];
                    while ((DateTime.Now - start).TotalMilliseconds < timeout && !done)
                    {
                        Thread.Sleep(100);
                        int i = _SprogPort.BytesToRead;
                        if (i > 0)
                        {
                            _SprogPort.Read(buffer, 0, i);
                            s += ASCIIEncoding.ASCII.GetString(buffer);
                        }
                        if (s.Contains("P>")) { done = true; }
                    }
                    if (s != string.Empty)
                    {
                        s = s.Replace("\r", "<CR>");
                        s = s.Replace("\n", "<LF>");
                    }
                }
                catch (Exception ex) { LogMessage($"Exception getting SPROG port: {ex.Message}"); }
            }
            return s;
        }

        private void LogMessage(string message) { LogMessageAvailable?.Invoke(message); }
    }
}
