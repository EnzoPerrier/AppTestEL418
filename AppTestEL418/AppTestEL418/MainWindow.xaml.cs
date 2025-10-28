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
        private TerminalWindow terminalWindow = null;


        private readonly string[] etapeMessages =
        {
            "ETAPE 0 : Appuyer sur le bouton pour commencer",
            "ETAPE 1 : Entrez le PER (8 digits)",
            "ETAPE 2 : Test STS",
            "ETAPE 3 : DIPs à OFF",
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

        private readonly string[] instructionMessages =
        {
            "Branchez la carte, alimentez le banc en 12.5V et appuyez ensuite sur le BP valider pour commencer",
            "Mesurez la base de temps à l'aide du fréquencemètre, reportez la dans la zone de texte ci-dessous\nSi la base de temps commence par 99xxxxx et ne contient que 7 digits, ajoutez un '0' à la fin",
            "Test STS auto en  cours ...",
            "Mettez tous les DIPs à OFF et appuyez sur le BP valider du banc de test",
            "Mettez tous les DIPs à ON et appuyer sur le BP reset de la carte, ensuite appuyez sur le BP valider du banc de test",
            "Test auto des entrées à OFF en cours ...",
            "Test auto des entrées à ON en cours ...",
            "Vérifiez que toutes les LEDs du décompteur s'allument correctement à la bonne luminosité",
            "Vérifiez que tous les défauts ampoules apparaissent les uns après les autres, ensuite appuyez sur le bouton valider si OK",
            "Test cellule JOUR en cours, appuyez sur le BP valider une fois la cellule exposée à la lumière",
            "Test cellule NUIT en cours, appuyez sur le BP valider une fois la cellule exposée à l'obscurité",
            "Vérifiez l'IR en utilisant la télécommande",
            "Vérifiez que le message 'supression batterie' s'affiche à l'écran LCD'"
        };

        private readonly string[] etapeImages =
        {
            "pack://application:,,,/Images/etape0.png",
            "pack://application:,,,/Images/etape1.png",
            //"pack://application:,,,/Images/etape2.png",
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

        private bool[] dips = new bool[8];
        private bool[] inps = new bool[3];

        public MainWindow()
        {
            InitializeComponent();
            btnOpen.IsEnabled = true;
            btnClose.IsEnabled = false;
            RefreshPorts();
            UpdateUI();
        }

        private void UpdateUI()
        {
            txtEtape.Text = $"---- {etapeMessages[currentState]} ----";
            txtInstructions.Text = instructionMessages[currentState];

            try
            {
                if (currentState < etapeImages.Length)
                    imgEtape.Source = new BitmapImage(new Uri(etapeImages[currentState]));
            }
            catch { imgEtape.Source = null; }

            panelPer.Visibility = (currentState == 1) ? Visibility.Visible : Visibility.Collapsed;
            panelSts.Visibility = (currentState == 2) ? Visibility.Visible : Visibility.Collapsed;
            panelDips.Visibility = (currentState == 3 || currentState == 4) ? Visibility.Visible : Visibility.Collapsed;
            panelInps.Visibility = (currentState == 5 || currentState == 6) ? Visibility.Visible : Visibility.Collapsed;

            if (currentState == 3 || currentState == 4) UpdateDips(dips);
            if (currentState == 5 || currentState == 6) UpdateInps(inps);
        }

        //DEBUG
        private void BtnNextStep_Click(object sender, RoutedEventArgs e) 
        {
            if (currentState < etapeMessages.Length - 1) currentState++;
            UpdateUI();
        }

        private void BtnPrevStep_Click(object sender, RoutedEventArgs e)
        {
            if (currentState > 0) currentState--;
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

        private void TxtPer_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (currentState == 1 && e.Key == System.Windows.Input.Key.Enter)
            {
                BtnSendPer_Click(sender, e);
                e.Handled = true;
            }
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
                {
                    MessageBox.Show("Sélectionnez un port valide !");
                    return;
                }

                string selectedPort = cmbPorts.SelectedItem.ToString();

                if (serialPort != null && serialPort.IsOpen)
                {
                    if (serialPort.PortName == selectedPort)
                    {
                        Log($"Le port {selectedPort} est déjà ouvert.");
                        btnOpen.IsEnabled = false;
                        btnClose.IsEnabled = true;
                        return;
                    }

                    serialPort.DataReceived -= SerialPort_DataReceived;
                    serialPort.Close();
                    serialPort.Dispose();
                    serialPort = null;
                }

                serialPort = new SerialPort(selectedPort, 9600, Parity.None, 8, StopBits.One)
                {
                    Encoding = System.Text.Encoding.ASCII,
                    NewLine = "\r" 
                };

                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.Open();

                ellipseStatus.Fill = Brushes.Green;
                Log($"Port {serialPort.PortName} ouvert.");
                btnOpen.IsEnabled = false;
                btnClose.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur ouverture port : " + ex.Message);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (serialPort != null)
                {
                    if (serialPort.IsOpen)
                    {
                        serialPort.DataReceived -= SerialPort_DataReceived;
                        serialPort.Close();
                    }
                    serialPort.Dispose();
                    serialPort = null;
                }

                ellipseStatus.Fill = Brushes.Red;
                Log("Port fermé.");
                btnOpen.IsEnabled = true;
                btnClose.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur fermeture port : " + ex.Message);
            }
        }

        private void RefreshPorts()
        {
            cmbPorts.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length > 0)
            {
                foreach (string p in ports) cmbPorts.Items.Add(p);
                cmbPorts.SelectedIndex = 0;
            }
            else
            {
                cmbPorts.Items.Add("Aucun port trouvé");
                cmbPorts.SelectedIndex = 0;
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (serialPort == null || !serialPort.IsOpen) return; // Si pas de port série

            try
            {
                string message = serialPort.ReadLine(); // lit une ligne complète

                if (!string.IsNullOrEmpty(message))
                {
                    // BeginInvoke non bloquant évite freeze
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        txtLog.AppendText($"{DateTime.Now:HH:mm:ss} - {message}\n");
                        txtLog.ScrollToEnd();
                        HandleSerialMessage(message);
                    }));
                }
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    txtLog.AppendText("Erreur réception: " + ex.Message + "\n");
                    txtLog.ScrollToEnd();
                }));
            }
        }


        private void HandleSerialMessage(string message)
        {
            string cleanedMessage = message.Replace("-", "").Trim();
            int idx = cleanedMessage.IndexOf("ETAPE", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                idx += 5;
                while (idx < cleanedMessage.Length && char.IsWhiteSpace(cleanedMessage[idx])) idx++;

                string numberPart = "";
                while (idx < cleanedMessage.Length && char.IsDigit(cleanedMessage[idx]))
                    numberPart += cleanedMessage[idx++];

                if (int.TryParse(numberPart, out int step))
                {
                    if (step >= 0 && step < etapeMessages.Length)
                    {
                        currentState = step;
                        UpdateUI();
                        Log($"Changement d'étape automatique : {step}");
                    }
                }
            }

            // Gestion STS
            if ((currentState == 2) && message.Contains("STS"))
            {
                UpdateSTS(message);
            }

            // Gestion DIP
            if ((currentState == 3 || currentState == 4) && message.Contains("DIP"))
            {
                Array.Fill(dips, false);

                if (message.Contains("ERROR"))
                {
                    string[] parts = message.Split(new char[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i].Equals("DIP", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
                        {
                            if (int.TryParse(parts[i + 1], out int dipNum) && dipNum >= 1 && dipNum <= 8)
                                dips[dipNum - 1] = true;
                        }
                    }
                }

                if (message.Contains("OFF --> OK", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("DIP a OFF", StringComparison.OrdinalIgnoreCase))
                {
                    Array.Fill(dips, false);
                }

                UpdateDips(dips);
            }

            // Gestion Entrées
            if ((currentState == 5 || currentState == 6) && message.Contains("Entree"))
            {
                Array.Fill(inps, false);

                if (message.Contains("ERROR"))
                {
                    string[] parts = message.Split(new char[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i].Equals("Entree", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
                        {
                            if (int.TryParse(parts[i + 1], out int entreeNum) && entreeNum >= 1 && entreeNum <= 3)
                                inps[entreeNum - 1] = true;
                        }
                    }
                }

                if (message.Contains("Entrees a OFF", StringComparison.OrdinalIgnoreCase))
                {
                    Array.Fill(inps, false);
                }

                UpdateInps(inps);
            }
        }

        //Mise à jour STS
        private void UpdateSTS(string STSmsg)
        {
            wrapSTS.Children.Clear();
            
                TextBlock txt = new TextBlock
                {
                    Text = STSmsg,
                    FontSize = 22,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(10)
                };
                wrapSTS.Children.Add(txt);
            

        }

        // Mise à jour  affichage état  des dips
        private void UpdateDips(bool[] dips)
        {
            wrapDips.Children.Clear();
            for (int i = 0; i < dips.Length; i++)
            {
                TextBlock txt = new TextBlock
                {
                    Text = $"{i + 1}: {(dips[i] ? "ON" : "OFF")}",
                    FontSize = 22,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(10),
                    Foreground = ((currentState == 3 && !dips[i]) || (currentState == 4 && dips[i])) ? Brushes.LimeGreen : Brushes.Red
                };
                wrapDips.Children.Add(txt);
            }
        }

        // Mode terminal
        private void BtnOpenTerminal_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort == null || !serialPort.IsOpen)
            {
                MessageBox.Show("Veuillez ouvrir un port série avant d'accéder au terminal !");
                return;
            }

            // Si le terminal est déjà ouvert, on ne le recrée pas
            if (terminalWindow != null && terminalWindow.IsVisible)
            {
                terminalWindow.Focus();
                return;
            }

            // On détache la réception principale pour pas que ça s'affiche sur les 2 pages
            serialPort.DataReceived -= SerialPort_DataReceived;

            // On ouvre le terminal avec le port actif
            terminalWindow = new TerminalWindow(serialPort);
            terminalWindow.Owner = this;

            // Quand le terminal se ferme, on rattache à nouveau la réception principale
            terminalWindow.Closed += (s, args) =>
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    serialPort.DataReceived += SerialPort_DataReceived;
                }
                terminalWindow = null;
            };

            terminalWindow.Show();
        }



        // Mise à Jour affichage Entrées
        private void UpdateInps(bool[] inps)
        {
            wrapInps.Children.Clear();
            for (int i = 0; i < inps.Length; i++)
            {
                TextBlock txt = new TextBlock
                {
                    Text = $"{i + 1}: {(inps[i] ? "ON" : "OFF")}",
                    FontSize = 22,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(10),
                    Foreground = ((currentState == 5 && !inps[i]) || (currentState == 6 && inps[i])) ? Brushes.LimeGreen : Brushes.Red
                };
                wrapInps.Children.Add(txt);
            }
        }

        // A la fermeture de la fenêtre 
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (serialPort != null)
                {
                    serialPort.DataReceived -= SerialPort_DataReceived;
                    if (serialPort.IsOpen)
                        serialPort.Close();
                    serialPort.Dispose();
                    serialPort = null;
                }
            }
            catch { }
        }


    }
}
