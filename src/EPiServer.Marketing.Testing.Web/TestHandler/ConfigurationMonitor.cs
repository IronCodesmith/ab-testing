﻿using EPiServer.Marketing.Testing.Core.Manager;
using EPiServer.Marketing.Testing.Web.Config;
using EPiServer.ServiceLocation;

namespace EPiServer.Marketing.Testing.Web
{
    /// <summary>
    /// Contains methods to monitor configuration changes and apply them to all the remote nodes.
    /// </summary>
    public class ConfigurationMonitor : IConfigurationMonitor
    {
        private IServiceLocator serviceLocator;
        private ICacheSignal cacheSignal;

        /// <summary>
        /// Default
        /// </summary>
        /// <param name="serviceLocator"></param>
        /// <param name="cacheSignal"></param>
        public ConfigurationMonitor(IServiceLocator serviceLocator, ICacheSignal cacheSignal)
        {
            this.serviceLocator = serviceLocator;
            this.cacheSignal = cacheSignal;

            HandleConfigurationChange();
            this.cacheSignal.Monitor(HandleConfigurationChange);
        }

        /// <summary>
        /// Enables or disables AB testing based on config changes.
        /// </summary>
        public void HandleConfigurationChange()
        {
            var testHandler = serviceLocator.GetInstance<ITestHandler>();
            var testManager = serviceLocator.GetInstance<ITestManager>();

            if (AdminConfigTestSettings.Current.IsEnabled && testManager.GetActiveTests().Count >= 1)
            {
                testHandler.EnableABTesting();
                this.cacheSignal.Reset();
            }
            else
            {
                testHandler.DisableABTesting();
                this.cacheSignal.Reset();
            }

            this.cacheSignal.Set();
        }
    }
}