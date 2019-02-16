using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace modem_com
{
    /// <summary>
    /// Interaction logic for Terminal.xaml
    /// </summary>
    public partial class Terminal : Window
    {

        private ModemCommunication modemCommunication = new ModemCommunication();

        public Terminal()
        {
            InitializeComponent();

            // wypelnij liste wszystkimi dostepnymi portami szeregowymi
            foreach (string port in SerialPort.GetPortNames())
            {
                port_comboBox.Items.Add(port);
            }
            
            // wybierz pierwszy element z listy
            port_comboBox.SelectedIndex = port_comboBox.Items.Count - 1;

            // wypelnij liste command code
            foreach (ModemRequest m in ModemRequest.GetListOfCommands())
            {
                commandCode_comboBox.Items.Add(m.Name);
            }
            commandCode_comboBox.SelectedIndex = 5;

            // wszystkie przychodzące ramki będą pokazywane w polu tekstowym
            modemCommunication.TerminalOutput = WriteToTerminal;

            // eventy od zaznaczania radio boxow
            EIGHTPSK_RadioButton.Checked += ModulationChanged;
            BPSK_RadioButton.Checked += ModulationChanged;
            QPSK_RadioButton.Checked += ModulationChanged;
            BPSK_PNA_RadioButton.Checked += ModulationChanged;

            PHY_RadioButton.Checked += ModeChanged;
            DL_RadioButton.Checked += ModeChanged;
            
        }

        private void ModulationChanged(object sender, RoutedEventArgs e)
        {
            if ((bool)EIGHTPSK_RadioButton.IsChecked)
            {
                FEC_CheckBox.IsEnabled = false;
                FEC_CheckBox.IsChecked = false;
            }
            else if ((bool)BPSK_PNA_RadioButton.IsChecked)
            {
                FEC_CheckBox.IsEnabled = false;
                FEC_CheckBox.IsChecked = true;
            }
            else
            {
                FEC_CheckBox.IsEnabled = true;
            }
        }

        private void ModeChanged(object sender, RoutedEventArgs e)
        {
            byte[] frame = null;
            if ((bool)DL_RadioButton.IsChecked)
            {
                frame = ModemRequest.CreateFrame(Command.MIB_WriteRequest, null, Mode.DL, 0, false);
                commandCode_comboBox.SelectedIndex = 5;
            }
            else if ((bool)PHY_RadioButton.IsChecked)
            {
                frame = ModemRequest.CreateFrame(Command.MIB_WriteRequest, null, Mode.PHY, 0, false);
                commandCode_comboBox.SelectedIndex = 4;
            }

            if(modemCommunication.Port.IsOpen)
                modemCommunication.SendFrame(frame);
        }

        private void WriteToTerminal(string text)
        {
            Dispatcher.BeginInvoke((Action)(() => {
                received_textBox.Text += text;
                received_textBox.ScrollToEnd();
            }));
        }

        private void PortOpenCloseButtonClick(object sender, RoutedEventArgs e)
        {
            if (port_comboBox.SelectedItem == null)
                return;

            string port_name = port_comboBox.SelectedItem.ToString();
            SerialPort port = modemCommunication.Port;

            // zamknij port, jezeli jest otwarty
            if(port != null && port.IsOpen)
            {
                port.Close();
                portStatus_label.Content = "Status: Disconnected";
                portOpenClose_button.Content = "Open";
                return;
            }

            // otworz port, o ile zaden nie jest otworzony
            try
            {
                port.PortName = port_name;
                port.BaudRate = 57600;
                port.Parity = Parity.None;
                port.DataBits = 8;
                port.StopBits = StopBits.One;
                port.Open();
                portStatus_label.Content = "Status: Connected to " + port_name;
                portOpenClose_button.Content = "Close";
                DL_RadioButton.IsChecked = true;
                
                // reset modemu, zeby przelaczyl sie w tryb DL
                modemCommunication.ResetModem();
            }
            catch(Exception)
            {
                portStatus_label.Content = "Status: Cannot connect to " + port_name;
            }

        }

        private void ResetButtonClick(object sender, RoutedEventArgs e)
        {
            if (modemCommunication.Port.IsOpen)
            {
                modemCommunication.ResetModem();
                DL_RadioButton.IsChecked = true;
            }
        }

        private void SendButtonClick(object sender, RoutedEventArgs e)
        {
            if (modemCommunication.Port.IsOpen)
            {
                Command command = ModemRequest.GetCommandFromName(commandCode_comboBox.SelectedItem.ToString());
                byte[] asciiBytes = Encoding.ASCII.GetBytes(ascii_TextBox.Text);
                Mode mode = DL_RadioButton.IsChecked == true ? Mode.DL : Mode.PHY;

                Modulation modulation;
                if (BPSK_RadioButton.IsChecked == true)
                    modulation = Modulation.BPSK;
                else if (QPSK_RadioButton.IsChecked == true)
                    modulation = Modulation.QPSK;
                else if (EIGHTPSK_RadioButton.IsChecked == true)
                    modulation = Modulation.EIGHT_PSK;
                else
                    modulation = Modulation.BPSK_WITH_PNA;

                byte[] frame = ModemRequest.CreateFrame(command, asciiBytes, mode, modulation, (bool)FEC_CheckBox.IsChecked);

                //byte cmd = ModemRequest.GetCCFromName(commandCode_comboBox.SelectedItem.ToString());
                //byte[] frame = ModemCommunication.MakeFrame(cmd, asciiBytes);
                if(frame != null)
                    modemCommunication.SendFrame(frame);
            }
        }

        private void AsciiTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            if (ascii_TextBox.IsFocused)
            {
                hex_textBox.Text = "";

                byte[] asciiBytes = Encoding.ASCII.GetBytes(ascii_TextBox.Text);

                foreach(byte b in asciiBytes)
                {
                    hex_textBox.Text += String.Format("{0:x} ", b);
                }
            }
        }

        private void HexTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            if (hex_textBox.IsFocused)
            {
                ascii_TextBox.Text = "";

                string[] hex_numbers = hex_textBox.Text.Split(' ');
                foreach(string hex_string in hex_numbers)
                {
                    Console.WriteLine(hex_string);
                    if(hex_string != "")
                    {
                        try
                        {
                            int hex = Convert.ToInt32(hex_string, 16);
                            ascii_TextBox.Text += System.Convert.ToChar(hex);
                        }
                        catch (Exception)
                        {
                            ascii_TextBox.Text = "ERROR";
                        }
                    }
                }
            }

        }
        

        // close connection on exit
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SerialPort port = modemCommunication.Port;
            if(port != null && port.IsOpen)
            {
                port.Close();
            }
            modemCommunication.ApplicationRunning = false;
        }


    }
}
