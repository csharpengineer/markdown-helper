using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using MarkdownHelper.Core;

namespace MarkdownHelper.Windows
{
    /// <summary>Lists the apps that have a window open, with their friendly names.</summary>
    internal static class RunningApps
    {
        public static List<RunningApp> Read()
        {
            var apps = new List<RunningApp>();
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        if (process.MainWindowHandle == IntPtr.Zero) continue;
                        apps.Add(Describe(process));
                    }
                    catch (Exception e) when (e is InvalidOperationException || e is Win32Exception)
                    {
                        // The process exited while being read, so there is nothing to offer.
                    }
                }
            }
            return apps;
        }

        private static RunningApp Describe(Process process)
        {
            try
            {
                var module = process.MainModule;
                return new RunningApp(process.ProcessName, module.FileVersionInfo.FileDescription, module.FileName);
            }
            catch (Exception e) when (e is Win32Exception || e is InvalidOperationException || e is NotSupportedException)
            {
                // Windows hides these details for apps running as administrator.
                return new RunningApp(process.ProcessName, null, null);
            }
        }
    }
}
