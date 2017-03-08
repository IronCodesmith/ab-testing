﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using EPiServer.Core;
using EPiServer.ServiceLocation;
using System.Linq;
using Castle.Core.Internal;
using EPiServer.Marketing.Testing.Core.DataClass;
using EPiServer.Marketing.Testing.Web.Helpers;
using EPiServer.Marketing.KPI.Manager.DataClass;
using EPiServer.Logging;
using System.Web;
using EPiServer.Marketing.KPI.Common.Attributes;
using EPiServer.Marketing.KPI.Results;
using EPiServer.Marketing.Testing.Core.DataClass.Enums;
using EPiServer.Data;
using EPiServer.Marketing.Testing.Core.Manager;
using EPiServer.Web.Routing;
using System.IO;
using System.Text;
using EPiServer.Marketing.KPI.Manager;
using Newtonsoft.Json;

namespace EPiServer.Marketing.Testing.Web
{
    [ServiceConfiguration(ServiceType = typeof(ITestHandler), Lifecycle = ServiceInstanceScope.Singleton)]
    internal class TestHandler : ITestHandler
    {
        private readonly IServiceLocator _serviceLocator;
        private readonly ITestingContextHelper _contextHelper;
        private readonly ITestDataCookieHelper _testDataCookieHelper;
        private readonly ILogger _logger;
        private readonly ITestManager _testManager;
        private readonly DefaultMarketingTestingEvents _marketingTestingEvents;
        /// Used to keep track of how many times for the same service/event we add the proxy event handler
        private readonly IReferenceCounter _ReferenceCounter = new ReferenceCounter();

        /// <summary>
        /// HTTPContext flag used to skip AB Test Processing in LoadContent event handler.
        /// </summary>
        public const string ABTestHandlerSkipFlag = "ABTestHandlerSkipFlag";
        public const string SkipRaiseContentSwitchEvent = "SkipRaiseContentSwitchEvent";
        public const string ABTestHandlerSkipKpiEval = "ABTestHandlerSkipKpiEval";

        [ExcludeFromCodeCoverage]
        public TestHandler()
        {
            _serviceLocator = ServiceLocator.Current;

            _testDataCookieHelper = new TestDataCookieHelper();
            _contextHelper = new TestingContextHelper();
            _logger = LogManager.GetLogger();

            _testManager = _serviceLocator.GetInstance<ITestManager>();
            _marketingTestingEvents = _serviceLocator.GetInstance<DefaultMarketingTestingEvents>();

            // Setup our content events
            var contentEvents = _serviceLocator.GetInstance<IContentEvents>();
            contentEvents.LoadedChildren += LoadedChildren;
            contentEvents.LoadedContent += LoadedContent;
            contentEvents.DeletedContent += ContentEventsOnDeletedContent;
            contentEvents.DeletingContentVersion += ContentEventsOnDeletingContentVersion;

            initProxyEventHandler();
        }

        //To support unit testing
        internal TestHandler(IServiceLocator serviceLocator)
        {
            _serviceLocator = serviceLocator;

            _testDataCookieHelper = serviceLocator.GetInstance<ITestDataCookieHelper>();
            _contextHelper = serviceLocator.GetInstance<ITestingContextHelper>();
            _logger = serviceLocator.GetInstance<ILogger>();
            _testManager = serviceLocator.GetInstance<ITestManager>();
            _marketingTestingEvents = serviceLocator.GetInstance<DefaultMarketingTestingEvents>();

            IReferenceCounter rc = serviceLocator.GetInstance<IReferenceCounter>();
            _ReferenceCounter = rc;
        }

        /// <summary>
        /// need this for deleted drafts as they are permanently deleted and do not go to the trash
        /// the OnDeletedContentVersion event is too late to get the guid to see if it is part of a test or not.
        /// Excluding from coverage as CheckForActiveTest is tested separately and the rest of this would be mocked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="contentEventArgs"></param>
        [ExcludeFromCodeCoverage]
        internal void ContentEventsOnDeletingContentVersion(object sender, ContentEventArgs contentEventArgs)
        {
            var repo = _serviceLocator.GetInstance<IContentRepository>();

            IContent draftContent;

            // get the actual content item so we can get its Guid to check against our tests
            if (repo.TryGet(contentEventArgs.ContentLink, out draftContent))
            {
                CheckForActiveTests(draftContent.ContentGuid, contentEventArgs.ContentLink.WorkID);
            }
        }

