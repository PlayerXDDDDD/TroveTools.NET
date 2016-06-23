﻿using log4net;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TroveTools.NET.Framework;
using TroveTools.NET.Model;

namespace TroveTools.NET.DataAccess
{
    static class RegistrySettings
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const string TroveKeyName = @"Software\Classes\trove";
        private const string TroveUrlProtocol = @"HKEY_CURRENT_USER\" + TroveKeyName;
        private const string TroveUrlProtocolOpenCommand = TroveUrlProtocol + @"\shell\open\command";
        private const string TroveUrlProtocolValue = "URL: Trove Protocol";
        private const string UrlProtocolValueName = "URL Protocol";
        private const string OpenCommandFormat = "\"{0}\" \"%1\"";

        private const string WindowsRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string CurrentUserRun = @"HKEY_CURRENT_USER\" + WindowsRun;
        private const string ProductName = "TroveTools .NET";
        private const string RunCommandFormat = "\"{0}\"";

        private const string WinUninstall = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private const string Win64Uninstall = @"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
        private const string WinUninstallTroveLive = WinUninstall + @"\Glyph Trove";
        private const string Win64UninstallTroveLive = Win64Uninstall + @"\Glyph Trove";
        private const string WinUninstallTrovePTS = WinUninstall + @"\Glyph Trove PTS-US";
        private const string Win64UninstallTrovePTS = Win64Uninstall + @"\Glyph Trove PTS-US";
        private const string LocationValue = "InstallLocation";

        public static bool RunAtStartup
        {
            get
            {
                bool startup = false;
                try
                {
                    string value = Registry.GetValue(CurrentUserRun, ProductName, string.Empty) as string;
                    if (value == string.Format(RunCommandFormat, ApplicationDetails.LaunchPath)) startup = true;
                }
                catch (Exception ex) { log.Error("Error getting run at startup setting from Windows Registry", ex); }
                return startup;
            }
            set
            {
                try
                {
                    if (value)
                    {
                        log.Info("Registering startup command in Windows Registry");
                        Registry.SetValue(CurrentUserRun, ProductName, string.Format(RunCommandFormat, ApplicationDetails.LaunchPath));
                    }
                    else
                    {
                        log.Info("Removing startup command from Windows Registry");
                        Registry.CurrentUser.OpenSubKey(WindowsRun, true).DeleteValue(ProductName, false);
                    }
                }
                catch (Exception ex) { log.Error("Error setting run at startup setting in Windows Registry", ex); }
            }
        }

        public static bool IsTroveUrlProtocolRegistered
        {
            get
            {
                bool registered = false;
                try
                {
                    string value = Registry.GetValue(TroveUrlProtocol, "", string.Empty) as string;
                    string urlProtocol = Registry.GetValue(TroveUrlProtocol, UrlProtocolValueName, string.Empty) as string;
                    string openCommand = Registry.GetValue(TroveUrlProtocolOpenCommand, "", string.Empty) as string;
                    string troveToolsOpenCommand = string.Format(OpenCommandFormat, ApplicationDetails.AssemblyLocation);

                    if (value == TroveUrlProtocolValue && urlProtocol != null && openCommand == troveToolsOpenCommand) registered = true;
                }
                catch (Exception ex) { log.Error("Error getting Trove URL Protocol settings from Windows Registry", ex); }

                // Re-register for ClickOnce version updates
                if (!registered && SettingsDataProvider.IsTroveUrlProtocolRegistered)
                {
                    RegisterTroveUrlProtocol();
                    registered = true;
                }
                return registered;
            }
        }

        public static void RegisterTroveUrlProtocol()
        {
            try
            {
                log.Info("Registering Trove URL Protocol in Windows Registry");
                string troveToolsOpenCommand = string.Format(OpenCommandFormat, ApplicationDetails.AssemblyLocation);

                // Remove trove protocol for local machine
                try { Registry.LocalMachine.DeleteSubKeyTree(TroveKeyName, false); }
                catch (Exception ex) { log.Warn("Unable to remove local machine trove URL protocol", ex); }

                // Setup trove protocol for current user 
                Registry.SetValue(TroveUrlProtocol, "", TroveUrlProtocolValue);
                Registry.SetValue(TroveUrlProtocol, UrlProtocolValueName, "");
                Registry.SetValue(TroveUrlProtocolOpenCommand, "", troveToolsOpenCommand);
                SettingsDataProvider.IsTroveUrlProtocolRegistered = true;

                log.Info("Registered Trove URL Protocol in Windows Registry");
            }
            catch (Exception ex) { log.Error("Error registering Trove URL Protocol settings in Windows Registry", ex); }
        }

        public static void UnregisterTroveUrlProtocol()
        {
            try
            {
                log.Info("Removing Trove URL Protocol registration from Windows Registry");

                try { Registry.LocalMachine.DeleteSubKeyTree(TroveKeyName, false); }
                catch (Exception ex) { log.Warn("Unable to remove local machine trove URL protocol", ex); }

                Registry.CurrentUser.DeleteSubKeyTree(TroveKeyName, false);
                SettingsDataProvider.IsTroveUrlProtocolRegistered = false;

                log.Info("Removed Trove URL Protocol registration from Windows Registry");
            }
            catch (Exception ex) { log.Error("Error removing Trove URL Protocol registration from Windows Registry", ex); }
        }

        public static void GetTroveLocations(Dictionary<string, string> potentialLocs)
        {
            try
            {
                string path = Registry.GetValue(Win64UninstallTroveLive, LocationValue, null) as string;
                if (path != null) potentialLocs.AddIfMissing(path, "Trove Live (Registry)");

                path = Registry.GetValue(WinUninstallTroveLive, LocationValue, null) as string;
                if (path != null) potentialLocs.AddIfMissing(path, "Trove Live (Registry)");

                path = Registry.GetValue(Win64UninstallTrovePTS, LocationValue, null) as string;
                if (path != null) potentialLocs.AddIfMissing(path, "Trove PTS (Registry)");

                path = Registry.GetValue(WinUninstallTrovePTS, LocationValue, null) as string;
                if (path != null) potentialLocs.AddIfMissing(path, "Trove PTS (Registry)");
            }
            catch (Exception ex) { log.Error("Error getting Trove locations from Windows Registry", ex); }
        }
    }
}
