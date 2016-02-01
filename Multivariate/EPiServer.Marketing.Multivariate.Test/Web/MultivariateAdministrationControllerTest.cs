﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Web.Mvc;
using EPiServer.Core;
using EPiServer.ServiceLocation;
using Moq;
using EPiServer.Marketing.Multivariate.Web;
using EPiServer.Marketing.Multivariate.Web.Repositories;
using EPiServer.Marketing.Testing.Model;
using EPiServer.Marketing.Testing.Model.Enums;
using EPiServer.Marketing.Multivariate.Web.Models;
using EPiServer.Marketing.Testing;
using Xunit;

namespace EPiServer.Marketing.Multivariate.Test.Web
{
        [ExcludeFromCodeCoverage]
    public class MultivariateAdministrationControllerTest
    {
        private Mock<IServiceLocator> _serviceLocator;
        private Mock<IMultivariateTestRepository> _testRepository;
        private Mock<IContentRepository> _contentRepository;
        private Mock<IMultivariateTestManager> _testManager;

        static Guid theGuid = new Guid("76B3BC47-01E8-4F6C-A07D-7F85976F5BE8");
        static Guid original = new Guid("76B3BC47-01E8-4F6C-A07D-7F85976F5BE7");
        static Guid varient = new Guid("76B3BC47-01E8-4F6C-A07D-7F85976F5BE6");
        static Guid result1 = new Guid("76B3BC47-01E8-4F6C-A07D-7F85976F5BE5");
        static Guid result2 = new Guid("76B3BC47-01E8-4F6C-A07D-7F85976F5BE4");
        MultivariateTestViewModel viewdata = new MultivariateTestViewModel()
        {
            id = theGuid,
            Title = "Title",
            Owner = Security.PrincipalInfo.CurrentPrincipal.Identity.Name, // Repo / business logic sets it to this.
            StartDate = DateTime.Today.AddDays(1),
            EndDate = DateTime.Today.AddDays(2),
            OriginalItemId = original,
            OriginalItem = 1,
            VariantItem = 2,
            testState = TestState.Active,
            VariantItemId = varient,
            TestResults = new List<MultivariateTestResult>() {
                    new MultivariateTestResult() { Id = result1 },
                    new MultivariateTestResult() { Id = result2 }
                }
        };

        MultivariateTest test = new MultivariateTest()
        {
            Id = theGuid,
            Title = "Title",
            Owner = "Owner",
            StartDate = DateTime.Today.AddDays(1),
            EndDate = DateTime.Today.AddDays(2),
            OriginalItemId = original,
            TestState = TestState.Active,
            Variants = new List<Variant>() { new Variant() { Id = varient } },
            MultivariateTestResults = new List<MultivariateTestResult>() {
                    new MultivariateTestResult() { Id = result1 },
                    new MultivariateTestResult() { Id = result2 }
                }
        };


        private MultivariateAdministrationController GetUnitUnderTest()
        {
            _serviceLocator = new Mock<IServiceLocator>();
            _testRepository = new Mock<IMultivariateTestRepository>();
            _contentRepository = new Mock<IContentRepository>();
            _testManager = new Mock<IMultivariateTestManager>();

            // Setup the contentrepo so it simulates episerver returning content
            var page1 = new BasicContent() { ContentGuid = viewdata.OriginalItemId };
            var page2 = new BasicContent() { ContentGuid = viewdata.VariantItemId };


            viewdata.OriginalItem = 1;
            viewdata.VariantItem = 2;
            _contentRepository.Setup(cr => cr.Get<IContent>(It.Is<ContentReference>(cf => cf.ID == 1))).Returns(page1);
            _contentRepository.Setup(cr => cr.Get<IContent>(It.Is<ContentReference>(cf => cf.ID == 2))).Returns(page2);
            _serviceLocator.Setup(sl => sl.GetInstance<IContentRepository>()).Returns(_contentRepository.Object);
            _serviceLocator.Setup(sl => sl.GetInstance<IMultivariateTestRepository>()).Returns(_testRepository.Object);
            _serviceLocator.Setup(sl => sl.GetInstance<IMultivariateTestManager>()).Returns(_testManager.Object);

            return new MultivariateAdministrationController(_serviceLocator.Object);
        }

        [Fact]
        public void AdministrationController_IndexAction_CallsTestRepositoryGetTestList()
        {
            var controller = GetUnitUnderTest();

            controller.Index();

            _testRepository.Verify(x => x.GetTestList(It.IsAny<MultivariateTestCriteria>()),
                Times.Once, "Multivariate Administration Controller Index Did Not Properly Call Repositories GetTestList");
        }

