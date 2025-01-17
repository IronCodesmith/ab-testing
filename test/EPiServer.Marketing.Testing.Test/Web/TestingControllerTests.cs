﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Web.Http;
using EPiServer.Marketing.Testing.Core.DataClass;
using EPiServer.Marketing.Testing.Core.DataClass.Enums;
using EPiServer.Marketing.Testing.Core.Manager;
using EPiServer.Marketing.Testing.Messaging;
using EPiServer.Marketing.Testing.Web.Controllers;
using EPiServer.Marketing.Testing.Web.Helpers;
using EPiServer.Marketing.Testing.Web.Repositories;
using EPiServer.ServiceLocation;
using Moq;
using Xunit;
using EPiServer.Marketing.KPI.Manager;
using EPiServer.Marketing.KPI.Manager.DataClass;
using EPiServer.Marketing.KPI.Manager.DataClass.Enums;
using EPiServer.Marketing.KPI.Results;

namespace EPiServer.Marketing.Testing.Test.Web
{
    public class TestingControllerTests
    {
        private Mock<IServiceLocator> _mockServiceLocator;
        private Mock<IMarketingTestingWebRepository> _marketingTestingRepoMock;
        private Mock<IMessagingManager> _messagingManagerMock;
        private Mock<ITestDataCookieHelper> _testDataCookieHelperMock;
        private Mock<ITestManager> _testManagerMock;
        private Mock<IKpiWebRepository> _kpiWebRepoMock;
        Mock<IHttpContextHelper> contextHelper;

        private TestingController GetUnitUnderTest()
        {
            _mockServiceLocator = new Mock<IServiceLocator>();
            _marketingTestingRepoMock = new Mock<IMarketingTestingWebRepository>();
            _testManagerMock = new Mock<ITestManager>();
            _messagingManagerMock = new Mock<IMessagingManager>();
            contextHelper = new Mock<IHttpContextHelper>();
            _testDataCookieHelperMock = new Mock<ITestDataCookieHelper>();
            _kpiWebRepoMock = new Mock<IKpiWebRepository>();
            contextHelper = new Mock<IHttpContextHelper>();
            
            _mockServiceLocator.Setup(s1 => s1.GetInstance<ITestDataCookieHelper>()).Returns(_testDataCookieHelperMock.Object);
            _mockServiceLocator.Setup(s1 => s1.GetInstance<IMarketingTestingWebRepository>()).Returns(_marketingTestingRepoMock.Object);
            _mockServiceLocator.Setup(s1 => s1.GetInstance<IMessagingManager>()).Returns(_messagingManagerMock.Object);
            _mockServiceLocator.Setup(s1 => s1.GetInstance<ITestDataCookieHelper>()).Returns(_testDataCookieHelperMock.Object);
            _mockServiceLocator.Setup(s1 => s1.GetInstance<ITestManager>()).Returns(_testManagerMock.Object);
            _mockServiceLocator.Setup(sl => sl.GetInstance<IKpiWebRepository>()).Returns(_kpiWebRepoMock.Object);
            
            return new TestingController(contextHelper.Object, _mockServiceLocator.Object)
            {
                Request = new System.Net.Http.HttpRequestMessage(),
                Configuration = new HttpConfiguration()
            };            
        }

        [Fact]
        public void UpdateConversion_Uses_ConfigurationSpecified_SessionID_Name()
        {
            var pairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("testId", Guid.NewGuid().ToString()),
                new KeyValuePair<string, string>("itemVersion", "1"),
                new KeyValuePair<string, string>("kpiId", Guid.Parse("bb53bed8-978a-456d-9fd7-6a2bea1bf66f").ToString()),
            };
            var data = new FormDataCollection(pairs);

            var controller = GetUnitUnderTest();
            var result = controller.UpdateConversion(data);

            contextHelper.Verify(m => m.GetSessionCookieName(), "Failed to use the method that gets the session cookie name from the config");
        }

