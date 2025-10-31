﻿using System;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AppTestEL418
{
    public partial class MainWindow : Window
    {
        private int currentState = 0;
        private SerialPort serialPort;
        private TerminalWindow terminalWindow = null;

        // DIPs
        private bool[] dipsError = new bool[8];      // true = NOK (rouge), false = OK (vert)
        private bool[] dipsPhysical = new bool[8];   // true = ON, false = OFF (valeur lue)

        // Entrées
        private bool[] inpsError = new bool[3];
        private bool[] inpsPhysical = new bool[3];

        private readonly string[] etapeMessages =
        {
            "ETAPE 0 : Appuyez sur le bouton pour commencer",
            "ETAPE 1 : Entrez le PER (8 digits)",
            "ETAPE 2 : Test STS",
            "ETAPE 3 : DIPs à OFF",
            "ETAPE 4 : DIPs à ON",
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
            "Mesurez la base de temps à l'aide du fréquencemètre, reportez la dans la zone de texte ci-dessous\nSi la base de temps commence par 99xxxxx et ne contient que 7 digits, ajoutez un '0' à la fin (ex: 99999980)",
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

        public MainWindow()
        {
            InitializeComponent();

            // par défaut tout NOK (pour éviter confusion)
            Array.Fill(dipsError, true);
            Array.Fill(dipsPhysical, false);
            Array.Fill(inpsError, true);
            Array.Fill(inpsPhysical, false);

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

            // Lorsque l'on entre sur les étapes DIPs on initialise en NOK (attente de la carte)
            if (currentState == 3 || currentState == 4)
            {
                for (int i = 0; i < dipsError.Length; i++)
                {
                    dipsError[i] = true;      // NOK par défaut
                    dipsPhysical[i] = false;  // valeur inconnue => OFF affiché
                }
                UpdateDips();
            }

            // Même principe pour INPs
            if (currentState == 5 || currentState == 6)
            {
                for (int i = 0; i < inpsError.Length; i++)
                {
                    inpsError[i] = true;
                    inpsPhysical[i] = false;
                }
                UpdateInps();
            }
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
                string message = per;
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

                    // nettoyage si ancien port ouvert
                    try
                    {
                        serialPort.DataReceived -= SerialPort_DataReceived;
                        if (serialPort.IsOpen) serialPort.Close();
                        serialPort.Dispose();
                    }
                    catch { }
                    serialPort = null;
                }

                serialPort = new SerialPort(selectedPort, 9600, Parity.None, 8, StopBits.One)
                {
                    Encoding = System.Text.Encoding.ASCII,
                    NewLine = "\r" // ou "\r\n" selon ton appareil
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
                    try
                    {
                        serialPort.DataReceived -= SerialPort_DataReceived;
                        if (serialPort.IsOpen) serialPort.Close();
                        serialPort.Dispose();
                    }
                    catch (Exception exClose)
                    {
                        Log("Erreur pendant la fermeture: " + exClose.Message);
                    }
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
            // Nettoyage simple
            string cleanedMessage = message.Replace("\r", "").Replace("\n", "").Trim();

            // Détection ETAPE
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
                        //Log($"Changement d'étape automatique : {step}"); //Debug
                    }
                }
            }

            // --- Gestion DIPs ---
            if ((currentState == 3 || currentState == 4) && cleanedMessage.IndexOf("DIP", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Si message global "DIP ... --> OK" on met tout OK (vert)
                if (cleanedMessage.IndexOf("--> OK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    cleanedMessage.IndexOf("DIPs OK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    cleanedMessage.IndexOf("DIP a OFF --> OK", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    for (int i = 0; i < dipsError.Length; i++)
                    {
                        dipsError[i] = false;
                    }
                }
                else
                {
                    // Parse toutes les occurrences "DIP <num> a <ON|OFF>"
                    var matches = Regex.Matches(cleanedMessage, @"DIP\s+(\d+)\s+a\s+(ON|OFF)", RegexOptions.IgnoreCase);
                    foreach (Match m in matches)
                    {
                        if (m.Groups.Count >= 3 &&
                            int.TryParse(m.Groups[1].Value, out int dipNum) &&
                            dipNum >= 1 && dipNum <= 8)
                        {
                            string state = m.Groups[2].Value.ToUpper(); // "ON" ou "OFF"
                            bool isOn = state == "ON";

                            // attendu : true si ON attendu (étape 4), false si OFF attendu (étape 3)
                            bool attenduOn = (currentState == 4);

                            // erreur si l'état réel != attendu
                            bool isError = (isOn != attenduOn);

                            dipsPhysical[dipNum - 1] = isOn;
                            dipsError[dipNum - 1] = isError;
                        }
                    }
                }

                UpdateDips();
            }

            // --- Gestion Entrées ---
            if ((currentState == 5 || currentState == 6) && cleanedMessage.IndexOf("ENTREE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (cleanedMessage.IndexOf("--> OK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    cleanedMessage.IndexOf("Entrees a OFF --> OK", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    cleanedMessage.IndexOf("Entrees OK", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    for (int i = 0; i < inpsError.Length; i++)
                        inpsError[i] = false;
                }
                else
                {
                    var matchesInp = Regex.Matches(cleanedMessage, @"Entree\s+(\d+)\s+a\s+(ON|OFF)", RegexOptions.IgnoreCase);
                    foreach (Match m in matchesInp)
                    {
                        if (m.Groups.Count >= 3 &&
                            int.TryParse(m.Groups[1].Value, out int num) &&
                            num >= 1 && num <= 3)
                        {
                            string state = m.Groups[2].Value.ToUpper();
                            bool isOn = state == "ON";
                            bool attenduOn = (currentState == 6); // étape 6 attend ON

                            bool isError = (isOn != attenduOn);

                            inpsPhysical[num - 1] = isOn;
                            inpsError[num - 1] = isError;
                        }
                    }
                }

                UpdateInps();
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

        // Mise à jour affichage état des DIPs
        private void UpdateDips()
        {
            wrapDips.Children.Clear();

            for (int i = 0; i < dipsError.Length; i++)
            {
                StackPanel dipPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(10),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // Voyant : rouge si erreur, vert sinon
                Ellipse led = new Ellipse
                {
                    Width = 40,
                    Height = 40,
                    Fill = dipsError[i] ? Brushes.Red : Brushes.LimeGreen,
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };

                TextBlock dipLabel = new TextBlock
                {
                    Text = $"DIP {i + 1}",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 0)
                };

                string stateText = dipsError[i] ? "NOK" : "OK";
                Brush stateColor = dipsError[i] ? Brushes.Red : Brushes.LimeGreen;

                TextBlock stateLabel = new TextBlock
                {
                    Text = stateText,
                    Foreground = stateColor,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                dipPanel.Children.Add(led);
                dipPanel.Children.Add(dipLabel);
                dipPanel.Children.Add(stateLabel);

                wrapDips.Children.Add(dipPanel);
            }
        }


        private void UpdateInps()
        {
            wrapInps.Children.Clear();

            for (int i = 0; i < inpsError.Length; i++)
            {
                StackPanel inpPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(10),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                Ellipse led = new Ellipse
                {
                    Width = 40,
                    Height = 40,
                    Fill = inpsError[i] ? Brushes.Red : Brushes.LimeGreen,
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };

                TextBlock inpLabel = new TextBlock
                {
                    Text = $"Entrée {i + 1}",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 0)
                };

                string stateText = inpsError[i] ? "NOK" : "OK";
                Brush stateColor = inpsError[i] ? Brushes.Red : Brushes.LimeGreen;

                TextBlock stateLabel = new TextBlock
                {
                    Text = stateText,
                    Foreground = stateColor,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                inpPanel.Children.Add(led);
                inpPanel.Children.Add(inpLabel);
                inpPanel.Children.Add(stateLabel);

                wrapInps.Children.Add(inpPanel);
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
            try { serialPort.DataReceived -= SerialPort_DataReceived; } catch { }

            // On ouvre le terminal avec le port actif
            terminalWindow = new TerminalWindow(serialPort);
            terminalWindow.Owner = this;

            // Quand le terminal se ferme, on rattache à nouveau la réception principale
            terminalWindow.Closed += (s, args) =>
            {
                try
                {
                    if (serialPort != null && serialPort.IsOpen)
                        serialPort.DataReceived += SerialPort_DataReceived;
                }
                catch { }
                terminalWindow = null;
            };

            terminalWindow.Show();
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

        //Bouton à propos
        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/EnzoPerrier/AppTestEL418",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors de l'ouverture du lien : " + ex.Message);
            }
        }
    }
}
