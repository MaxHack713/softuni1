﻿namespace OJS.Web.Tests.Contollers
{
    using System;
    using System.Security.Principal;
    using System.Web;
    using System.Web.Mvc;
    using System.Web.Routing;

    using Moq;

    using NUnit.Framework;

    using OJS.Data.Models;
    using OJS.Tests.Common;
    using OJS.Web.Controllers;
    using OJS.Web.ViewModels.Feedback;

    [TestFixture]
    public class FeedbackControllerTests : TestClassBase
    {
        public const string LoggedUserName = "workshop";

        private HttpContextBase httpContextBasePostCached;

        [Test]
        public void IndexActionShouldReturnViewModel()
        {
            var controller = new FeedbackController(this.EmptyOjsData);
            var result = controller.Index() as ViewResult;

            Assert.IsNotNull(result);
        }

        [Test]
        [ExpectedException(typeof(NullReferenceException))]
        public void IndexActionShouldReturnTheModelIfPostIsNotValid()
        {
            var feedback = new FeedbackViewModel
            {
                Name = "Ivaylo",
                Content = "Test",
            };

            var user = new UserProfile
            {
                UserName = LoggedUserName,
                Email = "bla@bla.com",
            };

            this.EmptyOjsData.Users.Add(user);
            this.EmptyOjsData.SaveChanges();

            var controller = new FeedbackController(this.EmptyOjsData);

            // assign the fake context
            var context = new ControllerContext(this.MockHttpContextBasePost(), new RouteData(), controller);
            controller.ControllerContext = context;

            var result = controller.Index(feedback, true) as ViewResult;
            var model = result.Model as FeedbackReport;

            Assert.AreEqual(model.Name, feedback.Name);
            Assert.AreEqual(model.Content, feedback.Content);
        }

        [Test]
        public void SubmittedShouldReturnNullViewModel()
        {
            var controller = new FeedbackController(this.EmptyOjsData);
            var result = controller.Submitted() as ViewResult;
            var model = result.Model as FeedbackReport;

            Assert.IsNull(model);
        }

        private HttpContextBase MockHttpContextBasePost()
        {
            if (this.httpContextBasePostCached == null)
            {
                var identity = new Mock<IIdentity>();
                identity.SetupGet(p => p.Name).Returns(LoggedUserName);
                var principal = new Mock<IPrincipal>();
                principal.SetupGet(p => p.Identity).Returns(identity.Object);

                var mockHttpContext = new Mock<HttpContextBase>();
                var mockRequest = new Mock<HttpRequestBase>();
                mockHttpContext.SetupGet(x => x.Request).Returns(mockRequest.Object);
                mockHttpContext.SetupGet(p => p.User).Returns(principal.Object);

                mockRequest.SetupGet(x => x.HttpMethod).Returns("POST");

                this.httpContextBasePostCached = mockHttpContext.Object;
            }

            return this.httpContextBasePostCached;
        }
    }
}