        /// <summary>
        /// need this for deleted published pages, this is called when the trash is emptied
        /// Excluding from coverage as CheckForActiveTest is tested separately and the rest of this would be mocked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="deleteContentEventArgs"></param>
        [ExcludeFromCodeCoverage]
        internal void ContentEventsOnDeletedContent(object sender, DeleteContentEventArgs deleteContentEventArgs)
        {
            // this is the list of pages that are being deleted from the trash.  All we have is the guid, at this point in time
            // the items already seem to be gone.  Luckily all we need is the guid as this only fires for published pages.
            var guids = (List<Guid>)deleteContentEventArgs.Items["DeletedItemGuids"];

            foreach (var guid in guids)
            {
                CheckForActiveTests(guid, 0);
            }
        }

        /// <summary>
        /// Check the guid passed in to see if the page/draft is part of a test.  For published pages, the version passed in will be 0, as all we need/get is the guid
        /// for drafts, we the guid and version will be passed in to compare against known variants being tested.
        /// </summary>
        /// <param name="contentGuid">Guid of item being deleted.</param>
        /// <param name="contentVersion">0 if published page, workID if draft</param>
        /// <returns>Number of active tests that were deleted from the system.</returns>
        internal int CheckForActiveTests(Guid contentGuid, int contentVersion)
        {
            var testsDeleted = 0;
            var tests = _testManager.GetActiveTestsByOriginalItemId(contentGuid);

            // no tests found for the deleted content
            if (tests.IsNullOrEmpty())
            {
                return testsDeleted;
            }

            foreach (var test in tests)
            {
                // the published page is being deleted
                if (contentVersion == 0)
                {
                    _testManager.Stop(test.Id);
                    _testManager.Delete(test.Id);
                    testsDeleted++;
                    continue;
                }

                // a draft version of a page is being deleted
                if (test.Variants.All(v => v.ItemVersion != contentVersion))
                    continue;

                _testManager.Stop(test.Id);
                _testManager.Delete(test.Id);
                testsDeleted++;
            }
            return testsDeleted;
        }

        /// <summary>
        /// Event handler to swap out content when children are loaded, however this does not
        /// cause a conversion or view, simply creates cookie if needed and swaps content
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void LoadedChildren(object sender, ChildrenEventArgs e)
        {
            if (!_contextHelper.SwapDisabled(e))
            {
                Boolean modified = false;
                IList<IContent> childList = new List<IContent>();

                EvaluateCookies();

                // its possible that something in the children changed, so we need to replace it with a variant 
                // if its in test. This method gets called once after the main page is loaded. (i.e. this is how
                // the links at the top of alloy get created)
                foreach (var content in e.ChildrenItems)
                {
                    try
                    {
                        // get the test from the cache
                        var activeTest = _testManager.GetActiveTestsByOriginalItemId(content.ContentGuid).FirstOrDefault();
                        if (activeTest != null)
                        {
                            var testCookieData = _testDataCookieHelper.GetTestDataFromCookie(content.ContentGuid.ToString());
                            var hasData = _testDataCookieHelper.HasTestData(testCookieData);

                            if (!hasData && DbReadWrite())
                            {
                                // Make sure the cookie has data in it. There are cases where you can load
                                // content directly from a url after opening a browser and if the cookie is not set
                                // the first pass through you end up seeing original content not content under test.
                                SetTestData(content, activeTest, testCookieData, out testCookieData);
                            }

                            if (testCookieData.ShowVariant && _testDataCookieHelper.IsTestParticipant(testCookieData))
                            {
                                modified = true;
                                childList.Add(_testManager.GetVariantContent(content.ContentGuid));
                            }
                            else
                            {
                                childList.Add(content);
                            }
                        }
                        else
                        {
                            childList.Add(content);
                        }
                    }
                    catch (Exception err)
                    {
                        _logger.Error("TestHandler.LoadChildren", err);
                    }
                }

                // if we modified the data, update the children list. Note that original order
                // is important else links do not show up in same order.
                if (modified)
                {
                    e.ChildrenItems.Clear();
                    e.ChildrenItems.AddRange(childList);
                }
            }
        }

