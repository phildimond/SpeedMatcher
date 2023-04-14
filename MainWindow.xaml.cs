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
    public enum Direction
    {
        Forward = 0,
        Reverse = 1
    };

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Settings? _Settings = null;
        private byte _CurrentSpeed = 0;
        private Direction _CurrentDirection = Direction.Forward;
        private SprogII? _Sprog = null;

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

            // If the SPROG port exists, try to connect to it
            if (SProgPortSelector.SelectedIndex != 0)
            {
                ConnectItem.IsChecked = true;
                ConnectItem_Click(this, new RoutedEventArgs());
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
                            _Sprog = new SprogII(_Settings.SprogPort);
                        }
                        catch (Exception ex) 
                        { 
                            MessageBox.Show($"Exception when trying to open SPROG port: {ex.Message}"); 
                            ConnectItem.IsChecked = false;
                            return;
                        }
                        _Sprog.LogMessageAvailable += new LogEventDelegate(delegate (string s) { LogMessage(s); });
                        ConnectItem.Header = "Connected";
                        ConnectItem.IsChecked = true;
                        LogMessage($"SPROG connected on {_Settings.SprogPort}");
                        SprogInfoButton_Click(this, new RoutedEventArgs());
                        SprogGetModeButton_Click(this, new RoutedEventArgs());
                        SprogGetAddressButton_Click(this, new RoutedEventArgs());
                        SprogPowerOffButton_Click(this, new RoutedEventArgs());
                    }
                    break;
                case false:
                    if (_Settings !=  null && _Sprog != null && _Sprog.IsOpen)
                    {
                        try { _Sprog.Close(); }
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
            if (_Sprog != null && _Sprog.IsOpen) { _Sprog.Close(); }
        }

        private void LogMessage(string s)
        {
            s = s.Replace("\r", "<CR>");
            s = s.Replace("\n", "<LF>");
            LogLB.Items.Add(s);
            while (LogLB.Items.Count > 100) { LogLB.Items.RemoveAt(0); }
            LogScrollViewer.ScrollToEnd();
        }

        private void SprogInfoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_Sprog == null) { return; }
            LogMessage($"Sending {SprogII.GetSprogInfoCommand} to SPROG");
            string s = _Sprog.SprogTransaction(SprogII.GetSprogInfoCommand, 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage($"Timeout!"); }
        }

        private void SprogGetModeButton_Click(object sender, RoutedEventArgs e)
        {
            LogMessage($"Sending {SprogII.GetModeCommand} to SPROG");
            string s = _Sprog.SprogTransaction(SprogII.GetModeCommand, 1000);
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
            string s = _Sprog.SprogTransaction($"M h{mode:X4}\r", 1000);
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
            if ((value & 0x0100) > 0) { ModeDirectionCheckBox.IsChecked = true; _CurrentDirection = Direction.Reverse; } 
            else { ModeDirectionCheckBox.IsChecked = false; _CurrentDirection = Direction.Forward; }
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
            //string s = "+\r";
            LogMessage($"Sending {SprogII.PowerOnCommand} to SPROG");
            string s = _Sprog.SprogTransaction(SprogII.PowerOnCommand, 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage($"Timeout!"); }
        }

        private void SprogPowerOffButton_Click(object sender, RoutedEventArgs e)
        {
            LogMessage($"Sending {SprogII.PowerOffCommand} to SPROG");
            string s = _Sprog.SprogTransaction(SprogII.PowerOffCommand, 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage("Timeout!"); }
        }

        private void SpeedDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (_CurrentSpeed > 0) { _CurrentSpeed--; }
            SpeedTextBox.Text = _CurrentSpeed.ToString();
            string s = SprogII.ForwardSpeedCommand(_CurrentSpeed);
            if (ModeDirectionCheckBox.IsChecked == true) { s = SprogII.ReverseSpeedCommand(_CurrentSpeed); }
            LogMessage($"Sending {s} to SPROG");
            s = _Sprog.SprogTransaction(s, 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage("Timeout!"); }
        }

        private void SpeedUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (_CurrentSpeed < 127) { _CurrentSpeed++; }
            SpeedTextBox.Text = _CurrentSpeed.ToString();
            string s = SprogII.ForwardSpeedCommand(_CurrentSpeed);
            if (ModeDirectionCheckBox.IsChecked == true) { s = SprogII.ReverseSpeedCommand(_CurrentSpeed); }
            LogMessage($"Sending {s} to SPROG");
            s = _Sprog.SprogTransaction(s, 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage("Timeout!"); }
        }

        private void SprogSetSpeedButton_Click(object sender, RoutedEventArgs e)
        {
            byte sp = 0;
            try
            {
                sp = byte.Parse(SpeedTextBox.Text);
            }
            catch (Exception) { SpeedTextBox.Text = _CurrentSpeed.ToString(); return; }

            if (sp > 127) { sp = 127; }
            _CurrentSpeed = sp;
            SpeedTextBox.Text = _CurrentSpeed.ToString();
            string s = SprogII.ForwardSpeedCommand(_CurrentSpeed);
            if (ModeDirectionCheckBox.IsChecked == true) { s = SprogII.ReverseSpeedCommand(_CurrentSpeed); }
            LogMessage($"Sending {s} to SPROG");
            s = _Sprog.SprogTransaction(s, 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage("Timeout!"); }
        }

        private void SprogGetAddressButton_Click(object sender, RoutedEventArgs e)
        {
            string s = "A\r";
            LogMessage($"Sending {s} to SPROG");
            s = _Sprog.SprogTransaction(s, 1000);
            if (s != string.Empty)
            {
                LogMessage($"Received {s}");
                if (s.Contains("P>"))
                {
                    s = s.Replace("<CR>", string.Empty);
                    s = s.Replace("<LF>", string.Empty);
                    s = s.Replace("P>", string.Empty);
                    s = s.Replace("=", string.Empty);
                    s = s.Replace(" ", string.Empty);
                    int i = 0;
                    try { i = int.Parse(s); } catch { LogMessage($"Exception converting {s} to an int."); }
                    AddressTextBox.Text = i.ToString();
                }
            }
            else { LogMessage("Timeout!"); }
        }

        private void SprogSetAddressButton_Click(object sender, RoutedEventArgs e)
        {
            string s = $"A {AddressTextBox.Text}\r";
            LogMessage($"Sending {s} to SPROG");
            s = _Sprog.SprogTransaction(s, 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage("Timeout!"); }
        }

        private void SprogForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_CurrentDirection== Direction.Forward) { return; }
            // We have to stop the loco, then reverse it, then speed up again
            byte oldSpeed = _CurrentSpeed;
            // First set speed to zero
            _CurrentSpeed = 0;
            SpeedTextBox.Text = _CurrentSpeed.ToString();
            string s = SprogII.ReverseSpeedCommand(_CurrentSpeed);
            LogMessage($"Sending {s} to SPROG");
            s = _Sprog.SprogTransaction(s, 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage("Timeout!"); }
            // Now set the mode to change the direction
            ModeDirectionCheckBox.IsChecked = false;
            SprogSetModeButton_Click(this, new RoutedEventArgs());
            _CurrentDirection = Direction.Reverse;
            // Finally set the speed back to the original value
            _CurrentSpeed = oldSpeed;
            SpeedTextBox.Text = _CurrentSpeed.ToString();
            s = SprogII.ForwardSpeedCommand(_CurrentSpeed);
            LogMessage($"Sending {s} to SPROG");
            s = _Sprog.SprogTransaction(s, 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage("Timeout!"); }
            _CurrentDirection= Direction.Forward;
        }

        private void SprogReverseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_CurrentDirection== Direction.Reverse) { return; }
            // We have to stop the loco, then reverse it, then speed up again
            byte oldSpeed = _CurrentSpeed;
            // First set speed to zero
            _CurrentSpeed = 0;
            SpeedTextBox.Text = _CurrentSpeed.ToString();
            string s = SprogII.ForwardSpeedCommand(_CurrentSpeed);
            LogMessage($"Sending {s} to SPROG");
            s = _Sprog.SprogTransaction(s, 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage("Timeout!"); }
            // Now set the mode to change the direction
            ModeDirectionCheckBox.IsChecked = true;
            SprogSetModeButton_Click(this, new RoutedEventArgs());
            _CurrentDirection = Direction.Reverse;
            // Finally set the speed back to the original value
            _CurrentSpeed = oldSpeed;
            SpeedTextBox.Text = _CurrentSpeed.ToString();
            s = SprogII.ReverseSpeedCommand(_CurrentSpeed);
            LogMessage($"Sending {s} to SPROG");
            s = _Sprog.SprogTransaction(s, 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage("Timeout!"); }
            _CurrentDirection = Direction.Reverse;
        }

        private void SprogIdleButton_Click(object sender, RoutedEventArgs e)
        {
            _CurrentSpeed = 0;
            SpeedTextBox.Text = _CurrentSpeed.ToString();
            string s = SprogII.ForwardSpeedCommand(_CurrentSpeed);
            if (_CurrentDirection == Direction.Reverse) { s = SprogII.ReverseSpeedCommand(_CurrentSpeed); }
            LogMessage($"Sending {s} to SPROG");
            s = _Sprog.SprogTransaction(s, 1000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage("Timeout!"); }
        }

        private void ReadDirectBitButton_Click(object sender, RoutedEventArgs e)
        {
            byte cv = 1;
            try { cv = byte.Parse(CvTextBox.Text); } 
            catch (Exception ex) { LogMessage($"Exception converting CV {CvTextBox.Text} to a number: {ex.Message}"); }
            string s = SprogII.ReadCvDirectBitCommand(cv);
            LogMessage($"Sending {s} to SPROG");
            s = _Sprog.SprogTransaction(s, 5000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage("Timeout!"); }
            if (s.Contains("P>"))
            {
                s = s.Replace("<CR>", string.Empty);
                s = s.Replace("<LF>", string.Empty);
                s = s.Replace("P>", string.Empty);
                s = s.Replace("=", string.Empty);
                s = s.Replace(" ", string.Empty);
                byte cvVal = 0;
                bool isHex = false;
                try
                {
                    if (s.Contains("h"))
                    {
                        isHex = true;
                        s = s.Replace("h", string.Empty);
                        cvVal = byte.Parse(s, System.Globalization.NumberStyles.HexNumber);
                    }
                    else { cvVal = byte.Parse(s, System.Globalization.NumberStyles.HexNumber); }
                }
                catch (Exception ex)
                {
                    LogMessage($"Could not convert [{s}] to a byte value: {ex.Message}"); return;
                }
                if (isHex) { CvValueTextBox.Text = $"${cvVal:X2}"; }
                else { CvValueTextBox.Text = cvVal.ToString(); }
            }
        }

        private void WriteDirectBitButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ReadPagedModeButton_Click(object sender, RoutedEventArgs e)
        {
            byte cv = 1;
            try { cv = byte.Parse(CvTextBox.Text); }
            catch (Exception ex) { LogMessage($"Exception converting CV {CvTextBox.Text} to a number: {ex.Message}"); }
            string s = SprogII.ReadCvPagedCommand(cv);
            LogMessage($"Sending {s} to SPROG");
            s = _Sprog.SprogTransaction(s, 5000);
            if (s != string.Empty) { LogMessage($"Received {s}"); } else { LogMessage("Timeout!"); }
            if (s.Contains("P>"))
            {
                s = s.Replace("<CR>", string.Empty);
                s = s.Replace("<LF>", string.Empty);
                s = s.Replace("P>", string.Empty);
                s = s.Replace("=", string.Empty);
                s = s.Replace(" ", string.Empty);
                s = s.Replace("O", string.Empty);
                byte cvVal = 0;
                bool isHex = false;
                try {
                    if (s.Contains("h"))
                    {
                        isHex = true;
                        s = s.Replace("h", string.Empty);
                        cvVal = byte.Parse(s, System.Globalization.NumberStyles.HexNumber);
                    }
                    else { cvVal = byte.Parse(s, System.Globalization.NumberStyles.HexNumber); }
                } 
                catch (Exception ex) { 
                    LogMessage($"Could not convert [{s}] to a byte value: {ex.Message}"); return; 
                }
                if (isHex) { CvValueTextBox.Text = $"${cvVal:X2}"; }
                else { CvValueTextBox.Text = cvVal.ToString(); }
            }
        }

        private void WritePagedModeButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
