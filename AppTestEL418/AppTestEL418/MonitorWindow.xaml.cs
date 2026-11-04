// MonitorWindow.xaml.cs
/*
© 2026 Enzo PERRIER

This code is provided free of charge.

Permission is granted to use this code for personal, non-commercial purposes
only.

The following actions are strictly prohibited:
- Using this code, in whole or in part, for commercial purposes;
- Modifying this code or creating derivative works;
- Redistributing this code, modified or unmodified, without prior written
  permission from the author.

This code is provided "as is", without warranty of any kind, express or implied.
The author shall not be held liable for any damages arising from the use of
this code.
*/

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace AppTestEL418
{
    public partial class MonitorWindow : Window
    {
        private readonly SerialPort port;
        private string rxBuffer = "";
        private bool streaming = false;

        private readonly Stopwatch clock = new Stopwatch();
        private double lastRxSec = 0;
        private readonly DispatcherTimer watchdog;

        private static readonly Brush SelectedBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x86, 0xFF));
        private static readonly Brush NormalBrush = new SolidColorBrush(Color.FromRgb(0x2D, 0x2F, 0x36));

        // Format envoyé par la carte : "INA219: V=12.345 V I=-123.4 mA P=1520 mW [OVF]"
        private static readonly Regex InaRegex = new Regex(
            @"INA219:\s*V=(-?\d+(?:\.\d+)?)\s*V\s+I=(-?\d+(?:\.\d+)?)\s*mA\s+P=(-?\d+)\s*mW(\s+OVF)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>true si le mode stream a été démarré au moins une fois (la carte est alors repartie à l'étape 0)</summary>
        public bool StreamWasUsed { get; private set; } = false;

        public MonitorWindow(SerialPort serialPort)
        {
            InitializeComponent();
            port = serialPort;

            try { port.DiscardInBuffer(); } catch { }
            port.DataReceived += Port_DataReceived;

            watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            watchdog.Tick += Watchdog_Tick;

            SetWindow(60);
            SetStreamingUi(false);
        }

        // ---------------------------------------------------------------- Boutons

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (port == null || !port.IsOpen)
            {
                MessageBox.Show("Le port série n'est pas ouvert !");
                return;
            }

            ClearAll();

            try
            {
                port.DiscardInBuffer();
                rxBuffer = "";
                port.Write("stream_enable\r");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur envoi commande : " + ex.Message);
                return;
            }

            streaming = true;
            StreamWasUsed = true;
            clock.Restart();
            lastRxSec = 0;
            watchdog.Start();
            SetStreamingUi(true);
            SetStatus("⏳ Démarrage... (la carte termine d'abord son étape en cours)", Brushes.Orange);
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            StopStream();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            ClearAll();
            clock.Restart();
            lastRxSec = 0;
        }

        private void BtnWindow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b &&
                double.TryParse((string)b.Tag, NumberStyles.Float, CultureInfo.InvariantCulture, out double s))
            {
                SetWindow(s);
            }
        }

        private void StopStream()
        {
            if (streaming)
            {
                try { if (port != null && port.IsOpen) port.Write("stream_disable\r"); } catch { }
            }
            streaming = false;
            watchdog.Stop();
            SetStreamingUi(false);
            SetStatus("■ Stream arrêté (la carte repart à l'étape 0)", Brushes.Gray);
        }

        // ---------------------------------------------------------------- Réception

        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            string data;
            try { data = port.ReadExisting(); }
            catch { return; }

            if (string.IsNullOrEmpty(data)) return;

            Dispatcher.BeginInvoke(new Action(() => ProcessData(data)));
        }

        private void ProcessData(string data)
        {
            rxBuffer += data;

            // Découpe en lignes (\r, \n ou \r\n)
            int start = 0;
            for (int i = 0; i < rxBuffer.Length; i++)
            {
                if (rxBuffer[i] == '\r' || rxBuffer[i] == '\n')
                {
                    if (i > start)
                        HandleLine(rxBuffer.Substring(start, i - start));
                    start = i + 1;
                }
            }
            rxBuffer = rxBuffer.Substring(start);

            if (rxBuffer.Length > 512) rxBuffer = ""; // sécurité
        }

        private void HandleLine(string line)
        {
            if (!streaming) return;

            lastRxSec = clock.Elapsed.TotalSeconds;

            Match m = InaRegex.Match(line);
            if (m.Success)
            {
                double v = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                double i = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                double p = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                bool ovf = m.Groups[4].Success;

                double t = lastRxSec;
                scopeV.AddPoint(t, v);
                scopeI.AddPoint(t, i);
                scopeP.AddPoint(t, p);

                if (ovf)
                    SetStatus("⚠ OVERFLOW INA219 (courant > 3.2 A ?)", Brushes.Red);
                else
                    SetStatus("● STREAM ACTIF", Brushes.LimeGreen);
                return;
            }

            if (line.IndexOf("STREAM ENABLED", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                SetStatus("● STREAM ACTIF (en attente de la première mesure...)", Brushes.LimeGreen);
            }
            else if (line.IndexOf("INA219", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     line.IndexOf("ERR", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                SetStatus("⚠ " + line.Trim(), Brushes.Red);
            }
        }

        private void Watchdog_Tick(object sender, EventArgs e)
        {
            if (!streaming) return;

            double since = clock.Elapsed.TotalSeconds - lastRxSec;
            double limit = (lastRxSec == 0) ? 8 : 4;

            if (since > limit)
                SetStatus($"⚠ Aucune donnée depuis {since:F0} s (liaison, carte ou INA219 ?)", Brushes.Orange);
        }

        // ---------------------------------------------------------------- Utilitaires UI

        private void ClearAll()
        {
            scopeV.Clear();
            scopeI.Clear();
            scopeP.Clear();
        }

        private void SetWindow(double seconds)
        {
            scopeV.WindowSeconds = seconds;
            scopeI.WindowSeconds = seconds;
            scopeP.WindowSeconds = seconds;

            foreach (Button b in new[] { btnW30, btnW60, btnW300 })
            {
                double s = double.Parse((string)b.Tag, CultureInfo.InvariantCulture);
                b.Background = (s == seconds) ? SelectedBrush : NormalBrush;
            }
        }

        private void SetStreamingUi(bool on)
        {
            btnStart.IsEnabled = !on;
            btnStop.IsEnabled = on;
        }

        private void SetStatus(string text, Brush color)
        {
            txtStatus.Text = text;
            txtStatus.Foreground = color;
        }

        // ---------------------------------------------------------------- Fermeture

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            watchdog.Stop();

            // Désactive le stream sur la carte pour que la machine d'états reprenne
            if (streaming)
            {
                try { if (port != null && port.IsOpen) port.Write("stream_disable\r"); } catch { }
                streaming = false;
            }

            try { port.DataReceived -= Port_DataReceived; } catch { }
        }
    }
}