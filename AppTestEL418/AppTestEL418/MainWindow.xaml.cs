using System;
using System.IO;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

        // Cellule
        private bool status_test_cel = false; // false = OK et true = NOK

        private readonly string[] etapeMessages =
        {
            "ETAPE 0 : Appuyez sur le bouton pour commencer", // ETAPE 0
            "ETAPE 1 : Entrez le PER (8 digits)", // ETAPE 1
            "ETAPE 2 : Test STS", // ETAPE 2
            "ETAPE 3 : DIPs à OFF", // ETAPE 3
            "ETAPE 4 : DIPs à ON", // ETAPE 4
            "ETAPE 5 : Test entrées à OFF", // ETAPE 5
            "ETAPE 6 : Test entrées à ON", // ETAPE 6
            "ETAPE 7 : Test décompteur", // ETAPE 7
            "ETAPE 8 : Test ampoules", // ETAPE 8
            "ETAPE 9 : Test cellule JOUR", // ETAPE 9
            "ETAPE 10 : Test cellule NUIT", // ETAPE 10
            "ETAPE 11 : Test infrarouge", // ETAPE 11
            "ETAPE 12 : Test accu" // ETAPE 12
        };

        private readonly string[] instructionMessages =
        {
            "Branchez la carte, alimentez le banc en 12.5V et appuyez ensuite sur le BP valider pour commencer", // ETAPE 0
            "Mesurez la base de temps à l'aide du fréquencemètre, reportez la dans la zone de texte ci-dessous\nSi la base de temps commence par 99xxxxx et ne contient que 7 digits, ajoutez un '0' à la fin (ex: 99999980)", // ETAPE 1
            null, // ETAPE 2
            "Mettez tous les DIPs à OFF et appuyez sur le BP valider du banc de test", // ETAPE 3
            "Mettez tous les DIPs à ON et appuyer sur le BP reset de la carte, ensuite appuyez sur le BP valider du banc de test", // ETAPE 4
            "Test auto des entrées à OFF en cours ...", // ETAPE 5
            "Test auto des entrées à ON en cours ...", // ETAPE 6
            "Vérifiez que toutes les LEDs du décompteur s'allument correctement à la bonne luminosité", // ETAPE 7
            "Vérifiez que tous les défauts ampoules apparaissent les uns après les autres, ensuite appuyez sur le bouton valider si OK", // ETAPE 8
            "Test cellule JOUR en cours, appuyez sur le BP valider une fois la cellule exposée à la lumière", // ETAPE 9
            "Test cellule NUIT en cours, appuyez sur le BP valider une fois la cellule exposée à l'obscurité", // ETAPE 10
            "Vérifiez l'IR en utilisant la télécommande", // ETAPE 11
            "Vérifiez que le message 'supression de batterie' s'affiche à l'écran LCD" // ETAPE 12
        };

        private readonly string[] etapeImages =
        {
            "pack://application:,,,/Images/etape0.png", // ETAPE 0
            "pack://application:,,,/Images/etape1.png", // ETAPE 1
            null, // ETAPE 2
            null, // ETAPE 3
            null, // ETAPE 4
            null, // ETAPE 5
            null, // ETAPE 6
            null, // ETAPE 7
            null, // ETAPE 8
            null,//"pack://application:,,,/Images/CEL_JOUR.png", // ETAPE 9
            null,//"pack://application:,,,/Images/CEL_NUIT.png", // ETAPE 10
            null, // ETAPE 11
            "pack://application:,,,/Images/etape12.jpg" // ETAPE 12
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
            panelCelJour.Visibility = (currentState == 9) ? Visibility.Visible : Visibility.Collapsed;
            panelCelNuit.Visibility = (currentState == 10) ? Visibility.Visible : Visibility.Collapsed;

            // Lorsque l'on entre sur les étapes DIPs on initialise en NOK (attente de la carte)
            if (currentState == 3 || currentState == 4)
            {
                for (int i = 0; i < dipsError.Length; i++)
                {
                    dipsError[i] = false;      // NOK par défaut
                    dipsPhysical[i] = false;  // valeur inconnue => OFF affiché
                }
                UpdateDips();
            }

            // Même principe pour INPs
            if (currentState == 5 || currentState == 6)
            {
                for (int i = 0; i < inpsError.Length; i++)
                {
                    inpsError[i] = false;
                    inpsPhysical[i] = false;
                }
                UpdateInps();
            }
            
            // TST (Test décompteur Etape 7)
            if(currentState == 7)
            {
                TST_LED_Anim.Visibility = Visibility.Visible;
            }else TST_LED_Anim.Visibility=Visibility.Collapsed;

            // TST (Test décompteur Etape 7)
            if (currentState == 8)
            {
                OPT_Anim.Visibility = Visibility.Visible;
            }
            else OPT_Anim.Visibility = Visibility.Collapsed;

            // Lorsque l'on entre sur les étapes CEL on initialise en OK (attente de la carte)
            if (currentState == 9 || currentState == 10)
            {
                UpdateCel(false);
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
                    try { serialPort.Write(message); Log("PER envoyé : " + message); }
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
                        //Log($"[DEBUG]Changement d'étape automatique : {step}"); //Debug
                    }
                }
            }

            // --- Gestion DIPs ---
            if ((currentState == 3 || currentState == 4) && cleanedMessage.Contains("DIP", StringComparison.OrdinalIgnoreCase))
            {
                bool attenduOn = (currentState == 4); // Étape 3 = OFF attendu ; Étape 4 = ON attendu

                // Ne pas tout réinitialiser à chaque ligne : on ne modifie que les DIPs concernés
                var matches = Regex.Matches(cleanedMessage, @"DIP\s*(\d+)\s*a\s*(ON|OFF)", RegexOptions.IgnoreCase);
                foreach (Match m in matches)
                {
                    if (int.TryParse(m.Groups[1].Value, out int dipNum) && dipNum >= 1 && dipNum <= dipsError.Length)
                    {
                        bool isOn = m.Groups[2].Value.Equals("ON", StringComparison.OrdinalIgnoreCase);
                        bool isError = (isOn != attenduOn);

                        dipsPhysical[dipNum - 1] = isOn;   // état réel lu
                        dipsError[dipNum - 1] = isError;   // erreur ou non

                        //Log($"[DEBUG] DIP {dipNum}: {(isOn ? "ON" : "OFF")} attendu {(attenduOn ? "ON" : "OFF")} → {(isError ? "NOK" : "OK")}");
                    }
                }

                // Cas global : " --> OK" → tout est bon
                if (cleanedMessage.Contains("--> OK", StringComparison.OrdinalIgnoreCase))
                {
                    for (int i = 0; i < dipsError.Length; i++)
                    {
                        dipsError[i] = false;
                        dipsPhysical[i] = attenduOn;
                    }
                    //Log("[DEBUG] Tous les DIPs conformes");
                }
                
                UpdateDips();
                Task.Delay(500);
            }




            // --- Gestion Entrées ---
            if ((currentState == 5 || currentState == 6) && cleanedMessage.Contains("IN", StringComparison.OrdinalIgnoreCase))
            {
                bool attenduOn = (currentState == 6); // Étape 5 = OFF attendu ; Étape 6 = ON attendu

                // Cas générique : "ERROR: IN X a ON" / "OK: IN X a OFF"
                var matches = Regex.Matches(cleanedMessage, @"IN\s*(\d+)\s*a\s*(ON|OFF)", RegexOptions.IgnoreCase);
                foreach (Match m in matches)
                {
                    if (int.TryParse(m.Groups[1].Value, out int inpNum))
                    {
                        bool isOn = m.Groups[2].Value.Equals("ON", StringComparison.OrdinalIgnoreCase);
                        bool isError = (isOn != attenduOn);
                        if (inpNum >= 1 && inpNum <= inpsError.Length)
                            inpsError[inpNum - 1] = isError;

                        //Log($"[DEBUG] IN {inpNum}: {(isOn ? "ON" : "OFF")} attendu {(attenduOn ? "ON" : "OFF")} → {(isError ? "NOK" : "OK")}"); //DEBUG
                    }
                }

                // Cas global : "--> OK"
                if (cleanedMessage.Contains("--> OK", StringComparison.OrdinalIgnoreCase))
                {
                    for (int i = 0; i < inpsError.Length; i++)
                        inpsError[i] = false;
                    //Log("[DEBUG] Toutes les IN conformes"); //DEBUG
                }

                UpdateInps();
                Task.Delay(500);
            }

            // --- Test CEL JOUR & NUIT---
            if (currentState == 9 ||currentState == 10)
            {
                if (cleanedMessage.Contains("ERROR:", StringComparison.OrdinalIgnoreCase))
                {
                    status_test_cel = true; 

                }
                else if(cleanedMessage.Contains("--> OK", StringComparison.OrdinalIgnoreCase))
                {
                    status_test_cel = false;
                }

                UpdateCel(status_test_cel);
            }

            // --- Test STS ---
            if (currentState ==  3 && cleanedMessage.Contains("STS", StringComparison.OrdinalIgnoreCase)) // Si STS OK
            {
                UpdateSTS(cleanedMessage);
            }

            if(currentState == 8)
            {
                if(cleanedMessage.Contains("OPTR", StringComparison.OrdinalIgnoreCase))
                {
                    OptFullOn.Visibility = Visibility.Collapsed;
                    OptRouge.Visibility = Visibility.Visible;
                    OptOrange.Visibility = Visibility.Collapsed;
                    OptVert.Visibility = Visibility.Collapsed;
                }

                else if (cleanedMessage.Contains("OPTY", StringComparison.OrdinalIgnoreCase))
                {
                    OptFullOn.Visibility = Visibility.Collapsed;
                    OptRouge.Visibility = Visibility.Collapsed;
                    OptOrange.Visibility = Visibility.Visible;
                    OptVert.Visibility = Visibility.Collapsed;
                }

                else if (cleanedMessage.Contains("OPTG", StringComparison.OrdinalIgnoreCase))
                {
                    OptFullOn.Visibility = Visibility.Collapsed;
                    OptRouge.Visibility = Visibility.Collapsed;
                    OptOrange.Visibility = Visibility.Collapsed;
                    OptVert.Visibility = Visibility.Visible;
                }

                else if (cleanedMessage.Contains("OPTFULL", StringComparison.OrdinalIgnoreCase))
                {
                    OptFullOn.Visibility = Visibility.Visible;
                    OptRouge.Visibility = Visibility.Collapsed;
                    OptOrange.Visibility = Visibility.Collapsed;
                    OptVert.Visibility = Visibility.Collapsed;
                }
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
            LoadingBarSTS.Visibility = Visibility.Collapsed;
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
                    Width = 50,
                    Height = 50,
                    Fill = dipsError[i] ? Brushes.Red : Brushes.LimeGreen,
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };

                TextBlock dipLabel = new TextBlock
                {
                    Text = $"DIP {i + 1}",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 15,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 0)
                };

                string stateText = dipsError[i] ? "NOK" : "OK";
                Brush stateColor = dipsError[i] ? Brushes.Red : Brushes.LimeGreen;

                TextBlock stateLabel = new TextBlock
                {
                    Text = stateText,
                    Foreground = stateColor,
                    FontSize = 13,
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
                    Width = 50,
                    Height = 50,
                    Fill = inpsError[i] ? Brushes.Red : Brushes.LimeGreen,
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };

                TextBlock inpLabel = new TextBlock
                {
                    Text = $"Entrée {i + 1}",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 15,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 0)
                };

                string stateText = inpsError[i] ? "NOK" : "OK";
                Brush stateColor = inpsError[i] ? Brushes.Red : Brushes.LimeGreen;

                TextBlock stateLabel = new TextBlock
                {
                    Text = stateText,
                    Foreground = stateColor,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                inpPanel.Children.Add(led);
                inpPanel.Children.Add(inpLabel);
                inpPanel.Children.Add(stateLabel);

                wrapInps.Children.Add(inpPanel);
            }
        }

        
        private void UpdateCel(bool test_status)
        {
            if(currentState == 9)
            {
                wrapCelJour.Children.Clear();
                StackPanel panelCelJour = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(10),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                Ellipse led = new Ellipse
                {
                    Width = 80,
                    Height = 80,
                    Fill = test_status ? Brushes.Red : Brushes.Yellow,
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };

                TextBlock celJLabel = new TextBlock
                {
                    Text = $"Cellule en Jour",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 0)
                };

                string stateText = test_status ? "NOK" : "OK";
                Brush stateColor = test_status ? Brushes.Red : Brushes.LimeGreen;

                TextBlock stateLabel = new TextBlock
                {
                    Text = stateText,
                    Foreground = stateColor,
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                panelCelJour.Children.Add(led);
                panelCelJour.Children.Add(celJLabel);
                panelCelJour.Children.Add(stateLabel);

                wrapCelJour.Children.Add(panelCelJour);

            }
            else if (currentState == 10)
            {
                wrapCelNuit.Children.Clear();
                StackPanel panelCelNuit = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(10),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                Ellipse led = new Ellipse
                {
                    Width = 80,
                    Height = 80,
                    Fill = test_status ? Brushes.Red : Brushes.DarkBlue,
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };

                TextBlock CelNLabel = new TextBlock
                {
                    Text = $"Cellule en Nuit",
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 0)
                };

                string stateText = test_status ? "NOK" : "OK";
                Brush stateColor = test_status ? Brushes.Red : Brushes.LimeGreen;

                TextBlock stateLabel = new TextBlock
                {
                    Text = stateText,
                    Foreground = stateColor,
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                panelCelNuit.Children.Add(led);
                panelCelNuit.Children.Add(CelNLabel);
                panelCelNuit.Children.Add(stateLabel);

                wrapCelNuit.Children.Add(panelCelNuit);

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
