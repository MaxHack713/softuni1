﻿namespace OJS.Data.Tests.Contest.Date
{
    using System;
    using System.Linq;

    using NUnit.Framework;

    [TestFixture]
    public class TestContestModifiedOnData : TestContestBaseData
    {
        public TestContestModifiedOnData()
        {
            this.PopulateEmptyDataBaseWithContest();
        }

        [Test]
        public void ModifiedOnShouldBeNullOnCreation()
        {
            var result = this.EmptyOjsData.Contests.All()
                .Where(x => x.Name == "Created")
                .Select(x => new
                {
                    x.ModifiedOn
                })
                .FirstOrDefault();

            Assert.IsNull(result.ModifiedOn);
        }

        [Test]
        public void ModifiedOnShouldBeCorrectOnModification()
        {
            this.EmptyOjsData.Contests.All().FirstOrDefault(x => x.Name == "Created").Name = "Modified";

            this.EmptyOjsData.SaveChanges();

            var result =
                this.EmptyOjsData.Contests.All()
                    .Where(x => x.Name == "Modified")
                    .Select(x => new { x.ModifiedOn })
                    .FirstOrDefault();

            var expected = DateTime.Now.AddSeconds(-5) < result.ModifiedOn
                           && result.ModifiedOn < DateTime.Now.AddSeconds(5);

            Assert.IsTrue(expected);
        }
    }
}
