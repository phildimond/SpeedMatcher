using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime;
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

        public bool IsOpen { get { if (_SprogPort != null) { return _SprogPort.IsOpen; } else { return false; } } }

        public static string ForwardSpeedCommand(byte speed) { return $"> {speed.ToString()}\r"; }
        public static string ReverseSpeedCommand(byte speed) { return $"< {speed.ToString()}\r"; }
        public static string ReadCvDirectBitCommand(byte cv) { return $"C {cv.ToString()}\r"; }
        public static string WriteCvDirectBitCommand(byte cv, byte value) { return $"C {cv.ToString()} {value.ToString()}\r"; }
        public static string ReadCvPagedCommand(byte cv) { return $"V {cv.ToString()}\r"; }
        public static string WriteCvPagedCommand(byte cv, byte value) { return $"V {cv.ToString()} {value.ToString()}\r"; }

        public event LogEventDelegate LogMessageAvailable;

        public SprogII(string portname)
        { 
            _SprogPort = _SprogPort = new SerialPort(portname, 9600, Parity.None, 8, StopBits.One);
            _SprogPort.Open();
        }

        public void Close()
        {
            if (_SprogPort != null) { _SprogPort.Close(); }
            else { throw new NullReferenceException("Port is null"); }
        }

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
                            //LogMessage($"There are {i} bytes available on the serial port");
                            _SprogPort.Read(buffer, 0, i);
                            s += ASCIIEncoding.ASCII.GetString(buffer, 0, i);
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
