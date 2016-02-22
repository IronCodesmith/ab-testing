﻿using System;
using System.Collections.Generic;
using EPiServer.Marketing.Testing.Dal;
using EPiServer.Marketing.Testing.Model;
using EPiServer.Marketing.Testing.Model.Enums;
using EPiServer.Marketing.Testing;
using EPiServer.Marketing.Testing.Messaging;
using EPiServer.ServiceLocation;
using Moq;
using Xunit;

namespace EPiServer.Marketing.Multivariate.Test.Core
{
        public class MultivariateManagerTests
    {
        private Mock<IServiceLocator> _serviceLocator;
        private Mock<ITestingDataAccess> _dataAccessLayer;

        private TestManager GetUnitUnderTest()
        {
            _serviceLocator = new Mock<IServiceLocator>();
            _dataAccessLayer = new Mock<ITestingDataAccess>();
            _serviceLocator.Setup(sl => sl.GetInstance<ITestingDataAccess>()).Returns(_dataAccessLayer.Object);

            return new TestManager(_serviceLocator.Object);
        }

        [Fact]
        public void TestManager_CallsDataAccessGetWithGuid()
        {
            var theGuid = new Guid("A2AF4481-89AB-4D0A-B042-050FECEA60A3");
            var tm = GetUnitUnderTest();
            tm.Get(theGuid);

            _dataAccessLayer.Verify(da => da.Get(It.Is<Guid>(arg => arg.Equals(theGuid))),
                "DataAcessLayer get was never called or Guid did not match.");
        }

        [Fact]
        public void TestManager_CallsDataAccessGetTestByItemId()
        {
            var theGuid = new Guid("A2AF4481-89AB-4D0A-B042-050FECEA60A3");
            var tm = GetUnitUnderTest();
            tm.GetTestByItemId(theGuid);

            _dataAccessLayer.Verify(da => da.GetTestByItemId(It.Is<Guid>(arg => arg.Equals(theGuid))),
                "DataAcessLayer GetTestByItemId was never called or Guid did not match.");
        }

        [Fact]
        public void TestManager_CallsGetTestListWithCritera()
        {
            var critera = new TestCriteria();
            var tm = GetUnitUnderTest();
            tm.GetTestList(critera);

            _dataAccessLayer.Verify(da => da.GetTestList(It.Is<TestCriteria>(arg => arg.Equals(critera))),
                "DataAcessLayer GetTestList was never called or criteria did not match.");
        }

        [Fact]
        public void TestManager_CallsDeleteWithGuid()
        {
            var theGuid = new Guid("A2AF4481-89AB-4D0A-B042-050FECEA60A3");
            var tm = GetUnitUnderTest();
            tm.Delete(theGuid);

            _dataAccessLayer.Verify(da => da.Delete(It.Is<Guid>(arg => arg.Equals(theGuid))),
                "DataAcessLayer Delete was never called or Guid did not match.");
        }

        [Fact]
        public void TestManager_CallsStartWithGuid()
        {
            var theGuid = new Guid("A2AF4481-89AB-4D0A-B042-050FECEA60A3");
            var tm = GetUnitUnderTest();
            tm.Start(theGuid);

            _dataAccessLayer.Verify(da => da.Start(It.Is<Guid>(arg => arg.Equals(theGuid))),
                "DataAcessLayer Start was never called or Guid did not match.");
        }

        [Fact]
        public void TestManager_CallsStopWithGuid()
        {
            var theGuid = new Guid("A2AF4481-89AB-4D0A-B042-050FECEA60A3");
            var tm = GetUnitUnderTest();
            tm.Stop(theGuid);

            _dataAccessLayer.Verify(da => da.Stop(It.Is<Guid>(arg => arg.Equals(theGuid))),
                "DataAcessLayer Stop was never called or Guid did not match.");
        }

        [Fact]
        public void TestManager_CallsArchiveWithGuid()
        {
            var theGuid = new Guid("A2AF4481-89AB-4D0A-B042-050FECEA60A3");
            var tm = GetUnitUnderTest();
            tm.Archive(theGuid);

            _dataAccessLayer.Verify(da => da.Archive(It.Is<Guid>(arg => arg.Equals(theGuid))),
                "DataAcessLayer Archive was never called or Guid did not match.");
        }

        [Fact]
        public void TestManager_CallsSaveWithProperArguments()
        {
            var theGuid = new Guid("A2AF4481-89AB-4D0A-B042-050FECEA60A3");
            var tm = GetUnitUnderTest();
            ABTest test = new ABTest() { Id = theGuid };
            tm.Save(test);

            _dataAccessLayer.Verify(da => da.Save(It.Is<ABTest>(arg => arg.Equals(test))),
                "DataAcessLayer Save was never called or object did not match.");
        }

