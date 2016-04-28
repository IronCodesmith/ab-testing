﻿using System;
using System.Collections.Generic;
using EPiServer.Core;
using EPiServer.Marketing.Testing.Data;
using EPiServer.Marketing.Testing.Data.Enums;
using EPiServer.Core;
using System.Collections;
using EPiServer.Marketing.KPI.Manager.DataClass;

namespace EPiServer.Marketing.Testing
{
    public interface ITestManager
    {
        IMarketingTest Get(Guid testObjectId);

        List<IMarketingTest> GetTestByItemId(Guid originalItemId);

        List<IMarketingTest> GetTestList(TestCriteria criteria);

        Guid Save(IMarketingTest testObject);

        void Delete(Guid testObjectId);

        void Start(Guid testObjectId);

        void Stop(Guid testObjectId);

        void Archive(Guid testObjectId);

        void IncrementCount(Guid testId, Guid itemId, int itemVersion, CountType resultType);

        void EmitUpdateCount(Guid testId, Guid testItemId, int itemVersion, CountType resultType);

        Variant ReturnLandingPage(Guid testId);
        PageData CreateVariantPageDataCache(Guid contentGuid, List<ContentReference> processedList);
        List<IMarketingTest> CreateActiveTestCache();

        /// <summary>
        /// Given a specific test id and the content, iterates over all the Kpi objects and returns 
        /// the list of Kpi Guids that evaluated as true.
        /// </summary>
        /// <param name="testId"></param>
        /// <param name="content"></param>
        /// <returns>list - can be empty, never null</returns>
        IList<Guid> EvaluateKPIs(IList<IKpi> kpis, IContent content);
    }
}