        /// Main worker method.  Processes each content which triggers a
        /// content loaded event to determine the state of a test and what content to display.
        public void LoadedContent(object sender, ContentEventArgs e)
        {
            if (!_contextHelper.SwapDisabled(e))
            {
                try
                {
                    EvaluateCookies();

                    // get the test from the cache
                    var activeTest = _testManager.GetActiveTestsByOriginalItemId(e.Content.ContentGuid).FirstOrDefault();
                    if (activeTest != null)
                    {
                        var testCookieData = _testDataCookieHelper.GetTestDataFromCookie(e.Content.ContentGuid.ToString());
                        var hasData = _testDataCookieHelper.HasTestData(testCookieData);
                        var originalContent = e.Content;

                        // Preload the cache if needed. Note that this causes an extra call to loadContent Event
                        // so set the skip flag so we dont try to process the test.
                        HttpContext.Current.Items[ABTestHandlerSkipFlag] = true;
                        _testManager.GetVariantContent(e.Content.ContentGuid);
                        if (!hasData && DbReadWrite())
                        {
                            // Make sure the cookie has data in it.
                            SetTestData(e.Content, activeTest, testCookieData, out testCookieData);
                        }

                        Swap(testCookieData, activeTest, e);
                        EvaluateViews(testCookieData, originalContent);

                        HttpContext.Current.Items.Remove(ABTestHandlerSkipFlag);
                    }
                }
                catch (Exception err)
                {
                    _logger.Error("TestHandler.LoadedContent", err);
                }
            }
        }

        private void SetTestData(IContent e, IMarketingTest activeTest, TestDataCookie testCookieData, out TestDataCookie retCookieData)
        {
            var newVariant = _testManager.ReturnLandingPage(activeTest.Id);
            testCookieData.TestId = activeTest.Id;
            testCookieData.TestContentId = activeTest.OriginalItemId;
            testCookieData.TestVariantId = newVariant.Id;

            foreach (var kpi in activeTest.KpiInstances)
            {
                testCookieData.KpiConversionDictionary.Add(kpi.Id, false);
                testCookieData.AlwaysEval = Attribute.IsDefined(kpi.GetType(), typeof(AlwaysEvaluateAttribute));
            }

            if (newVariant.Id != Guid.Empty)
            {
                if (!newVariant.IsPublished)
                {
                    testCookieData.ShowVariant = true;
                }
            }
            _testDataCookieHelper.UpdateTestDataCookie(testCookieData);
            retCookieData = testCookieData;
        }
        //Handles the swapping of content data
        private void Swap(TestDataCookie cookie, IMarketingTest activeTest, ContentEventArgs activeContent)
        {
            if (cookie.ShowVariant && _testDataCookieHelper.IsTestParticipant(cookie))
            {
                var variant = _testManager.GetVariantContent(activeContent.Content.ContentGuid);
                //swap it with the cached version
                if (variant != null)
                {
                    activeContent.ContentLink = variant.ContentLink;
                    activeContent.Content = variant;

                    //The SkipRaiseContentSwitchEvent flag is necessary in order to only raise our ContentSwitchedEvent
                    //once per content per request.  We save an item of activecontent+flag because we may have multiple 
                    //content items per request which will need to be handled.
                    if (!HttpContext.Current.Items.Contains(activeContent + SkipRaiseContentSwitchEvent))
                    {
                        _marketingTestingEvents.RaiseMarketingTestingEvent(
                            DefaultMarketingTestingEvents.ContentSwitchedEvent,
                            new TestEventArgs(activeTest, activeContent.Content));
                        HttpContext.Current.Items[activeContent + SkipRaiseContentSwitchEvent] = true;
                    }
                }
            }
        }