        [Fact]
        public void TestManager_CallsIncrementCountWithProperArguments()
        {
            var theGuid = new Guid("A2AF4481-89AB-4D0A-B042-050FECEA60A3");
            var theTestItemGuid = new Guid("A2AF4481-89AB-4D0A-B042-050FECEA60A4");
            CountType type = CountType.Conversion;

            var tm = GetUnitUnderTest();
            tm.IncrementCount(theGuid, theTestItemGuid, type);

            _dataAccessLayer.Verify(da => da.IncrementCount(It.Is<Guid>(arg => arg.Equals(theGuid)), It.IsAny<Guid>(), It.IsAny<CountType>()),
                "DataAcessLayer IncrementCount was never called or Test Guid did not match.");
            _dataAccessLayer.Verify(da => da.IncrementCount(It.IsAny<Guid>(), It.Is<Guid>(arg => arg.Equals(theTestItemGuid)), It.IsAny<CountType>()),
                "DataAcessLayer IncrementCount was never called or test item Guid did not match.");
            _dataAccessLayer.Verify(da => da.IncrementCount(It.IsAny<Guid>(), It.IsAny<Guid>(), It.Is<CountType>(arg => arg.Equals(CountType.Conversion))),
                "DataAcessLayer IncrementCount was never called or CountType did not match.");
        }

        [Fact]
        public void TestManager_ReturnLandingPage_NoCache()
        {
            // Make sure that the return landing page, calls data access layer if its not in the cache..
            var theGuid = new Guid("A2AF4481-89AB-4D0A-B042-050FECEA60A3");
            var originalItemId = new Guid("A2AF4481-89AB-4D0A-B042-050FECEA60A4");
            var vID = new Guid("A2AF4481-89AB-4D0A-B042-050FECEA60A5");
            var variantList = new List<Variant>() { new Variant { Id = vID }, new Variant {Id = originalItemId} };

            var tm = GetUnitUnderTest();
            _dataAccessLayer.Setup(da => da.Get(It.Is<Guid>(arg => arg.Equals(theGuid)))).Returns(
                new ABTest()
                {
                    Id = theGuid,
                    OriginalItemId = originalItemId,
                    Variants = variantList
                });

            var count = 0;
            var originalCalled = false;
            var variantCalled = false;
            // loop over call until all possible switch options are generated.
            while (count < 2)
            {
                // clear the cache if you have to (tm.clearCache() ?) - this test is supposed to verify that the 
                // database layer is called.
                var landingPage = tm.ReturnLandingPage(theGuid);

                if (landingPage == originalItemId && !originalCalled)
                {
                    count++;
                    originalCalled = true;
                }

                if (landingPage == vID && !variantCalled)
                {
                    count++;
                    variantCalled = true;
                }

                _dataAccessLayer.Verify(da => da.Get(It.Is<Guid>(arg => arg.Equals(theGuid))),
                    "DataAcessLayer get was never called or Guid did not match.");
                Assert.True(landingPage.Equals(originalItemId) ||
                              landingPage.Equals(vID), "landingPage is not the original quid or the variant quid");
            }
        }

        [Fact]
        public void TestManager_EmitUpdateConversion()
        {
            var testManager = GetUnitUnderTest();

            // Mock up the message manager
            Mock<IMessagingManager> messageManager = new Mock<IMessagingManager>();
            _serviceLocator.Setup(sl => sl.GetInstance<IMessagingManager>()).Returns(messageManager.Object);

            Guid original = Guid.NewGuid();
            Guid testItemId = Guid.NewGuid();
            testManager.EmitUpdateCount(original, testItemId, CountType.Conversion);

            messageManager.Verify(mm => mm.EmitUpdateConversion(
                It.Is<Guid>(arg => arg.Equals(original)),
                It.Is<Guid>(arg => arg.Equals(testItemId))),
                "Guids are not correct or update conversion message not emmited");
        }

        [Fact]
        public void TestManager_EmitUpdateView()
        {
            var testManager = GetUnitUnderTest();

            // Mock up the message manager
            Mock<IMessagingManager> messageManager = new Mock<IMessagingManager>();
            _serviceLocator.Setup(sl => sl.GetInstance<IMessagingManager>()).Returns(messageManager.Object);

            Guid original = Guid.NewGuid();
            Guid testItemId = Guid.NewGuid();
            testManager.EmitUpdateCount(original, testItemId, CountType.View);

            messageManager.Verify(mm => mm.EmitUpdateViews(
                It.Is<Guid>(arg => arg.Equals(original)),
                It.Is<Guid>(arg => arg.Equals(testItemId))),
                "Guids are not correct or update View message not emmited");
        }
    }
}