        [Fact]
        public void UpdateConversion_Returns_BadRequest_If_TestID_Is_Null()
        {
            var pairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("itemVersion", "1"),
                new KeyValuePair<string, string>("kpiId", Guid.Parse("bb53bed8-978a-456d-9fd7-6a2bea1bf66f").ToString()),
            };
            var data = new FormDataCollection(pairs);

            var controller = GetUnitUnderTest();
            var result = controller.UpdateConversion(data);
            Assert.True(result.StatusCode == HttpStatusCode.BadRequest);
        }

        [Fact]
        public void UpdateConversion_Returns_OK_And_Calls_EmitUpdateConversion_With_Form_Data()
        {
            var TestGuid = Guid.NewGuid();
            var KpiGuid = Guid.NewGuid();
            var pairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("testId", TestGuid.ToString()),
                new KeyValuePair<string, string>("itemVersion", "1"),
                new KeyValuePair<string, string>("kpiId", KpiGuid.ToString()),
            };
            var data = new FormDataCollection(pairs);

            var controller = GetUnitUnderTest();
            var result = controller.UpdateConversion(data);

            Assert.True(result.StatusCode == HttpStatusCode.OK);
            _messagingManagerMock.Verify(m => m.EmitUpdateConversion(It.Is<Guid>(g => g.Equals(TestGuid)), It.Is<int>(v => v == 1), It.Is<Guid>(g => g.Equals(KpiGuid)), It.IsAny<string>()), "Did not emit message with proper arguments");
        }


        [Fact]
        public void GetAllTests_Returns_OK_Status_Result()
        {
            var controller = GetUnitUnderTest();

            var result = controller.GetAllTests();

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }

        [Fact]
        public void GetTest_Returns_Test()
        {
            var controller = GetUnitUnderTest();

            var result = controller.GetTest(Guid.NewGuid().ToString());

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }

        [Fact]
        public void GetTest_Returns_Not_Found()
        {
            var id = Guid.NewGuid();
            ABTest test = null;

            var controller = GetUnitUnderTest();
            _marketingTestingRepoMock.Setup(call => call.GetTestById(It.Is<Guid>(g => g == id), It.IsAny<bool>())).Returns(test);

            var result = controller.GetTest(id.ToString());
            var response = result.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Contains(id.ToString(), response.Result);
        }

        [Fact]
        public void SaveKpiResult_Financial_Returns_OK_Request()
        {
            var pairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("testId", Guid.NewGuid().ToString()),
                new KeyValuePair<string, string>("itemVersion", "1"),
                new KeyValuePair<string, string>("kpiId", Guid.Parse("bb53bed8-978a-456d-9fd7-6a2bea1bf66f").ToString()),
                new KeyValuePair<string, string>("total", "3")
            };
            var data = new FormDataCollection(pairs);
            var cookie = new TestDataCookie();
            cookie.KpiConversionDictionary.Add(Guid.Parse("bb53bed8-978a-456d-9fd7-6a2bea1bf66f"), false);

            var controller = GetUnitUnderTest();
            _testDataCookieHelperMock.Setup(call => call.GetTestDataFromCookie(It.IsAny<string>(), It.IsAny<string>())).Returns(cookie);
            _marketingTestingRepoMock.Setup(call => call.GetTestById(It.IsAny<Guid>(), It.IsAny<bool>())).Returns(new ABTest());

            var result = controller.SaveKpiResult(data);

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }

        [Fact]
        public void SaveKpiResult_Returns_OK_Request()
        {
            var pairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("testId", Guid.NewGuid().ToString()),
                new KeyValuePair<string, string>("itemVersion", "1"),
                new KeyValuePair<string, string>("kpiId", Guid.Parse("bb53bed8-978a-456d-9fd7-6a2bea1bf66f").ToString()),
                new KeyValuePair<string, string>("total", "3")
            };
            var data = new FormDataCollection(pairs);
            var cookie = new TestDataCookie();
            cookie.KpiConversionDictionary.Add(Guid.Parse("bb53bed8-978a-456d-9fd7-6a2bea1bf66f"), false);

            var controller = GetUnitUnderTest();
            _testDataCookieHelperMock.Setup(call => call.GetTestDataFromCookie(It.IsAny<string>(), It.IsAny<string>())).Returns(cookie);
            _marketingTestingRepoMock.Setup(call => call.GetTestById(It.IsAny<Guid>(), It.IsAny<bool>())).Returns(new ABTest());

            var result = controller.SaveKpiResult(data);

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }

        [Fact]
        public void SaveKpiResult_Returns_Bad_Request()
        {
            var pairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("testId", ""),
                new KeyValuePair<string, string>("itemVersion", "1"),
                new KeyValuePair<string, string>("keyResultType", "1"),
                new KeyValuePair<string, string>("kpiId", Guid.NewGuid().ToString()),
                new KeyValuePair<string, string>("total", "3")
            };
            var data = new FormDataCollection(pairs);

            var controller = GetUnitUnderTest();
            _kpiWebRepoMock.Setup(call => call.GetKpiInstance(It.IsAny<Guid>())).Returns(new Kpi());

            var result = controller.SaveKpiResult(data);

            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        }

        [Fact]
        public void SaveKpiResult_handles_full_range_of_itemversions()
        {
            // item versions can go up to int 32 ranges
            var pairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("testId", Guid.NewGuid().ToString()),
                new KeyValuePair<string, string>("itemVersion", "1695874"),
                new KeyValuePair<string, string>("kpiId", Guid.Parse("bb53bed8-978a-456d-9fd7-6a2bea1bf66f").ToString()),
                new KeyValuePair<string, string>("total", "3")
            };
            var data = new FormDataCollection(pairs);
            var cookie = new TestDataCookie();
            cookie.KpiConversionDictionary.Add(Guid.Parse("bb53bed8-978a-456d-9fd7-6a2bea1bf66f"), false);

            var controller = GetUnitUnderTest();
            _testDataCookieHelperMock.Setup(call => call.GetTestDataFromCookie(It.IsAny<string>(), It.IsAny<string>())).Returns(cookie);
            _marketingTestingRepoMock.Setup(call => call.GetTestById(It.IsAny<Guid>(), It.IsAny<bool>())).Returns(new ABTest());

            var result = controller.SaveKpiResult(data);

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }

        [Fact]
        public void UpdateView_Returns_OK_Request()
        {
            var pairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("testId", Guid.NewGuid().ToString()),
                new KeyValuePair<string, string>("itemVersion", "1")
            };

            var data = new FormDataCollection(pairs);

            var controller = GetUnitUnderTest();

            var result = controller.UpdateView(data);

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }

        [Fact]
        public void UpdateView_Returns_Bad_Request()
        {
            var pairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("testId", ""),
                new KeyValuePair<string, string>("itemVersion", "1")
            };

            var data = new FormDataCollection(pairs);

            var controller = GetUnitUnderTest();

            var result = controller.UpdateView(data);

            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        }
    }

    class testFinancialKpi : IKpi
    {
        public DateTime CreatedDate { get; set; }

        public string Description { get; }

        public string FriendlyName { get; set; }

        public Guid Id { get; set; }

        public virtual string KpiResultType
        {
            get
            {
                return typeof(KpiFinancialResult).Name.ToString();
            }
        }

        public DateTime ModifiedDate { get; set; }

        public ResultComparison ResultComparison { get; set; }

        public string UiMarkup { get; set; }

        public string UiReadOnlyMarkup { get; set; }

        ResultComparison IKpi.ResultComparison
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public event EventHandler EvaluateProxyEvent;

        public IKpiResult Evaluate(object sender, EventArgs e) { return null; }

        public void Initialize() { }

        public void Uninitialize() { }

        public void Validate(Dictionary<string, string> kpiData) { }

        IKpiResult IKpi.Evaluate(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
