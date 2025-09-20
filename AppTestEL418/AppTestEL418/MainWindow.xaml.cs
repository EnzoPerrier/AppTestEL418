using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AppTestEL418
{
    public partial class MainWindow : Window
    {
        private int currentState = 0;

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

        // Images par étape (mets tes images dans un dossier "Images" du projet, Build Action = Resource)
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

        public MainWindow()
        {
            InitializeComponent();
            RefreshPorts();
            UpdateUI();
        }

        private void UpdateUI()
        {
            txtEtape.Text = $"---- {etapeMessages[currentState]} ----";
            txtInstructions.Text = etapeMessages[currentState];

            // Image
            if (currentState < etapeImages.Length)
                imgEtape.Source = new BitmapImage(new Uri(etapeImages[currentState]));

            // Zone PER visible seulement en étape 1
            panelPer.Visibility = (currentState == 1) ? Visibility.Visible : Visibility.Collapsed;
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
                Log("PER envoyé : " + per);
                // TODO: envoyer au port série si ouvert
            }
            else
            {
                MessageBox.Show("Le PER doit contenir 8 chiffres.");
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
            ellipseStatus.Fill = Brushes.Green;
            Log("Port ouvert.");
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            ellipseStatus.Fill = Brushes.Red;
            Log("Port fermé.");
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
    }
}
