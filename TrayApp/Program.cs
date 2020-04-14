using CommonLib.Configuration;
using CommonLib.Processes;
using CommonLib.VirtualMachine;
using CommonLib.VirtualMachine.VirtualBox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System;
using System.Linq;
using System.Windows.Forms;
using TrayApp.AutoControl;
using TrayApp.Configuration;
using TrayApp.KeepAwake;
using TrayApp.Logging;
using TrayApp.Menu;
using TrayApp.Menu.Handler;
using TrayApp.VirtualMachine;

namespace TrayApp
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using var serviceProvider = new ServiceCollection()
                .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddNLog("NLog.config.xml"))

                // Application context
                .AddSingleton<TrayApplicationContext>()
                    .AddSingleton<NotifyIconManager>()
                    .AddSingleton<TrayContextMenuStrip>()

                // Menu
                .AddSingleton(provider => new TrayContextMenuStrip(
                    provider.GetService<ILogger<TrayContextMenuStrip>>(),
                    provider.GetServices<IMenuHandler>().OrderBy(m => m.GetSortOrder()).ToArray(),
                    provider.GetService<MachineStoreUpdater>()
                ))
                    .AddSingleton<IMenuHandler, ExitMenuHandler>()
                    .AddSingleton<IMenuHandler, ConfigureMenuHandler>()
                    .AddSingleton<IMenuHandler, MachineControlMenuHandler>()
                        .AddSingleton<IMachineController, VBoxManageOutputFactory>()
                    .AddSingleton<IMenuHandler, KeepAwakeMenuHandler>()
                        .AddSingleton<KeepAwakeTask>()

                // Machine locator
                .AddSingleton<MachineStore>()
                    .AddSingleton<IMachineLocator, VBoxManageOutputFactory>()
                    .AddSingleton<MonitoredMachineFilter>()
                .AddSingleton<MachineStoreUpdater>()
                    .AddSingleton<IUpdateSpeedLocator, MenuVisibleUpdateSpeedLocator>()

                // Configuration
                .AddSingleton<ConfigurationStore>()
                .AddSingleton<IConfigurationFileLocator, UserProfileFileLocator>()
                .AddSingleton<IConfigurationReader, XmlConfigurationReader>()
                .AddSingleton<IConfigurationWriter, XmlConfigurationWriter>()

                .AddSingleton<AutoController>()

                // Tell VBoxManageOutputFactory to use the standard Process output factory
                .AddSingleton<IProcessOutputFactory, ProcessOutputFactory>()

                .BuildServiceProvider();

            // Load the configuration into the store
            var configurationStore = serviceProvider.GetService<ConfigurationStore>();
            configurationStore.UpdateConfiguration();

            // Set the log level from the configuration
            LogLevelConfigurationManager.SetLogLevel(configurationStore.GetConfiguration().LogLevel);

            if (IsAutoStarting())
            {
                var machineStore = serviceProvider.GetService<MachineStore>();
                machineStore.UpdateMachines();

                serviceProvider.GetService<AutoController>().StartMachines();
            }

            // Start the machine state monitor
            serviceProvider.GetService<MachineStoreUpdater>().StartMonitor();

            // Run the application
            Application.Run(serviceProvider.GetService<TrayApplicationContext>());
        }

        private static bool IsAutoStarting()
        {
            return Array.Find(Environment.GetCommandLineArgs(), arg => arg == "--auto-start") != null;
        }
    }
}