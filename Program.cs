using System;
using System.Threading;
using System.Windows.Forms;
using TechnoVerseLoader.UI;

namespace TechnoVerseLoader
{
    internal static class Program
    {
        private static Mutex? _mutex;

        [STAThread]
        static void Main()
        {
            // Start anti-debugging and anti-crack protection
            TechnoVerseLoader.Services.AntiDebugService.StartMonitoring();

            const string appGuid = "TechnoVerse-Client-Loader-9A7B3E2F-4D1C";
            _mutex = new Mutex(true, appGuid, out bool createdNew);

            if (!createdNew)
            {
                MessageBox.Show(
                    "TechnoVerse Loader is already running! Please check your system tray.",
                    "TechnoVerse Loader",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return;
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    MessageBox.Show(
                        "An unexpected error occurred:\n\n" + ex.Message,
                        "TechnoVerse Loader Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            };

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());

            GC.KeepAlive(_mutex);
        }
    }
}
