using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Input;

namespace AppTestEL418
{
    public partial class TerminalWindow : Window
    {
        private SerialPort serialPort;

        public TerminalWindow(SerialPort port)
        {
            InitializeComponent();
            serialPort = port;
            serialPort.DataReceived += SerialPort_DataReceived;
            txtSend.Focus();
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = serialPort.ReadExisting();
                Dispatcher.Invoke(() =>
                {
                    txtReceive.AppendText(data);
                    txtReceive.ScrollToEnd();
                    scrollViewer.ScrollToEnd();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    txtReceive.AppendText($"\n[Erreur réception] {ex.Message}\n");
                    scrollViewer.ScrollToEnd();
                });
            }
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            SendData();
        }

        private void TxtSend_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendData();
                e.Handled = true;
            }
        }

        private void SendData()
        {
            string msg = txtSend.Text.Trim();

            try
            {
                serialPort.WriteLine(msg); 
                //txtReceive.AppendText($"> {msg}\n"); // DEBUG
                scrollViewer.ScrollToEnd();
                txtSend.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur d'envoi : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Bouton PER
        private void BtnPer_Click(object sender, RoutedEventArgs e)
        {
            SendCommand("PER\r");
        }

        // Bouton STS
        private void BtnSts_Click(object sender, RoutedEventArgs e)
        {
            SendCommand("STS\r");
        }

        // Bouton TST=1
        private void BtnTst1_Click(object sender, RoutedEventArgs e)
        {
            SendCommand("TST=1\r");
        }

        // Bouton TST=0
        private void BtnTst0_Click(object sender, RoutedEventArgs e)
        {
            SendCommand("TST=0\r");
        }

        // Méthode générique pour envoyer une commande
        private void SendCommand(string command)
        {
            try
            {
                serialPort.WriteLine(command);
                //txtReceive.AppendText($"> {command}\n"); // DEBUG
                scrollViewer.ScrollToEnd();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur d'envoi de la commande '{command}' : {ex.Message}",
                              "Erreur",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            txtReceive.Clear();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (serialPort != null)
            {
                serialPort.DataReceived -= SerialPort_DataReceived;
            }
        }
    }
}