        /// <summary>
        /// Checks for any client kpis which may be assigned to the test and injects the provided
        /// markup via the current response.
        /// </summary>
        /// <param name="kpiInstances"></param>
        /// <param name="cookieData"></param>
        private void ActivateClientKpis(List<IKpi> kpiInstances, TestDataCookie cookieData)
        {
            Dictionary<Guid, TestDataCookie> ClientKpiList = new Dictionary<Guid, TestDataCookie>();
            foreach (var kpi in kpiInstances.Where(x => x is IClientKpi))
            {
                if (!HttpContext.Current.Items.Contains(kpi.Id.ToString())
                    && !_contextHelper.IsInSystemFolder()
                    && (!cookieData.Converted || cookieData.AlwaysEval))
                {

                    if (HttpContext.Current.Response.Cookies.AllKeys.Contains("ClientKpiList"))
                    {
                        ClientKpiList = JsonConvert.DeserializeObject<Dictionary<Guid, TestDataCookie>>(HttpContext.Current.Response.Cookies["ClientKpiList"].Value);
                        HttpContext.Current.Response.Cookies.Remove("ClientKpiList");
                    }

                    ClientKpiList.Add(kpi.Id, cookieData);
                    var tempKpiList = JsonConvert.SerializeObject(ClientKpiList);
                    HttpContext.Current.Response.Cookies.Add(new HttpCookie("ClientKpiList") { Value = tempKpiList });
                    HttpContext.Current.Items[kpi.Id.ToString()] = true;
                }
            }
        }

        public void AppendClientKpiScript()
        {
            //Check if the current response has client kpis.  This lets us know we are in the correct response
            //so we don't inject scripts into an unrelated response stream.
            if (HttpContext.Current.Response.Cookies.AllKeys.Contains("ClientKpiList"))
            {
                //Get the current client kpis we are concered with.
                Dictionary<Guid, TestDataCookie> clientKpiList = JsonConvert.DeserializeObject<Dictionary<Guid, TestDataCookie>>(HttpContext.Current.Response.Cookies["ClientKpiList"].Value);

                //Marker to identify our injected code
                string script = "<!-- ABT Script -->";

                //We need the wrapper which we can get from any client kpi
                KpiManager kpiManager = new KpiManager();
                ClientKpi firstClient = kpiManager.Get(clientKpiList.First().Key) as ClientKpi;
                script += firstClient.ClientKpiScript;

                //Add clients custom evaluation scripts
                foreach (KeyValuePair<Guid, TestDataCookie> data in clientKpiList)
                {
                    //Get required test information for current client kpi

                    IKpiManager _kpiManager = _serviceLocator.GetInstance<IKpiManager>();
                    ClientKpi tempKpi = _kpiManager.Get(data.Key) as ClientKpi;
                    var test = _testManager.Get(data.Value.TestId);
                    var itemVersion = test.Variants.FirstOrDefault(v => v.Id == data.Value.TestVariantId).ItemVersion;

                    //Inject necessary code into client provided script to properly process client conversion
                    var modifiedClientScript = tempKpi.ClientEvaluationScript.Replace("window.dispatchEvent(this.ClientKpiConverted);",
                        "this.ClientKpiConverted.id = '" + tempKpi.Id + "';" + Environment.NewLine +
                        "addKpiData('" + tempKpi.Id + "','" + test.Id + "','" + itemVersion + "');" + Environment.NewLine +
                        "window.dispatchEvent(ClientKpiConverted);");

                    script += modifiedClientScript;
                    HttpContext.Current.Items[tempKpi.Id.ToString()] = true;
                }

                //Check to make sure we client kpis we are supposed to inject
                HttpContext context = HttpContext.Current;
                if (HttpContext.Current.Items.Contains(clientKpiList.Keys.First().ToString()))
                {
                    //Remove the temporary cookie.
                    context.Response.Cookies.Remove("ClientKpiList");

                    //Inject our script into the stream.
                    context.Response.Filter = new ABResponseFilter(context.Response.Filter, script);
                }
            }
        }

        //Handles the incrementing of view counts on a version
        private void EvaluateViews(TestDataCookie cookie, IContent originalContent)
        {
            var currentTest = _serviceLocator.GetInstance<ITestManager>().Get(cookie.TestId);
            var variantVersion = currentTest.Variants.FirstOrDefault(x => x.Id == cookie.TestVariantId).ItemVersion;

            if (_contextHelper.IsRequestedContent(originalContent) && _testDataCookieHelper.IsTestParticipant(cookie))
            {
                ActivateClientKpis(currentTest.KpiInstances, cookie);

                //increment view if not already done
                if (!cookie.Viewed && DbReadWrite())
                {
                    _testManager.IncrementCount(cookie.TestId,
                        variantVersion,
                        CountType.View);
                    cookie.Viewed = true;



                    _testDataCookieHelper.UpdateTestDataCookie(cookie);
                }
            }
        }

