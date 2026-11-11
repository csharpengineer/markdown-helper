using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using MarkdownHelper.Core;
using MarkdownHelper.Windows;

namespace MarkdownHelper
{
    public partial class App : Application
    {
        private const string MutexName = @"Local\MarkdownHelper.SingleInstance";
        private const string ShowEventName = @"Local\MarkdownHelper.ShowPreview";
        private const string QuitEventName = @"Local\MarkdownHelper.Quit";

        /// <summary>Tells a running copy to exit, for upgrades and scripts.</summary>
        private const string QuitArgument = "--quit";

        /// <summary>Leaves the "start with Windows" entry alone, for development runs.</summary>
        private const string NoStartupArgument = "--no-startup";

        /// <summary>Shows the popup for a file instead of opening it, for development runs.</summary>
        private const string PopupArgument = "--popup";

        /// <summary>Overrides the settings folder, for development runs.</summary>
        private const string SettingsDirVariable = "MDH_SETTINGS_DIR";

        private Mutex instanceMutex;
        private EventWaitHandle showEvent;
        private RegisteredWaitHandle showWait;
        private EventWaitHandle quitEvent;
        private RegisteredWaitHandle quitWait;
        private AppController controller;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += OnUnhandledError;

            instanceMutex = new Mutex(true, MutexName, out bool firstInstance);
            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            quitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEventName);

            if (!firstInstance)
            {
                if (e.Args.Contains(QuitArgument)) quitEvent.Set();
                else showEvent.Set();
                Shutdown();
                return;
            }

            if (e.Args.Contains(QuitArgument))
            {
                Shutdown();
                return;
            }

            if (e.Args.Contains(DiagnosticLog.Argument)) DiagnosticLog.Start();

            string folder = Environment.GetEnvironmentVariable(SettingsDirVariable);
            string path = string.IsNullOrEmpty(folder) ? SettingsStore.DefaultPath() : Path.Combine(folder, "settings.json");
            bool manageStartup = !e.Args.Contains(NoStartupArgument);

            controller = new AppController(new SettingsStore(path), manageStartup);

            // A second launch signals this one, which then opens the preview.
            showWait = ThreadPool.RegisterWaitForSingleObject(showEvent, (state, timedOut) =>
            {
                Dispatcher.BeginInvoke(new Action(() => controller?.OpenPreviewFromClipboard()));
            }, null, Timeout.Infinite, executeOnlyOnce: false);

            quitWait = ThreadPool.RegisterWaitForSingleObject(quitEvent, (state, timedOut) =>
            {
                Dispatcher.BeginInvoke(new Action(Shutdown));
            }, null, Timeout.Infinite, executeOnlyOnce: true);

            // A markdown file passed on the command line opens directly.
            string file = e.Args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
            if (file != null && File.Exists(file))
            {
                string text = ReadFile(file);
                if (e.Args.Contains(PopupArgument)) controller.DemonstratePopup(text);
                else controller.OpenPreview(text);
                return;
            }

            // The tool lives in the tray. Someone who starts it by hand is told where it went.
            if (!e.Args.Contains(StartupRegistration.BackgroundArgument)) controller.AnnounceRunning();
        }

        private static string ReadFile(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return "";
            }
        }

        /// <summary>A tray tool should survive one bad document, so errors are shown, not fatal.</summary>
        private void OnUnhandledError(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            MessageBox.Show("Something went wrong: " + e.Exception.Message, "Markdown Helper", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            showWait?.Unregister(null);
            quitWait?.Unregister(null);
            controller?.Dispose();
            showEvent?.Dispose();
            quitEvent?.Dispose();
            instanceMutex?.Dispose();
            base.OnExit(e);
        }
    }
}
