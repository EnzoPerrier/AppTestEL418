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