        /// <summary>
        /// Analyzes existing cookies and expires / updates any depending on what tests are in the cache.
        /// It is assumed that only tests in the cache are active.
        /// </summary>
        private void EvaluateCookies()
        {
            if (!DbReadWrite())
            {
                return;
            }

            var testCookieList = _testDataCookieHelper.GetTestDataFromCookies();
            foreach (var testCookie in testCookieList)
            {
                var activeTest = _testManager.GetActiveTestsByOriginalItemId(testCookie.TestContentId).FirstOrDefault();
                if (activeTest == null)
                {
                    // if cookie exists but there is no associated test, expire it 
                    if (_testDataCookieHelper.HasTestData(testCookie))
                    {
                        _testDataCookieHelper.ExpireTestDataCookie(testCookie);
                    }
                }
                else if (activeTest.Id != testCookie.TestId)
                {
                    // else we have a valid test but the cookie test id doesnt match because user created a new test 
                    // on the same content.
                    _testDataCookieHelper.ExpireTestDataCookie(testCookie);
                }
            }
        }

        /// <summary>
        /// Processes the Kpis, determining conversions and handling incrementing conversion counts.
        /// </summary>
        /// <param name="e"></param>
        private void EvaluateKpis(object sender, EventArgs e)
        {
            // Set the flag to stop evaluating ONLY if the current page link is the same link in the
            // content event args. This allows us to evaluate all pages, blocks, and sub pages
            // one time per request when we get to the content being evaluated
            // MAR -565 (not converting on sub pages)
            var cea = e as ContentEventArgs;
            if (cea != null)
            {
                // We only want to evaluate for the LoadedContent event's Kpis one time per request. 
                // If the flag is set we already evaluated, bail out
                if (HttpContext.Current.Items.Contains(ABTestHandlerSkipKpiEval))
                {
                    return;
                }

                try
                {
                    var pageHelper = _serviceLocator.GetInstance<IPageRouteHelper>();
                    if (pageHelper.PageLink.ID == cea.ContentLink.ID)
                    {
                        HttpContext.Current.Items[ABTestHandlerSkipKpiEval] = true;
                    }
                }
                catch (Exception err)
                {
                    // this should never happen when in view mode and we should never be here in edit mode.
                    _logger.Warning("EvaluateKpis : pagehelper error - evaluating all kpis", err);
                }
            }

            var cookielist = _testDataCookieHelper.GetTestDataFromCookies();
            foreach (var tdcookie in cookielist)
            {
                // for every test cookie we have, check for the converted and the viewed flag, also check for AlwaysEval flag (example: AverageCart kpi has this)
                if ((tdcookie.Converted && !tdcookie.AlwaysEval) || !tdcookie.Viewed)
                {
                    continue;
                }

                var test = _testManager.GetActiveTestsByOriginalItemId(tdcookie.TestContentId).FirstOrDefault();
                if (test == null)
                {
                    continue;
                }

                // optimization : Evalute only the kpis that have not currently evaluated to true.
                var kpis = new List<IKpi>();
                foreach (var kpi in test.KpiInstances)
                {
                    var converted = tdcookie.KpiConversionDictionary.First(x => x.Key == kpi.Id).Value;
                    if (!converted || tdcookie.AlwaysEval)
                    {
                        kpis.Add(kpi);
                    }
                }

                // if kpi object loads content we dont want to get triggered.
                HttpContext.Current.Items[ABTestHandlerSkipFlag] = true;
                var kpiResults = _testManager.EvaluateKPIs(kpis, sender, e);
                HttpContext.Current.Items.Remove(ABTestHandlerSkipFlag);

                var conversionResults = kpiResults.OfType<KpiConversionResult>();
                ProcessKpiConversionResults(tdcookie, test, kpis, conversionResults);

                var financialResults = kpiResults.OfType<KpiFinancialResult>();
                ProcessKeyFinancialResults(tdcookie, test, kpis, financialResults);

                var valueResults = kpiResults.OfType<KpiValueResult>();
                ProcessKeyValueResults(tdcookie, test, kpis, valueResults);
            }
        }

