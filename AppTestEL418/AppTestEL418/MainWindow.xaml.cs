using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;

namespace AppTestEL418
{
    public partial class MainWindow : Window
    {
        private int currentState = 0;
        private SerialPort serialPort;

        // Textes par étape
        private readonly string[] etapeMessages =
        {
            "ETAPE 0 : Appuyer sur le bouton pour commencer",
            "ETAPE 1 : Entrez le PER (8 digits)",
            "ETAPE 2 : Test STS en cours...",
            "ETAPE 3 : Mettez les DIPs à OFF et appuyez sur le bouton",
            "ETAPE 4 : Mettez les DIPs à ON et reset",
            "ETAPE 5 : Test entrées à OFF",
            "ETAPE 6 : Test entrées à ON",
            "ETAPE 7 : Test décompteur",
            "ETAPE 8 : Test ampoules",
            "ETAPE 9 : Test cellule JOUR",
            "ETAPE 10 : Test cellule NUIT",
            "ETAPE 11 : Test infrarouge",
            "ETAPE 12 : Test accu"
        };

        // Images par étape
        private readonly string[] etapeImages =
        {
            "pack://application:,,,/Images/etape0.png",
            "pack://application:,,,/Images/etape1.png",
            "pack://application:,,,/Images/etape2.png",
            "pack://application:,,,/Images/etape3.png",
            "pack://application:,,,/Images/etape4.png",
            "pack://application:,,,/Images/etape5.png",
            "pack://application:,,,/Images/etape6.png",
            "pack://application:,,,/Images/etape7.png",
            "pack://application:,,,/Images/etape8.png",
            "pack://application:,,,/Images/etape9.png",
            "pack://application:,,,/Images/etape10.png",
            "pack://application:,,,/Images/etape11.png",
            "pack://application:,,,/Images/etape12.png"
        };

        private bool[] dips = new bool[8];       // DIPs
        private bool[] inps = new bool[3];       // Entrées

        public MainWindow()
        {
            InitializeComponent();

            // état initial des boutons
            btnOpen.IsEnabled = true;
            btnClose.IsEnabled = false;

            RefreshPorts();
            UpdateUI();
        }

        private void UpdateUI()
        {
            txtEtape.Text = $"---- {etapeMessages[currentState]} ----";
            txtInstructions.Text = etapeMessages[currentState];

            try
            {
                if (currentState < etapeImages.Length)
                    imgEtape.Source = new BitmapImage(new Uri(etapeImages[currentState]));
            }
            catch
            {
                imgEtape.Source = null;
            }

            panelPer.Visibility = (currentState == 1) ? Visibility.Visible : Visibility.Collapsed;
            panelDips.Visibility = (currentState == 3 || currentState == 4) ? Visibility.Visible : Visibility.Collapsed;
            panelInps.Visibility = (currentState == 5 || currentState == 6) ? Visibility.Visible : Visibility.Collapsed;

            if (currentState == 3 || currentState == 4)
                UpdateDips(dips);

            if (currentState == 5 || currentState == 6)
                UpdateInps(inps);
        }

        private void BtnNextStep_Click(object sender, RoutedEventArgs e)
        {
            if (currentState < etapeMessages.Length - 1)
                currentState++;
            UpdateUI();
        }

        private void BtnPrevStep_Click(object sender, RoutedEventArgs e)
        {
            if (currentState > 0)
                currentState--;
            UpdateUI();
        }

        private void BtnSendPer_Click(object sender, RoutedEventArgs e)
        {
            string per = txtPer.Text.Trim();
            if (per.Length == 8)
            {
                string message = $"PER={per}\r";
                if (serialPort != null && serialPort.IsOpen)
                {
                    try { serialPort.WriteLine(message); Log("PER envoyé : " + message); }
                    catch (Exception ex) { MessageBox.Show("Erreur envoi PER : " + ex.Message); }
                }
                else MessageBox.Show("Le port série n'est pas ouvert !");
            }
            else MessageBox.Show("Le PER doit contenir 8 chiffres.");
        }

        private void Log(string msg)
        {
            txtLog.AppendText($"{DateTime.Now:HH:mm:ss} - {msg}\n");
            txtLog.ScrollToEnd();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshPorts();

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cmbPorts.SelectedItem == null || cmbPorts.SelectedItem.ToString().Contains("Aucun"))
                { MessageBox.Show("Sélectionnez un port valide !"); return; }

                string selectedPort = cmbPorts.SelectedItem.ToString();

                if (serialPort != null && serialPort.IsOpen)
                {
                    if (serialPort.PortName == selectedPort) { Log($"Le port {selectedPort} est déjà ouvert."); btnOpen.IsEnabled = false; btnClose.IsEnabled = true; return; }
                    try { serialPort.DataReceived -= SerialPort_DataReceived; serialPort.Close(); serialPort.Dispose(); } catch { }
                    serialPort = null;
                }