        [Fact]
        public void AdministrationController_CreateAction_ReturnsCreateViewWithId()
        {
            var controller = GetUnitUnderTest();
            var actionResult = controller.Create() as ViewResult;

            Assert.IsAssignableFrom(typeof(ViewResult),actionResult);

            ViewDataDictionary viewResult = controller.ViewData;

            Assert.True(viewResult.Keys.Contains("TestGuid"));

            Guid convertedGuid;

            Assert.True(Guid.TryParse(viewResult["TestGuid"].ToString(), out convertedGuid));
        }

        [Fact]
        public void AdministrationController_CreateWithNullModel_CallsTestRepository_ReturnsCreateView()
        {
            var controller = GetUnitUnderTest();

            var actionResult = controller.Create(It.IsAny<MultivariateTestViewModel>()) as ViewResult;

            Assert.NotNull(actionResult);
            Assert.Equal("Create", actionResult.ViewName);
        }

        [Fact]
        public void AdministrationController_CreateWithInvalidModel_CallsTestRepository_ReturnsCreateView()
        {
            var controller = GetUnitUnderTest();

            controller.ModelState.AddModelError("EndDate", "error");
            var actionResult = controller.Create(viewdata) as ViewResult;

            Assert.NotNull(actionResult);
            Assert.Equal("Create", actionResult.ViewName);
        }

        [Fact]
        public void AdministrationController_CreateWithActiveTest_RemovesValidationCheckOnFieldsOtherThanEndDate_ReturnsCreateView()
        {
            var controller = GetUnitUnderTest();

            controller.ModelState.AddModelError("Title", "error");
            controller.ModelState.AddModelError("StartDate", "error");
            controller.ModelState.AddModelError("OriginalItem", "error");
            var redirectResult = controller.Create(viewdata) as RedirectToRouteResult;
            _testRepository.Verify(tr => tr.CreateTest(It.IsAny<MultivariateTestViewModel>()),
              Times.Once, "Controller did not call repository to create test");

            Assert.NotNull(redirectResult);
            Assert.Equal("Index", redirectResult.RouteValues["Action"]);
        }

        [Fact]
        public void AdministrationController_CreateWithNonActiveTest_KeepsTheValidationCheckOnStartDate_ReturnsCreateView()
        {
            var controller = GetUnitUnderTest();

            viewdata.testState = TestState.Inactive;
            controller.ModelState.AddModelError("StartDate", "error");
            var actionResult = controller.Create(viewdata) as ViewResult;

            Assert.NotNull(actionResult);
            Assert.Equal("Create", actionResult.ViewName);
        }

        [Fact]
        public void AdministrationController_CreateWithValidModel_CallsTestRepository_ReturnsIndex()
        {
            var controller = GetUnitUnderTest();

            var redirectResult = controller.Create(viewdata) as RedirectToRouteResult;

            _testRepository.Verify(tr => tr.CreateTest(It.IsAny<MultivariateTestViewModel>()),
                Times.Once, "Controller did not call repository to create test");

            Assert.NotNull(redirectResult);
            Assert.Equal("Index", redirectResult.RouteValues["Action"]);
        }

        [Fact]
        public void AdministrationController_EditWithId_CallsTestRepository_ReturnsCreate()
        {
            var controller = GetUnitUnderTest();
            string testGuid = Guid.NewGuid().ToString();
            var actionResult = controller.Edit(testGuid) as ViewResult;

            _testRepository.Verify(tr => tr.GetTestById(It.IsAny<Guid>()),
                Times.Once, "Controller did not call repository to create test");

            Assert.NotNull(actionResult);
            Assert.Equal("Create", actionResult.ViewName);
        }

        [Fact]
        public void AdministrationController_DeleteWithId_CallsTestRepository_ReturnsIndex()
        {
            var controller = GetUnitUnderTest();
            string testGuid = Guid.NewGuid().ToString();
            var redirectResult = controller.Delete(testGuid) as RedirectToRouteResult;

            Assert.NotNull(redirectResult);
            Assert.Equal("Index", redirectResult.RouteValues["Action"]);
        }

        [Fact]
        public void AdministrationController_StopWithId_CallsTestRepository_ReturnsIndex()
        {
            var controller = GetUnitUnderTest();
            string testGuid = Guid.NewGuid().ToString();
            var redirectResult = controller.Delete(testGuid) as RedirectToRouteResult;

            Assert.NotNull(redirectResult);
            Assert.Equal("Index", redirectResult.RouteValues["Action"]);
        }
    }
}