        /// <summary>
        /// Loop through conversion results to see if any have converted
        /// </summary>
        /// <param name="tdcookie"></param>
        /// <param name="test"></param>
        /// <param name="kpis"></param>
        /// <param name="results"></param>
        /// <returns></returns>
        private bool CheckForConversion(TestDataCookie tdcookie, IMarketingTest test, List<IKpi> kpis,
            IEnumerable<IKpiResult> results)
        {
            // add each kpi to testdata cookie data
            foreach (var result in results)
            {
                if (!result.HasConverted)
                {
                    continue;
                }

                tdcookie.KpiConversionDictionary.Remove(result.KpiId);
                tdcookie.KpiConversionDictionary.Add(result.KpiId, true);
                _marketingTestingEvents.RaiseMarketingTestingEvent(DefaultMarketingTestingEvents.KpiConvertedEvent,
                    new KpiEventArgs(kpis.FirstOrDefault(k => k.Id == result.KpiId), test));
            }

            // now check to see if all kpi objects have evaluated
            tdcookie.Converted = tdcookie.KpiConversionDictionary.All(x => x.Value);

            // now save the testdata to the cookie
            _testDataCookieHelper.UpdateTestDataCookie(tdcookie);

            // now if we have converted, fire the converted message 
            // note : we wouldnt be here if we already converted on a previous loop
            return tdcookie.Converted;
        }

        /// <summary>
        /// Loop through conversion results to see if any have converted, if so update views/conversions as necessary
        /// </summary>
        /// <param name="tdcookie"></param>
        /// <param name="test"></param>
        /// <param name="kpis"></param>
        /// <param name="results"></param>
        private void ProcessKpiConversionResults(TestDataCookie tdcookie, IMarketingTest test, List<IKpi> kpis,
            IEnumerable<KpiConversionResult> results)
        {
            // check that the kpi has converted or not, if so then we save the necessary results
            if (!CheckForConversion(tdcookie, test, kpis, results))
                return;

            var varUserSees = test.Variants.First(x => x.Id == tdcookie.TestVariantId);
            _testManager.IncrementCount(test.Id, varUserSees.ItemVersion, CountType.Conversion);

            _marketingTestingEvents.RaiseMarketingTestingEvent(DefaultMarketingTestingEvents.AllKpisConvertedEvent,
                new KpiEventArgs(tdcookie.KpiConversionDictionary, test));
        }

        private void ProcessKeyFinancialResults(TestDataCookie tdcookie, IMarketingTest test, List<IKpi> kpis, IEnumerable<KpiFinancialResult> results)
        {
            // check that the kpi has converted or not, if so then we save the necessary results
            if (!CheckForConversion(tdcookie, test, kpis, results))
                return;

            var varUserSees = test.Variants.First(x => x.Id == tdcookie.TestVariantId);

            foreach (var kpiFinancialResult in results)
            {
                if (kpiFinancialResult.HasConverted)
                {
                    var keyFinancialResult = new KeyFinancialResult()
                    {
                        Id = Guid.NewGuid(),
                        KpiId = kpiFinancialResult.KpiId,
                        Total = kpiFinancialResult.Total,
                        TotalMarketCulture = kpiFinancialResult.TotalMarketCulture,
                        ConvertedTotal = kpiFinancialResult.ConvertedTotal,
                        ConvertedTotalCulture = kpiFinancialResult.ConvertedTotalCulture,
                        VariantId = varUserSees.ItemId,
                        CreatedDate = DateTime.UtcNow,
                        ModifiedDate = DateTime.UtcNow
                    };

                    _testManager.SaveKpiResultData(test.Id, varUserSees.ItemVersion, keyFinancialResult, KeyResultType.Financial);
                }
            }

            _marketingTestingEvents.RaiseMarketingTestingEvent(DefaultMarketingTestingEvents.AllKpisConvertedEvent,
                new KpiEventArgs(tdcookie.KpiConversionDictionary, test));
        }

