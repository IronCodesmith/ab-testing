﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Web;
using EPiServer.Framework;
using EPiServer.Framework.Initialization;

namespace EPiServer.Marketing.Testing.Web
{
    [InitializableModule]
    [ModuleDependency(typeof(EPiServer.Web.InitializationModule))]
    public class TestHandlerInitializer : IInitializableHttpModule
    {
        private TestHandler _testHandler;

        [ExcludeFromCodeCoverage]
        public TestHandlerInitializer()
        {
        }

        [ExcludeFromCodeCoverage]
        public void Initialize(InitializationEngine context)
        {
            _testHandler = new TestHandler();
        }

        [ExcludeFromCodeCoverage]
        public void InitializeHttpEvents(HttpApplication application)
        {
            // We are not actually doing in anything in begin and end request 
            // anymore however leaving this here in case we do. 
            //  application.BeginRequest += BeginRequest;
            //  application.EndRequest += EndRequest;
        }

        [ExcludeFromCodeCoverage]
        private void BeginRequest(object sender, EventArgs e)
        {
        }

        [ExcludeFromCodeCoverage]
        private void EndRequest(object sender, EventArgs e)
        {
        }

        //Interface Requirement but not used.
        [ExcludeFromCodeCoverage]
        public void Uninitialize(InitializationEngine context) { }
    }
}