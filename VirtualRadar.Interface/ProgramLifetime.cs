﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using InterfaceFactory;
using VirtualRadar.Interface.BaseStation;
using VirtualRadar.Interface.Listener;
using VirtualRadar.Interface.Network;
using VirtualRadar.Interface.View;
using VirtualRadar.Interface.WebServer;
using VirtualRadar.Localisation;

namespace VirtualRadar.Interface
{
    /// <summary>
    /// Common methods for managing the program's lifetime from a variety of different front ends.
    /// </summary>
    /// <remarks>
    /// <para>When starting the program up the following calls should be made in this order:</para>
    /// <list type="number">
    /// <item>InitialiseUnhandledExceptionHandling (* - do this as early as you can!)</item>
    /// <item>PrepassCommandLineArgs (if you don't call this then set the <see cref="ProgramLifetime"/>
    /// properties as required before initialising the class factory)</item>
    /// <item>-- Initialise the class factory, there's no standard method for this --</item>
    /// <item>InitialiseManagers (*)</item>
    /// <item>LoadPlugins (*)</item>
    /// <item>SingleInstanceStart -- or -- CreateSingleInstanceMutex and/or StartApplication</item>
    /// </list>
    /// <para>(*) = mandatory</para>
    /// <para>
    /// Once <see cref="SingleInstanceStart"/> or <see cref="StartApplication"/> have been called the <see
    /// cref="MainView"/> property will be non-null. Both of these methods will block until the user indicates
    /// that they want to close the program. They will only return control to the caller after the program has
    /// been fully shut down.
    /// </para>
    /// </remarks>
    public static class ProgramLifetime
    {
        /// <summary>
        /// The name of the mutex that we use to ensure only one instance will run.
        /// </summary>
        private const string _SingleInstanceMutexName = "VirtualRadarServer-SJKADBK42348J";

        /// <summary>
        /// Gets or sets the culture info that was forced into use by the -culture command-line switch.
        /// </summary>
        public static CultureInfo ForcedCultureInfo { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating that fonts should not be replaced.
        /// </summary>
        public static bool DisableFontReplacement { get; set; }

        /// <summary>
        /// Gets or sets a value indicating that the program is running in headless mode.
        /// </summary>
        public static bool Headless { get; set; }

        /// <summary>
        /// Gets or sets a value indicating that this instance of the program should not check to make sure
        /// that it's the only one running.
        /// </summary>
        public static bool AllowMultipleInstances { get; set; }

        /// <summary>
        /// Gets the main view that was created for the application.
        /// </summary>
        public static IMainView MainView { get; private set; }

        /// <summary>
        /// Registers event handlers with .NET that will be called when unhandled exceptions are thrown.
        /// </summary>
        public static void InitialiseUnhandledExceptionHandling()
        {
            Application.ThreadException += new System.Threading.ThreadExceptionEventHandler(Application_ThreadException);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
        }

        /// <summary>
        /// Called when an unhandled exception was caught for the GUI thread.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            ShowException(e.Exception);
        }

        /// <summary>
        /// Called when an unhandled exception was caught for any thread.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Don't translate, I don't want to hide errors if the translation throws exceptions
            Exception ex = e.ExceptionObject as Exception;
            if(ex != null) {
                ShowException(ex);
            } else {
                Factory.Singleton.Resolve<IMessageBox>().Show(String.Format("An exception that was not of type Exception was caught.\r\n{0}", e.ExceptionObject), "Unknown Exception Caught");
            }
        }

        /// <summary>
        /// Shows the details of an exception to the user and logs it.
        /// </summary>
        /// <param name="ex"></param>
        public static void ShowException(Exception ex)
        {
            // Don't translate, I don't want to confuse things if the translation throws exceptions

            var isThreadAbort = Hierarchy.Flatten(ex, r => r.InnerException).Any(r => r is ThreadAbortException);
            if(!isThreadAbort) {
                var message = Describe.ExceptionMultiLine(ex, "\r\n");

                ILog log = null;
                try {
                    log = Factory.Singleton.Resolve<ILog>().Singleton;
                    if(log != null) {
                        log.WriteLine(message);
                    }
                } catch { }

                try {
                    Factory.Singleton.Resolve<IMessageBox>().Show(message, "Unhandled Exception Caught");
                } catch(Exception doubleEx) {
                    Debug.WriteLine(String.Format("Program.ShowException caught double-exception: {0} when trying to display / log {1}", doubleEx.ToString(), ex.ToString()));
                    try {
                        if(log != null) {
                            log.WriteLine("Caught exception while trying to show a previous exception: {0}", doubleEx.ToString());
                        }
                    } catch { }
                }
            }
        }

        /// <summary>
        /// Performs a pass through the command-line arguments to see if there's anything that needs to be
        /// handled at program start-up.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        public static void PrepassCommandLineArgs(string[] args)
        {
            if(args != null) {
                foreach(string arg in args) {
                    if(arg.ToUpper().StartsWith("-CULTURE:")) {
                        ForcedCultureInfo = new CultureInfo(arg.Substring(9));
                        Thread.CurrentThread.CurrentUICulture = ForcedCultureInfo;
                        Thread.CurrentThread.CurrentCulture = ForcedCultureInfo;
                    } else if(arg.ToUpper() == "-DEFAULTFONTS") {
                        DisableFontReplacement = true;
                    } else if(arg.ToUpper() == "-NOGUI") {
                        Headless = true;
                    } else if(arg.ToUpper().StartsWith("-WORKINGFOLDER:")) {
                        AllowMultipleInstances = true;
                    }
                }
            }
        }

