﻿namespace OJS.Web.Tests.Administration.TestsControllerTests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Web;
    using System.Web.Mvc;

    using Ionic.Zip;

    using Moq;

    using NUnit.Framework;

    using OJS.Common;

    [TestFixture]
    public class ImportActionTests : TestsControllerBaseTestsClass
    {
        public ImportActionTests()
        {
            this.File = new Mock<HttpPostedFileBase>();
        }

        private Mock<HttpPostedFileBase> File { get; set; }

        [Test]
        public void ImportActionShouldShowProperRedirectAndMessageWithIncorrectProblemId()
        {
            var redirectResult = this.TestsController.Import("invalid", null, false, false) as RedirectToRouteResult;
            Assert.IsNotNull(redirectResult);

            Assert.AreEqual(GlobalConstants.Index, redirectResult.RouteValues["action"]);

            var tempDataHasKey = this.TestsController.TempData.ContainsKey(GlobalConstants.DangerMessage);
            Assert.IsTrue(tempDataHasKey);

            var tempDataMessage = this.TestsController.TempData[GlobalConstants.DangerMessage];
            Assert.AreEqual("Невалидна задача", tempDataMessage);
        }

        [Test]
        public void ImportActionShouldShowProperRedirectAndMessageWhenProblemDoesNotExist()
        {
            var redirectResult = this.TestsController.Import("100", null, false, false) as RedirectToRouteResult;
            Assert.IsNotNull(redirectResult);

            Assert.AreEqual(GlobalConstants.Index, redirectResult.RouteValues["action"]);

            var tempDataHasKey = this.TestsController.TempData.ContainsKey(GlobalConstants.DangerMessage);
            Assert.IsTrue(tempDataHasKey);

            var tempDataMessage = this.TestsController.TempData[GlobalConstants.DangerMessage];
            Assert.AreEqual("Невалидна задача", tempDataMessage);
        }

        [Test]
        public void ImportActionShouldShowProperRedirectAndMessageWhenFileIsNull()
        {
            var redirectResult = this.TestsController.Import("1", null, false, false) as RedirectToRouteResult;
            Assert.IsNotNull(redirectResult);

            Assert.AreEqual("Problem", redirectResult.RouteValues["action"]);
            Assert.AreEqual(1, redirectResult.RouteValues["id"]);

            var tempDataHasKey = this.TestsController.TempData.ContainsKey(GlobalConstants.DangerMessage);
            Assert.IsTrue(tempDataHasKey);

            var tempDataMessage = this.TestsController.TempData[GlobalConstants.DangerMessage];
            Assert.AreEqual("Файлът не може да бъде празен", tempDataMessage);
        }

        [Test]
        public void ImportActionShouldShowProperRedirectAndMessageWhenFileContentIsZero()
        {
            this.File.Setup(x => x.ContentLength).Returns(0);

            var redirectResult = this.TestsController.Import("1", this.File.Object, false, false) as RedirectToRouteResult;
            Assert.IsNotNull(redirectResult);

            Assert.AreEqual("Problem", redirectResult.RouteValues["action"]);
            Assert.AreEqual(1, redirectResult.RouteValues["id"]);

            var tempDataHasKey = this.TestsController.TempData.ContainsKey(GlobalConstants.DangerMessage);
            Assert.IsTrue(tempDataHasKey);

            var tempDataMessage = this.TestsController.TempData[GlobalConstants.DangerMessage];
            Assert.AreEqual("Файлът не може да бъде празен", tempDataMessage);
        }

        [Test]
        public void ImportActionShouldShowProperRedirectAndMessageWhenFileIsNotZip()
        {
            this.File.Setup(x => x.ContentLength).Returns(1);
            this.File.Setup(x => x.FileName).Returns("filename.invalid");

            var redirectResult = this.TestsController.Import("1", this.File.Object, false, false) as RedirectToRouteResult;
            Assert.IsNotNull(redirectResult);

            Assert.AreEqual("Problem", redirectResult.RouteValues["action"]);
            Assert.AreEqual(1, redirectResult.RouteValues["id"]);

            var tempDataHasKey = this.TestsController.TempData.ContainsKey(GlobalConstants.DangerMessage);
            Assert.IsTrue(tempDataHasKey);

            var tempDataMessage = this.TestsController.TempData[GlobalConstants.DangerMessage];
            Assert.AreEqual("Файлът трябва да бъде .ZIP файл", tempDataMessage);
        }

        [Test]
        public void ImportActionShouldDeleteAllPreviousTestsIfSettingsAreSetToTrue()
        {
            this.File.Setup(x => x.ContentLength).Returns(1);
            this.File.Setup(x => x.FileName).Returns("filename.zip");

            var remainingTests = this.Data.Tests.All().Count(test => test.ProblemId == 1);

            Assert.AreEqual(13, remainingTests);

            try
            {
                this.TestsController.Import("1", this.File.Object, false, true);
            }
            catch (Exception)
            {
                remainingTests = this.Data.Tests.All().Count(test => test.ProblemId == 1);
                Assert.AreEqual(0, remainingTests);
            }
        }

        [Test]
        public void ImportActionShouldReturnProperRedirectAndMessageIfZipFileIsNotValid()
        {
            this.File.Setup(x => x.ContentLength).Returns(1);
            this.File.Setup(x => x.FileName).Returns("filename.zip");
            this.File.Setup(x => x.InputStream).Returns(new MemoryStream());

            var redirectResult = this.TestsController.Import("1", this.File.Object, false, false) as RedirectToRouteResult;
            Assert.IsNotNull(redirectResult);

            Assert.AreEqual("Problem", redirectResult.RouteValues["action"]);
            Assert.AreEqual(1, redirectResult.RouteValues["id"]);

            var tempDataHasKey = this.TestsController.TempData.ContainsKey(GlobalConstants.DangerMessage);
            Assert.IsTrue(tempDataHasKey);

            var tempDataMessage = this.TestsController.TempData[GlobalConstants.DangerMessage];
            Assert.AreEqual("Zip файлът е повреден", tempDataMessage);
        }

        [Test]
        public void ImportActionShouldAddTestsToProblemIfZipFileIsCorrect()
        {
            var zipFile = new ZipFile();

            var inputTest = "input";
            var outputTest = "output";

            zipFile.AddEntry("test.001.in.txt", inputTest);
            zipFile.AddEntry("test.001.out.txt", outputTest);

            var zipStream = new MemoryStream();
            zipFile.Save(zipStream);
            zipStream = new MemoryStream(zipStream.ToArray());

            this.File.Setup(x => x.ContentLength).Returns(1);
            this.File.Setup(x => x.FileName).Returns("file.zip");
            this.File.Setup(x => x.InputStream).Returns(zipStream);

            var redirectResult = this.TestsController.Import("1", this.File.Object, false, false) as RedirectToRouteResult;
            Assert.IsNotNull(redirectResult);

            Assert.AreEqual("Problem", redirectResult.RouteValues["action"]);
            Assert.AreEqual(1, redirectResult.RouteValues["id"]);

            var tests = this.Data.Problems.All().First(pr => pr.Id == 1).Tests.Count;
            Assert.AreEqual(14, tests);

            var tempDataHasKey = this.TestsController.TempData.ContainsKey(GlobalConstants.InfoMessage);
            Assert.IsTrue(tempDataHasKey);

            var tempDataMessage = this.TestsController.TempData[GlobalConstants.InfoMessage];
            Assert.AreEqual("Тестовете са добавени към задачата", tempDataMessage);
        }
    }
}