                if (serialPort == null) serialPort = new SerialPort(selectedPort, 9600, Parity.None, 8, StopBits.One);
                else serialPort.PortName = selectedPort;

                serialPort.DataReceived -= SerialPort_DataReceived;
                serialPort.DataReceived += SerialPort_DataReceived;

                serialPort.Open();
                ellipseStatus.Fill = Brushes.Green;
                Log($"Port {serialPort.PortName} ouvert.");
                btnOpen.IsEnabled = false;
                btnClose.IsEnabled = true;
            }
            catch (Exception ex) { MessageBox.Show("Erreur ouverture port : " + ex.Message); }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (serialPort == null) { Log("Aucun port ouvert."); btnOpen.IsEnabled = true; btnClose.IsEnabled = false; ellipseStatus.Fill = Brushes.Red; return; }

                try { if (serialPort.IsOpen) { serialPort.DataReceived -= SerialPort_DataReceived; serialPort.Close(); } }
                catch (Exception exClose) { Log("Erreur pendant la fermeture: " + exClose.Message); }
                try { serialPort.Dispose(); } catch { }
                serialPort = null;
                ellipseStatus.Fill = Brushes.Red;
                Log("Port fermé.");
                btnOpen.IsEnabled = true;
                btnClose.IsEnabled = false;
            }
            catch (Exception ex) { MessageBox.Show("Erreur fermeture port : " + ex.Message); }
        }

        private void RefreshPorts()
        {
            cmbPorts.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length > 0) { foreach (string p in ports) cmbPorts.Items.Add(p); cmbPorts.SelectedIndex = 0; }
            else { cmbPorts.Items.Add("Aucun port trouvé"); cmbPorts.SelectedIndex = 0; }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (serialPort == null || !serialPort.IsOpen) return;

                string message = string.Empty;
                try { message = serialPort.ReadLine(); } catch (TimeoutException) { return; }

                Dispatcher.Invoke(() =>
                {
                    Log("Reçu: " + message);

                    // DIPs
                    if ((currentState == 3 || currentState == 4) && message.Contains("DIP"))
                    {
                        for (int i = 0; i < 8; i++) dips[i] = false;

                        if (message.Contains("ERROR"))
                        {
                            string[] parts = message.Split(new char[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 0; i < parts.Length; i++)
                            {
                                if (parts[i].Equals("DIP", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
                                {
                                    if (int.TryParse(parts[i + 1], out int dipNum))
                                        if (dipNum >= 1 && dipNum <= 8) dips[dipNum - 1] = true; // ON
                                }
                            }
                        }

                        if (message.IndexOf("OFF --> OK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            message.IndexOf("DIP a OFF", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            for (int i = 0; i < 8; i++) dips[i] = false;
                        }

                        UpdateDips(dips);
                    }

                    // Entrées
                    if ((currentState == 5 || currentState == 6) && message.Contains("Entree"))
                    {
                        for (int i = 0; i < 3; i++) inps[i] = false;

                        if (message.Contains("ERROR"))
                        {
                            string[] parts = message.Split(new char[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 0; i < parts.Length; i++)
                            {
                                if (parts[i].Equals("Entree", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
                                {
                                    if (int.TryParse(parts[i + 1], out int entreeNum))
                                        if (entreeNum >= 1 && entreeNum <= 3) inps[entreeNum - 1] = true;
                                }
                            }
                        }

                        if (message.IndexOf("Entrees a OFF", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            for (int i = 0; i < 3; i++) inps[i] = false;
                        }

                        UpdateInps(inps);
                    }
                });
            }
            catch (Exception ex) { Dispatcher.Invoke(() => Log("Erreur réception: " + ex.Message)); }
        }

        private void UpdateDips(bool[] dips)
        {
            wrapDips.Children.Clear();
            for (int i = 0; i < dips.Length; i++)
            {
                TextBlock txt = new TextBlock
                {
                    Text = $"{i + 1}: {(dips[i] ? "ON" : "OFF")}",
                    FontSize = 17,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(10),
                    Foreground = ((currentState == 3 && !dips[i]) || (currentState == 4 && dips[i])) ? Brushes.LimeGreen : Brushes.Red
                };
                wrapDips.Children.Add(txt);
            }
        }

        private void UpdateInps(bool[] inps)
        {
            wrapInps.Children.Clear();
            for (int i = 0; i < inps.Length; i++)
            {
                TextBlock txt = new TextBlock
                {
                    Text = $"{i + 1}: {(inps[i] ? "ON" : "OFF")}",
                    FontSize = 17,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(10),
                    Foreground = ((currentState == 5 && !inps[i]) || (currentState == 6 && inps[i])) ? Brushes.LimeGreen : Brushes.Red
                };
                wrapInps.Children.Add(txt);
            }
        }
    }
}
