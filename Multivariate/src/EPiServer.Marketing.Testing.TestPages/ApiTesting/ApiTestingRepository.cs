using System;
using System.Collections.Generic;
using System.Linq;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Marketing.Testing.Data;
using EPiServer.Marketing.Testing.Data.Enums;
using EPiServer.Marketing.Testing.TestPages.Models;
using EPiServer.Marketing.Testing.Web.Repositories;
using EPiServer.ServiceLocation;

namespace EPiServer.Marketing.Testing.TestPages.ApiTesting
{
    public class ApiTestingRepository
    {

        private TestManager _mtm;
        private List<KeyPerformanceIndicator> Kpis;
        private Guid originalItemGuid;
        private List<Variant> variantsToSave;
        private List<TestResult> testResults = new List<TestResult>();

        public List<IMarketingTest> GetTests(ViewModel viewModel = null)
        {
            TestManager mtm = new TestManager();
            List<IMarketingTest> discoveredTests = new List<IMarketingTest>();
            ITestRepository testRepo = new TestRepository();

            if (viewModel == null)
            {
                discoveredTests = mtm.GetTestList(new TestCriteria());
            }
            else
            {
                var criteria = new TestCriteria();

                foreach (var filter in viewModel.Filters.Where(filter => filter.IsEnabled))
                {
                    if (filter.Property == ABTestProperty.State)
                    {
                        var state = TestState.Active;
                        switch (filter.FilterValue.ToLower())
                        {
                            case "inactive":
                                state = TestState.Inactive;
                                break;
                            case "active":
                                state = TestState.Active;
                                break;
                            case "done":
                                state = TestState.Done;
                                break;
                            case "archived":
                                state = TestState.Archived;
                                break;
                        }

                        criteria.AddFilter(new ABTestFilter(filter.Property,
                            filter.OperatorValue.ToLower() == "and" ? FilterOperator.And : FilterOperator.Or,
                            state));
                    }
                    else
                    {
                        criteria.AddFilter(new ABTestFilter(filter.Property,
                            filter.OperatorValue.ToLower() == "and" ? FilterOperator.And : FilterOperator.Or,
                            new Guid(filter.FilterValue)));
                    }
                }

                discoveredTests = mtm.GetTestList(criteria);
            }
            return discoveredTests;
        }

        public Guid CreateAbTest(ABTest dataToSave)
        {
            TestManager _mtm = new TestManager();
            dataToSave.Id = Guid.NewGuid();

            dataToSave.KeyPerformanceIndicators = new List<KeyPerformanceIndicator>()
                {
                    new KeyPerformanceIndicator() {Id=Guid.NewGuid(),KeyPerformanceIndicatorId = Guid.NewGuid()},
                };

            dataToSave.TestResults = new List<TestResult>()
            {
                new TestResult() {Id=Guid.NewGuid(),ItemId = dataToSave.Variants[0].Id, ItemVersion = dataToSave.Variants[0].ItemVersion},
                new TestResult() {Id = Guid.NewGuid(),ItemId = dataToSave.Variants[1].Id, ItemVersion = dataToSave.Variants[1].ItemVersion}
            };

            _mtm.Save(dataToSave);

            return dataToSave.Id;
        }

        /// <summary>
        /// Gets a list of tests associated with the provided ID
        /// 
        /// todo
        ///     Remove hardcoded test data and add parametr for
        ///     test supplied test data
        /// </summary>
        /// <returns>List of IABTest containing</returns>
        public List<IMarketingTest> GetAbTestList(string originalItemId)
        {
            var itemId = new Guid(originalItemId);
            _mtm = new TestManager();

            return _mtm.GetTestList(new TestCriteria()).Where(t => t.OriginalItemId == itemId).ToList();
        }

        public IMarketingTest SetAbState(Guid testId, TestState? state)
        {
            _mtm = new TestManager();
            switch (state)
            {
                case TestState.Active:
                    _mtm.Start(testId);
                    break;
                case TestState.Inactive:
                    _mtm.Stop(testId);
                    break;
                case TestState.Archived:
                    _mtm.Archive(testId);
                    break;
                default:
                    return null;
            }
            return _mtm.Get(testId);
        }

        public IMarketingTest RunTests(Guid testId)
        {
            _mtm = new TestManager();
            _mtm.Start(testId);
            for (int x = 0; x < 5; x++)
            {
                Guid result = _mtm.ReturnLandingPage(testId);
                _mtm.IncrementCount(testId, result, 1, CountType.View);
                if (x % 5 == 0)
                    _mtm.IncrementCount(testId, result, 1, CountType.Conversion);
            }

            _mtm.Stop(testId);

            return _mtm.Get(testId);
        }

        public IMarketingTest StartTest(Guid testId)
        {
            _mtm = new TestManager();
            _mtm.Start(testId);
            for (int x = 0; x < 5; x++)
            {
                Guid result = _mtm.ReturnLandingPage(testId);
                _mtm.IncrementCount(testId, result, 1, CountType.View);
                if (x % 5 == 0)
                    _mtm.IncrementCount(testId, result, 1, CountType.Conversion);
            }

            return _mtm.Get(testId);
        }

        public PageVersionCollection GetContentVersions(Guid originalPageReference)
        {
            IServiceLocator _serviceLocator = ServiceLocator.Current;
            IContentRepository _contentRepository = _serviceLocator.GetInstance<IContentRepository>();

            PageData originalContent = _contentRepository.Get<PageData>(originalPageReference);

            DataFactory df = DataFactory.Instance;

            return df.ListVersions(originalContent.PageLink);
        }
    }
}