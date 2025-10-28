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
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = serialPort.ReadExisting();
                Dispatcher.Invoke(() =>
                {
                    txtReceive.AppendText(data);
                    txtReceive.ScrollToEnd(); // défilement automatique
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
                //txtReceive.AppendText($"\n\r> {msg}\n"); //Affiche la commande envoyée par l'utilisateur
                scrollViewer.ScrollToEnd();
                txtSend.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur d'envoi : {ex.Message}");
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