        private void ProcessKeyValueResults(TestDataCookie tdcookie, IMarketingTest test, List<IKpi> kpis, IEnumerable<KpiValueResult> results)
        {
            // check that the kpi has converted or not, if so then we save the necessary results
            if (!CheckForConversion(tdcookie, test, kpis, results))
                return;

            var varUserSees = test.Variants.First(x => x.Id == tdcookie.TestVariantId);

            foreach (var kpiValueResult in results)
            {
                var keyValueResult = new KeyValueResult()
                {
                    Id = Guid.NewGuid(),
                    KpiId = kpiValueResult.KpiId,
                    Value = kpiValueResult.Value,
                    VariantId = varUserSees.ItemId,
                    CreatedDate = DateTime.UtcNow,
                    ModifiedDate = DateTime.UtcNow
                };

                _testManager.SaveKpiResultData(test.Id, varUserSees.ItemVersion, keyValueResult, KeyResultType.Value);
            }

            _marketingTestingEvents.RaiseMarketingTestingEvent(DefaultMarketingTestingEvents.AllKpisConvertedEvent,
                new KpiEventArgs(tdcookie.KpiConversionDictionary, test));
        }

        private bool DbReadWrite()
        {
            var dbmode = _serviceLocator.GetInstance<IDatabaseMode>().DatabaseMode;
            return dbmode == DatabaseMode.ReadWrite;
        }

        #region ProxyEventHandlerSupport

        /// <summary>
        /// Handles KPI evaluation logic for KPIs that are triggered from an event.  
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ProxyEventHandler(object sender, EventArgs e)
        {
            if (!_contextHelper.SwapDisabled(e) && DbReadWrite())
            {
                try
                {
                    EvaluateKpis(sender, e);
                }
                catch (Exception err)
                {
                    _logger.Error("TestHandler.ProxyEventHandler", err);
                }
            }
        }

        /// <summary>
        /// At startup, initializes all the ProxyEventHandler's for all Kpi objects found in all active tests.
        /// </summary>
        internal void initProxyEventHandler()
        {
            foreach (var test in _testManager.ActiveCachedTests)
            {
                foreach (var kpi in test.KpiInstances)
                {
                    AddProxyEventHandler(kpi);
                }
            }

            // Setup our listener so when tests are added and removed and update our proxyEventHandler
            var e = _serviceLocator.GetInstance<IMarketingTestingEvents>();
            e.TestAddedToCache += TestAddedToCache;
            e.TestRemovedFromCache += TestRemovedFromCache;
        }

        /// <summary>
        /// When a test is added to the active cache, this method will be fired.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void TestAddedToCache(object sender, TestEventArgs e)
        {
            foreach (var kpi in e.Test.KpiInstances)
            {
                AddProxyEventHandler(kpi);
            }
        }

        /// <summary>
        /// When a test is removed to the active cache, this method will be fired.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void TestRemovedFromCache(object sender, TestEventArgs e)
        {
            foreach (var kpi in e.Test.KpiInstances)
            {
                RemoveProxyEventHandler(kpi);
            }
        }

        /// <summary>
        /// Adds the ProxyEventHandler for the given Kpi instance if it supports the EventSpecificationAttribute.
        /// </summary>
        /// <param name="kpi"></param>
        internal void AddProxyEventHandler(IKpi kpi)
        {
            kpi.Initialize();

            // Add the proxyeventhandler only once, if its in our reference counter, just increment
            // the reference.
            if (!_ReferenceCounter.hasReference(kpi.GetType()))
            {
                kpi.EvaluateProxyEvent += ProxyEventHandler;
                _ReferenceCounter.AddReference(kpi.GetType());
            }
            else
            {
                _ReferenceCounter.AddReference(kpi.GetType());
            }
        }

        /// <summary>
        /// Removes the ProxyEventHandler for the given Kpi instance if it supports the EventSpecificationAttribute.
        /// </summary>
        /// <param name="kpi"></param>
        internal void RemoveProxyEventHandler(IKpi kpi)
        {
            kpi.Uninitialize();

            _ReferenceCounter.RemoveReference(kpi.GetType());

            // Remove the proxyeventhandler only once, when the last reference is removed.
            if (!_ReferenceCounter.hasReference(kpi.GetType()))
            {
                kpi.EvaluateProxyEvent -= ProxyEventHandler;
            }
        }
        #endregion
    }
}
