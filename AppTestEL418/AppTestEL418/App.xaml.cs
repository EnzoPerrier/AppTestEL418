using System.Configuration;
using System.Data;
using System.Windows;

namespace AppTestEL418
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Affiche le splash screen manuellement
            SplashScreen splash = new SplashScreen("Images/splash.png");
            splash.Show(autoClose: false); // on ne le ferme pas tout de suite

            // Retarde le lancement de la MainWindow
            Task.Delay(2000).ContinueWith(_ =>
            {
                // Sur le thread UI
                Dispatcher.Invoke(() =>
                {
                    splash.Close(TimeSpan.FromMilliseconds(300)); // animation douce

                    // Lancer la fenêtre principale
                    MainWindow mainWindow = new MainWindow();
                    mainWindow.Show();
                });
            });
        }
    }

}