        /// <summary>
        /// Initialises manager singletons. Note that this should be called before plugins are loaded, which
        /// means that plugins will not be able to provide their own implementations of the managers
        /// initialised here.
        /// </summary>
        public static void InitialiseManagers()
        {
            var receiverFormatManager = Factory.Singleton.Resolve<IReceiverFormatManager>().Singleton;
            receiverFormatManager.Initialise();

            var rebroadcastFormatManager = Factory.Singleton.Resolve<IRebroadcastFormatManager>().Singleton;
            rebroadcastFormatManager.Initialise();
        }

        /// <summary>
        /// Loads the plugins.
        /// </summary>
        public static void LoadPlugins()
        {
            var pluginManager = Factory.Singleton.Resolve<IPluginManager>().Singleton;
            pluginManager.LoadPlugins();
        }

        /// <summary>
        /// Starts the program in single-instance mode throwing an exception if there's already a running
        /// instance. If <see cref="AllowMultipleInstances"/> is set then the single-instance check is skipped
        /// but the application is still spun up.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        public static void SingleInstanceStart(string[] args)
        {
            bool mutexAcquired;
            using(var singleInstanceMutex = CreateSingleInstanceMutex(out mutexAcquired, AllowMultipleInstances)) {
                try {
                    StartApplication(args);
                } finally {
                    if(mutexAcquired) {
                        singleInstanceMutex.ReleaseMutex();
                    }
                }
            }
        }

        /// <summary>
        /// Creates the mutex that will be held for the duration of the application. The mutex is used to prevent
        /// multiple instances from running. Quits the program if another instance is seen.
        /// </summary>
        /// <returns></returns>
        public static Mutex CreateSingleInstanceMutex(out bool mutexAcquired, bool allowMultipleInstances)
        {
            mutexAcquired = false;
            var result = new Mutex(false, _SingleInstanceMutexName);

            var runtimeEnvironment = Factory.Singleton.Resolve<IRuntimeEnvironment>().Singleton;
            if(!runtimeEnvironment.IsMono) {
                var allowEveryoneRule = new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), MutexRights.FullControl, AccessControlType.Allow);
                var securitySettings = new MutexSecurity();
                securitySettings.AddAccessRule(allowEveryoneRule);
                result.SetAccessControl(securitySettings);

                if(!allowMultipleInstances) {
                    try {
                        mutexAcquired = result.WaitOne(1000, false);
                        if(!mutexAcquired) {
                            Factory.Singleton.Resolve<IMessageBox>().Show(Strings.AnotherInstanceRunningFull, Strings.AnotherInstanceRunningTitle);
                            Environment.Exit(1);
                        }
                    } catch(AbandonedMutexException) { }
                }
            }

            return result;
        }

        /// <summary>
        /// Displays the splash screen (whose presenter builds up most of the objects used by the program) and then the
        /// main view once the splash screen has finished. When the main view quits the program shuts down.
        /// </summary>
        /// <param name="args"></param>
        public static void StartApplication(string[] args)
        {
            IUniversalPlugAndPlayManager uPnpManager = null;
            IBaseStationAircraftList baseStationAircraftList = null;
            ISimpleAircraftList flightSimulatorXAircraftList = null;
            bool loadSucceded = false;

            using(var splashScreen = Factory.Singleton.Resolve<ISplashView>()) {
                splashScreen.Initialise(args, BackgroundThread_ExceptionCaught);
                splashScreen.ShowView();

                loadSucceded = splashScreen.LoadSucceeded;
                uPnpManager = splashScreen.UPnpManager;
                baseStationAircraftList = splashScreen.BaseStationAircraftList;
                flightSimulatorXAircraftList = splashScreen.FlightSimulatorXAircraftList;
            }

            var shutdownSignalHandler = Factory.Singleton.Resolve<IShutdownSignalHandler>().Singleton;
            try {
                if(loadSucceded) {
                    var pluginManager = Factory.Singleton.Resolve<IPluginManager>().Singleton;
                    foreach(var plugin in pluginManager.LoadedPlugins) {
                        try {
                            plugin.GuiThreadStartup();
                        } catch(Exception ex) {
                            var log = Factory.Singleton.Resolve<ILog>().Singleton;
                            log.WriteLine($"Caught exception in {plugin.Name} plugin while calling GuiThreadStartup: {ex}");
                        }
                    }

                    try {
                        using(var mainWindow = Factory.Singleton.Resolve<IMainView>()) {
                            MainView = mainWindow;
                            mainWindow.Initialise(uPnpManager, flightSimulatorXAircraftList);

                            shutdownSignalHandler.CloseMainViewOnShutdownSignal();

                            mainWindow.ShowView();
                        }
                    } finally {
                        MainView = null;
                    }
                }
            } finally {
                shutdownSignalHandler.Cleanup();

                using(var shutdownWindow = Factory.Singleton.Resolve<IShutdownView>()) {
                    shutdownWindow.Initialise(uPnpManager, baseStationAircraftList);
                    shutdownWindow.ShowView();
                    Thread.Sleep(1000);
                }

                Factory.Singleton.Resolve<ILog>().Singleton.WriteLine("Clean shutdown complete");
            }
        }

        /// <summary>
        /// Called when objects that utilise background threads catch an exception on that background thread. Note
        /// that this is almost certainly called from a non-GUI thread.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private static void BackgroundThread_ExceptionCaught(object sender, EventArgs<Exception> args)
        {
            if(MainView != null) {
                MainView.BubbleExceptionToGui(args.Value);
            } else {
                var log = Factory.Singleton.Resolve<ILog>().Singleton;
                log.WriteLine("Unhandled exception caught in BaseStationAircraftList before GUI available to show to user: {0}", args.Value.ToString());
            }
        }
    }
}
