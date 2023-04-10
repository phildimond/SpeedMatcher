using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SpeedMatcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Settings? _Settings = null;
        private SerialPort? _SprogPort = null;
        private byte _CurrentSpeed = 0;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Load the settings
            try
            {
                _Settings = Settings.LoadSettings();
            }
            catch (Exception ex) 
            {
                MessageBox.Show($"Failed to load settings. Exception: {ex.Message}");
                _Settings = new Settings();
                try { Settings.SaveSettings(_Settings); } catch { MessageBox.Show($"Failed to save new settings. Exception: {ex.Message}"); }
            }

            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports) { SProgPortSelector.Items.Add(port); }

            // Setup the SPROG port control
            if (_Settings != null && _Settings.SprogPort != string.Empty)
            {
                for (int i = 0; i < SProgPortSelector.Items.Count; i++)
                {
                    if ((String)SProgPortSelector.Items[i] == _Settings.SprogPort) {
                        SProgPortSelector.SelectedIndex = i;
                        break;
                    }
                }
            }

        }

        private void SProgPortSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_Settings != null)
                {
                    _Settings.SprogPort = (string)SProgPortSelector.SelectedItem;
                    Settings.SaveSettings(_Settings);
                }
            } catch (Exception ex) { MessageBox.Show($"Failed to save changed SPROG port. Exception: {ex.Message}"); }
        }

        private void ConnectItem_Click(object sender, RoutedEventArgs e)
        {
            switch (ConnectItem.IsChecked)
            {
                case true: 
                    if (_Settings != null && _Settings.SprogPort != string.Empty)
                    {
                        try
                        {
                            _SprogPort = new SerialPort(_Settings.SprogPort, 9600, Parity.None, 8, StopBits.One);
                            _SprogPort.Open();
                        }
                        catch (Exception ex) { MessageBox.Show($"Exception when trying to open SPROG port: {ex.Message}"); }
                        ConnectItem.Header = "Connected";
                        ConnectItem.IsChecked = true;
                        LogMessage($"SPROG connected on {_Settings.SprogPort}");
                    }
                    break;
                case false:
                    if (_Settings !=  null && _SprogPort != null && _SprogPort.IsOpen)
                    {
                        try
                        {
                            _SprogPort.Close();
                        }
                        catch (Exception ex) { MessageBox.Show($"Exception when trying to open SPROG port: {ex.Message}"); }
                        ConnectItem.IsChecked = false;
                        ConnectItem.Header = "Connect";
                        LogMessage($"SPROG disconnected on {_Settings.SprogPort}");
                    }
                    break;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_SprogPort != null && _SprogPort.IsOpen) { _SprogPort.Close(); }
        }

        private string SprogTransaction(string command, int timeout = 1000)
        {
            string s = string.Empty;
            if (_SprogPort !=  null && _SprogPort.IsOpen) 
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

        private void LogMessage(string s)
        {
            LogLB.Items.Add(s);
            while (LogLB.Items.Count > 100) { LogLB.Items.RemoveAt(0); }
            LogScrollViewer.ScrollToEnd();
        }

        private void SprogInfoButton_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("Sending ?<CR> to SPROG");
            string s = SprogTransaction("?\r", 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage($"Timeout!"); }
        }

        private void SprogGetModeButton_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("Sending M<CR> to SPROG");
            string s = SprogTransaction("M\r", 1000);
            if (s != string.Empty) 
            { 
                LogMessage($"Received {s}");
                s = s.Replace("M=h", String.Empty);
                s = s.Replace("<CR><LF>P>", String.Empty);
                s = s.Replace("\r", String.Empty);
                s = s.Replace("\n", String.Empty);
                s = s.Replace("P>", String.Empty);
                UInt16 i = 0;
                try
                {
                    i = UInt16.Parse(s, System.Globalization.NumberStyles.HexNumber);
                }
                catch (Exception)
                {
                    LogMessage($"ERROR! Could not parse returned value {s} to a 16 bit value!");
                    return;
                }
                UpdateModeControls(i);
            }
            else { LogMessage($"Timeout!"); }          
        }

        private void SprogSetModeButton_Click(object sender, RoutedEventArgs e)
        {
            UInt16 mode = ReadModeControls();
            LogMessage($"Sending M h{mode:X4}<CR> to SPROG");
            string s = SprogTransaction($"M h{mode:X4}\r", 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage($"Timeout!"); }
        }

        private void UpdateModeControls(UInt16 value)
        {
            if ((value & 0x0001) > 0) { ModeUnlockCheckBox.IsChecked = true; } else { ModeUnlockCheckBox.IsChecked = false; }
            if ((value & 0x0002) > 0) { ModeEchoEnabledCheckBox.IsChecked = true; } else { ModeEchoEnabledCheckBox.IsChecked = false; }
            if ((value & 0x0008) > 0) { ModeCalcErrorCheckBox.IsChecked = true; } else { ModeCalcErrorCheckBox.IsChecked = false; }
            if ((value & 0x0010) > 0) { ModeRollingRoadCheckBox.IsChecked = true; } else { ModeRollingRoadCheckBox.IsChecked = false; }
            if ((value & 0x0020) > 0) { ModeZTCCheckBox.IsChecked = true; } else { ModeZTCCheckBox.IsChecked = false; }
            if ((value & 0x0040) > 0) { ModeBlueLineCheckBox.IsChecked = true; } else { ModeBlueLineCheckBox.IsChecked = false; }
            if ((value & 0x0100) > 0) { ModeDirectionCheckBox.IsChecked = true; } else { ModeDirectionCheckBox.IsChecked = false; }
            if ((value & 0x0200) > 0) { ModeSpeed14RadioButton.IsChecked = true; } else { ModeSpeed14RadioButton.IsChecked = false; }
            if ((value & 0x0400) > 0) { ModeSpeed28RadioButton.IsChecked = true; } else { ModeSpeed28RadioButton.IsChecked = false; }
            if ((value & 0x0800) > 0) { ModeSpeed128RadioButton.IsChecked = true; } else { ModeSpeed128RadioButton.IsChecked = false; }
            if ((value & 0x1000) > 0) { ModeLongAddressCheckBox.IsChecked = true; } else { ModeLongAddressCheckBox.IsChecked = false; }
        }

        private UInt16 ReadModeControls()
        {
            UInt16 value = 0;
            if (ModeUnlockCheckBox.IsChecked == true) { value += 0x0001; }
            if (ModeEchoEnabledCheckBox.IsChecked == true) { value += 0x0002; }
            if (ModeCalcErrorCheckBox.IsChecked == true) { value += 0x0008; }
            if (ModeRollingRoadCheckBox.IsChecked == true) { value += 0x0010; }
            if (ModeZTCCheckBox.IsChecked == true) { value += 0x0020; }
            if (ModeBlueLineCheckBox.IsChecked == true) { value += 0x0040; }
            if (ModeDirectionCheckBox.IsChecked == true) { value += 0x0100; }
            if (ModeSpeed14RadioButton.IsChecked == true) { value += 0x0200; }
            if (ModeSpeed28RadioButton.IsChecked == true) { value += 0x0400; }
            if (ModeSpeed128RadioButton.IsChecked == true) { value += 0x0800; }
            if (ModeLongAddressCheckBox.IsChecked == true) { value += 0x1000; }
            return value;
        }

        private void SprogPowerOnButton_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("Sending +<CR> to SPROG");
            string s = SprogTransaction("+\r", 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage($"Timeout!"); }
        }

        private void SprogPowerOffButton_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("Sending -<CR> to SPROG");
            string s = SprogTransaction("-\r", 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage($"Timeout!"); }
        }

        private void SpeedDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (_CurrentSpeed > 0) { _CurrentSpeed--; }
            SpeedTextBox.Text = _CurrentSpeed.ToString();
            LogMessage($"Sending >{_CurrentSpeed}<CR> to SPROG");
            string s = SprogTransaction($"<{_CurrentSpeed}\r", 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage($"Timeout!"); }
        }

        private void SpeedUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (_CurrentSpeed < 128) { _CurrentSpeed++; }
            SpeedTextBox.Text = _CurrentSpeed.ToString();
            LogMessage($"Sending >{_CurrentSpeed}<CR> to SPROG");
            string s = SprogTransaction($">{_CurrentSpeed}\r", 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage($"Timeout!"); }
        }

        private void SprogGetAddressButton_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("Sending A<CR> to SPROG");
            string s = SprogTransaction("A\r", 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage($"Timeout!"); }
        }

        private void SpeedTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            byte sp = 0;
            try
            {
                sp = byte.Parse(SpeedTextBox.Text);
            }
            catch (Exception) { SpeedTextBox.Text = _CurrentSpeed.ToString(); return; }

            if (sp > 128)
            {
                sp = 128;
                SpeedTextBox.Text = sp.ToString();
            }
            else if (sp < _CurrentSpeed)
            {
                _CurrentSpeed = sp;
                LogMessage($"Sending <{_CurrentSpeed}<CR> to SPROG");
                string s = SprogTransaction($"<{_CurrentSpeed}\r", 1000);
                if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage($"Timeout!"); }
            }
            else if (sp > _CurrentSpeed)
            {
                _CurrentSpeed = sp;
                LogMessage($"Sending >{_CurrentSpeed}<CR> to SPROG");
                string s = SprogTransaction($">{_CurrentSpeed}\r", 1000);
                if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage($"Timeout!"); }
            }
        }
    }
}
