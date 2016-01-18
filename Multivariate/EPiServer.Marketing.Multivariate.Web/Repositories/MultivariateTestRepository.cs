﻿using System;
using System.Collections.Generic;
using System.Linq;
using EPiServer.Core;
using EPiServer.Marketing.Multivariate.Dal;
using EPiServer.ServiceLocation;
using EPiServer.Marketing.Multivariate.Model;
using EPiServer.Marketing.Multivariate.Web.Models;
using System.Diagnostics.CodeAnalysis;

namespace EPiServer.Marketing.Multivariate.Web.Repositories
{
    [ServiceConfiguration(ServiceType = typeof(IMultivariateTestRepository))]
    public class MultivariateTestRepository : IMultivariateTestRepository
    {
        private IServiceLocator _serviceLocator;
        
        /// <summary>
        /// Default constructor
        /// </summary>
        [ExcludeFromCodeCoverage]
        public MultivariateTestRepository()
        {
            _serviceLocator = ServiceLocator.Current;
        }

        /// <summary>
        /// For unit testing
        /// </summary>
        /// <param name="locator"></param>
        internal MultivariateTestRepository(IServiceLocator locator)
        {
            _serviceLocator = locator;
        }


        /// <summary>
        /// Creates a test based on supplied multivariate test data
        /// </summary>
        /// <param name="testData"></param>
        public Guid CreateTest(MultivariateTestViewModel testData)
        {
            IContentRepository contentrepo = _serviceLocator.GetInstance<IContentRepository>();

            // need to get guid for pages from the page picker content id's we get
            var originalItemRef = contentrepo.Get<IContent>(new ContentReference(testData.OriginalItem));
            var variantItemRef = contentrepo.Get<IContent>(new ContentReference(testData.VariantItem));

            var tm = _serviceLocator.GetInstance<IMultivariateTestManager>();

            var test = new MultivariateTest
            {
                Id = testData.id,
                Title = testData.Title,
                Owner = Security.PrincipalInfo.CurrentPrincipal.Identity.Name,
                OriginalItemId = originalItemRef.ContentGuid,
                StartDate = testData.StartDate,
                EndDate = testData.EndDate,
                Variants = new List<Variant>()
                {
                    new Variant() {Id = Guid.NewGuid(), VariantId = variantItemRef.ContentGuid}
                },
                KeyPerformanceIndicators = new List<KeyPerformanceIndicator>()
                {
                    new KeyPerformanceIndicator() {Id = Guid.NewGuid(), KeyPerformanceIndicatorId = Guid.NewGuid()},
                },
                MultivariateTestResults = new List<MultivariateTestResult>()
                {
                    new MultivariateTestResult() {Id = Guid.NewGuid(), ItemId = originalItemRef.ContentGuid},
                    new MultivariateTestResult() {Id = Guid.NewGuid(), ItemId = variantItemRef.ContentGuid}
                }
            };

            return tm.Save(test);
        }

        /// <summary>
        /// Permanently deletes a test from the multivariate tables including
        /// associated variants, keyperformance indicators and test results.
        /// There is no recovery.
        /// </summary>
        /// <param name="testuGuid">The GUID of the test to be deleted.</param>
        public void DeleteTest(Guid testuGuid)
        {
            IMultivariateTestManager tm = _serviceLocator.GetInstance<IMultivariateTestManager>();
            tm.Delete(testuGuid);
        }
        
        /// <summary>
        /// Returns a list of tests satisfying the supplied criteria
        /// </summary>
        /// <param name="criteria">Criteria to filter on.</param>
        /// <returns>Filtered IMultivariate test list</returns>
        public List<MultivariateTestViewModel> GetTestList(MultivariateTestCriteria criteria)
        {
            IMultivariateTestManager tm = _serviceLocator.GetInstance<IMultivariateTestManager>();

            return (from MultivariateTest test in tm.GetTestList(criteria) select ConvertToViewModel(test)).ToList();
        }

        /// <summary>
        /// Returns a multivariate object based no the supplied testId
        /// </summary>
        /// <param name="testId"></param>
        /// <returns>MultivariateTest</returns>
        public MultivariateTestViewModel GetTestById(Guid testId)
        {
            IMultivariateTestManager tm = _serviceLocator.GetInstance<IMultivariateTestManager>();

            return ConvertToViewModel(tm.Get(testId));
        }

        public MultivariateTestViewModel ConvertToViewModel(IMultivariateTest testToConvert)
        {
            MultivariateTestViewModel testModel = new MultivariateTestViewModel()
            {
                id = testToConvert.Id,
                Title = testToConvert.Title,
                Owner = testToConvert.Owner,
                testState = testToConvert.TestState,
                StartDate = testToConvert.StartDate,
                EndDate = testToConvert.EndDate,
                OriginalItemId = testToConvert.OriginalItemId,
                VariantItemId = testToConvert.Variants[0].VariantId,
                TestResults = testToConvert.MultivariateTestResults
               
            };
            
            return testModel;
        }

        public MultivariateTestResult GetWinningTestResult(MultivariateTestViewModel test)
        {
            var winningTest = new MultivariateTestResult(); // never return null
            winningTest.ItemId = test.OriginalItemId;       // set it to something incase no test results
                                                            // exist, i.e. the original is still winning!
            var currentConversionRate = 0.0;
            foreach (var result in test.TestResults)
            {
                if (result.Views != 0)
                {
                    var rate = (int)(result.Conversions * 100.0 / result.Views);
                    if (rate > currentConversionRate)
                    {
                        currentConversionRate = rate;
                        winningTest = result;
                    }
                }
            }

            return winningTest;
        }

        public void StopTest(Guid testGuid)
        {
            IMultivariateTestManager tm = _serviceLocator.GetInstance<IMultivariateTestManager>();
            tm.Stop(testGuid);
        }

        public void UpdateConversion(Guid testId, Guid VariantId)
        {
            IMultivariateTestManager tm = _serviceLocator.GetInstance<IMultivariateTestManager>();
            var test = tm.Get(testId);
            foreach (var result in test.MultivariateTestResults)
            {
                if( result.ItemId.Equals(VariantId) )
                {
                    result.Conversions++;
                    tm.Save(test);
                    break;
                }
            }
        }

        public void UpdateView(Guid testId, Guid VariantId)
        {
            IMultivariateTestManager tm = _serviceLocator.GetInstance<IMultivariateTestManager>();
            var test = tm.Get(testId);
            foreach (var result in test.MultivariateTestResults )
            {
                if (result.ItemId.Equals(VariantId))
                {
                    result.Views++;
                    tm.Save(test);
                    break;
                }
            }
        }
    }
}
