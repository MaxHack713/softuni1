namespace OJS.Tools.OldDatabaseMigration.Copiers
{
    using System;
    using System.Linq;

    using OJS.Common.Extensions;
    using OJS.Common.Models;
    using OJS.Data;
    using OJS.Data.Models;

    internal sealed class SubmissionsCopier : ICopier
    {
        private readonly string contentHeader = new string('=', 115);
        private readonly string contentFooter = new string('-', 115);

        public void Copy(OjsDbContext context, TelerikContestSystemEntities oldDb)
        {
            var tests = context.Tests.Select(x => new { x.Id, x.ProblemId, x.IsTrialTest, x.OrderBy }).ToList();

            var count = oldDb.Submissions.Count();
            const int ElementsByIteration = 500;
            var iterations = Math.Ceiling((decimal)count / ElementsByIteration);
            for (int i = 0; i < iterations; i++)
            {
                GC.Collect();
                var newDb = new OjsDbContext();
                newDb.Configuration.AutoDetectChangesEnabled = false;
                newDb.Configuration.ValidateOnSaveEnabled = false;
                var csharpSubmissionType = newDb.SubmissionTypes.FirstOrDefault(x => x.Name == "C# code");
                var cplusPlusSubmissionType = newDb.SubmissionTypes.FirstOrDefault(x => x.Name == "C++ code");
                var javaScriptSubmissionType =
                    newDb.SubmissionTypes.FirstOrDefault(x => x.Name == "JavaScript code (NodeJS)");

                oldDb = new TelerikContestSystemEntities();
                var dataSource =
                    oldDb.Submissions.AsNoTracking()
                        .Where(x => x.Id != 127774)
                        .OrderBy(x => x.Id)
                        .Skip(i * ElementsByIteration)
                        .Take(ElementsByIteration);

                foreach (var oldSubmission in dataSource)
                {
                    var problem = newDb.Problems.FirstOrDefault(x => x.OldId == oldSubmission.Task);
                    var participant = newDb.Participants.FirstOrDefault(x => x.OldId == oldSubmission.Participant);
                    var submission = new Submission
                                         {
                                             Content = oldSubmission.File.ToText().Compress(),
                                             PreserveCreatedOn = true,
                                             CreatedOn = oldSubmission.SubmittedOn,
                                             Problem = problem,
                                             Participant = participant,
                                             Points = oldSubmission.Points,
                                             Processed = true,
                                             Processing = false,
                                         };

                    switch (oldSubmission.Language)
                    {
                        case "C# Code":
                            submission.SubmissionType = csharpSubmissionType;
                            break;
                        case "C++ Code":
                            submission.SubmissionType = cplusPlusSubmissionType;
                            break;
                        case "JavaScript Code":
                            submission.SubmissionType = javaScriptSubmissionType;
                            break;
                        case "C + + ���":
                            submission.SubmissionType = cplusPlusSubmissionType;
                            break;
                        default:
                            submission.SubmissionType = csharpSubmissionType;
                            break;
                    }

                    var reportFragments = oldSubmission.FullReport.Split(
                        new[] { this.contentHeader },
                            StringSplitOptions.RemoveEmptyEntries);

                    if (string.IsNullOrWhiteSpace(oldSubmission.FullReport) || !reportFragments.Any())
                    {
                        continue;
                    }

                    if (reportFragments.Count() == 1)
                    {
                        // Some kind of exception (e.g. "not enough disk space")
                        var errorFragments = reportFragments[0].Split(
                            new[] { this.contentFooter },
                            StringSplitOptions.RemoveEmptyEntries);
                        submission.IsCompiledSuccessfully = false;
                        submission.CompilerComment = errorFragments[0];
                    }
                    else if (!reportFragments[1].Trim().StartsWith("Compilation successfull!!!"))
                    {
                        submission.IsCompiledSuccessfully = false;
                        var compilerParts = reportFragments[0].Split(
                            new[] { "\r\n" },
                            3,
                            StringSplitOptions.RemoveEmptyEntries);

                        submission.CompilerComment = compilerParts.Count() > 2 ? compilerParts[2].Trim(' ', '\n', '\r', '\t', '-', '=') : null;
                    }
                    else
                    {
                        submission.IsCompiledSuccessfully = true;
                        var compilerParts = reportFragments[0].Split(
                            new[] { "\r\n" },
                            3,
                            StringSplitOptions.RemoveEmptyEntries);
                        submission.CompilerComment = compilerParts.Count() > 2 ? compilerParts[2].Trim(' ', '\n', '\r', '\t', '-', '=') : null;

                        for (int j = 2; j < reportFragments.Length - 1; j++)
                        {
                            var testRunText = reportFragments[j].Trim();
                            var testRunTextParts = testRunText.Split(new[] { this.contentFooter }, StringSplitOptions.None);
                            var testRunTitle = testRunTextParts[0].Trim();
                            var testRunDescription = testRunTextParts[1].Trim() + Environment.NewLine;
                            var isZeroTest = testRunTitle.StartsWith("Zero");

                            var testOrderAsString = testRunTitle.GetStringBetween("�", " ");

                            var testOrder = int.Parse(testOrderAsString);

                            var test =
                                tests.FirstOrDefault(
                                    x =>
                                    x.ProblemId == problem.Id && x.IsTrialTest == isZeroTest && x.OrderBy == testOrder);

                            if (test == null)
                            {
                                continue;
                            }

                            var testRun = new TestRun
                                              {
                                                  MemoryUsed = 0,
                                                  TimeUsed = 0,
                                                  Submission = submission,
                                                  TestId = test.Id,
                                              };

                            if (testRunDescription.StartsWith("Answer correct!!!"))
                            {
                                testRun.ResultType = TestRunResultType.CorrectAnswer;
                                testRun.ExecutionComment = null;
                                testRun.CheckerComment = null;

                                var timeUsedAsString =
                                    testRunDescription.GetStringBetween("Time used (in milliseconds): ", "Memory used");
                                if (timeUsedAsString != null)
                                {
                                    double timeUsed;
                                    if (double.TryParse(timeUsedAsString.Replace(",", ".").Trim(), out timeUsed))
                                    {
                                        testRun.TimeUsed = (int)timeUsed;
                                    }
                                }

                                var memoryUsedAsString = testRunDescription.GetStringBetween(
                                    "Memory used (in bytes): ",
                                    Environment.NewLine);
                                if (memoryUsedAsString != null)
                                {
                                    testRun.MemoryUsed = int.Parse(memoryUsedAsString.Trim());
                                }
                            }
                            else if (testRunDescription.StartsWith("Answer incorrect!"))
                            {
                                testRun.ResultType = TestRunResultType.WrongAnswer;
                                testRun.ExecutionComment = null;
                                testRun.CheckerComment = testRunDescription.GetStringBetween(
                                    "Answer incorrect!",
                                    "Time used"); // Won't work with non-zero tests (will return null)
                                if (testRun.CheckerComment != null)
                                {
                                    testRun.CheckerComment = testRun.CheckerComment.Trim();
                                    if (string.IsNullOrWhiteSpace(testRun.CheckerComment))
                                    {
                                        testRun.CheckerComment = null;
                                    }
                                }

                                var timeUsedAsString =
                                    testRunDescription.GetStringBetween("Time used (in milliseconds): ", "Memory used");
                                if (timeUsedAsString != null)
                                {
                                    double timeUsed;
                                    if (double.TryParse(timeUsedAsString.Replace(",", ".").Trim(), out timeUsed))
                                    {
                                        testRun.TimeUsed = (int)timeUsed;
                                    }
                                }

                                var memoryUsedAsString = testRunDescription.GetStringBetween(
                                    "Memory used (in bytes): ",
                                    Environment.NewLine);
                                if (memoryUsedAsString != null)
                                {
                                    testRun.MemoryUsed = int.Parse(memoryUsedAsString.Trim());
                                }
                            }
                            else if (testRunDescription.StartsWith("Runtime error:"))
                            {
                                testRun.ResultType = TestRunResultType.RunTimeError;
                                testRun.ExecutionComment = testRunDescription.Replace("Runtime error:", string.Empty).Trim();
                                testRun.CheckerComment = null;
                                testRun.TimeUsed = 0;
                                testRun.MemoryUsed = 0;
                            }
                            else if (testRunDescription.StartsWith("Time limit!"))
                            {
                                testRun.ResultType = TestRunResultType.TimeLimit;
                                testRun.ExecutionComment = null;
                                testRun.CheckerComment = null;
                                testRun.TimeUsed = problem.TimeLimit;
                                testRun.MemoryUsed = 0;
                            }
                            else
                            {
                                testRun.ResultType = TestRunResultType.RunTimeError;
                                testRun.ExecutionComment = testRunDescription.Trim();
                                testRun.CheckerComment = null;
                                testRun.TimeUsed = 0;
                                testRun.MemoryUsed = 0;
                            }

                            newDb.TestRuns.Add(testRun);
                        }
                    }

                    newDb.Submissions.Add(submission);
                }

                newDb.SaveChanges();
                Console.Write(".");
            }
        }
    }
}

/*
 * Id -> New id
 * Participant -> Copy to "Participant"
 * Task -> Copy to "Problem"
 * File -> Copy to "Content"
 * SubmittedOn -> Copy to "SubmittedOn"
 * Language -> Copy to submission type id
 * Process
 * Processed
 * ZeroTestsPassed -> deprecated, can be obtained from TestRuns
 * Points -> deprecated, can be obtained from TestRuns
 * HasFullPoints -> deprecated, can be obtained from Problem.MaximumPoints == Submission.TestRuns...
 * MaxTime -> deprecated, can be obtained from TestRuns
 * MaxMemory -> deprecated, can be obtained from TestRuns
 * ShortReport -> deprecated, can be obtained from TestRuns
 * FullReport
 * FullReportPrivate -> deprecated, empty
 * Status
 */
/*
===================================================================================================================
Trying to compile...
-------------------------------------------------------------------------------------------------------------------
===================================================================================================================
Compilation successfull!!!
-------------------------------------------------------------------------------------------------------------------
===================================================================================================================
Zero test �1 execution successful!
-------------------------------------------------------------------------------------------------------------------
Answer incorrect!
Expected output:
1

Your output:
2

Time used (in milliseconds): 21.4844
Memory used (in bytes): 1024000
===================================================================================================================
Zero test �2 execution successful!
-------------------------------------------------------------------------------------------------------------------
Answer incorrect!
Expected output:
3

Your output:
0

Time used (in milliseconds): 25.3906
Memory used (in bytes): 94208
===================================================================================================================
Zero test �3 execution successful!
-------------------------------------------------------------------------------------------------------------------
Answer incorrect!
Expected output:
2

Your output:
0

Time used (in milliseconds): 20.5078
Memory used (in bytes): 1024000
===================================================================================================================
Zero test �4 execution failed!
-------------------------------------------------------------------------------------------------------------------
Runtime error:
Unhandled Exception: System.IndexOutOfRangeException: Index was outside the bounds of the array.
   at Test.Program.Main(String[] args)
===================================================================================================================
Zero test �5 execution failed!
-------------------------------------------------------------------------------------------------------------------
Time limit!
===================================================================================================================
Test �1 execution successful!
-------------------------------------------------------------------------------------------------------------------
Answer correct!!!
Time used (in milliseconds): 26.3672
Memory used (in bytes): 94208
===================================================================================================================
Test �2 execution successful!
-------------------------------------------------------------------------------------------------------------------
Answer incorrect!
Time used (in milliseconds): 27.3437
Memory used (in bytes): 98304
===================================================================================================================
Test �3 execution failed!
-------------------------------------------------------------------------------------------------------------------
Time limit!
===================================================================================================================
Test �4 execution failed!
-------------------------------------------------------------------------------------------------------------------
Runtime error:
Unhandled Exception: System.FormatException: Input string was not in a correct format.
   at System.Number.StringToNumber(String str, NumberStyles options, NumberBuffer& number, NumberFormatInfo info, Boolean parseDecimal)
   at System.Number.ParseInt32(String s, NumberStyles style, NumberFormatInfo info)
   at Test.Program.Main(String[] args)
===================================================================================================================
Test �5 execution failed!
-------------------------------------------------------------------------------------------------------------------
Runtime error:
Unhandled Exception: System.FormatException: Input string was not in a correct format.
   at System.Number.StringToNumber(String str, NumberStyles options, NumberBuffer& number, NumberFormatInfo info, Boolean parseDecimal)
   at System.Number.ParseInt32(String s, NumberStyles style, NumberFormatInfo info)
   at Test.Program.Main(String[] args)
===================================================================================================================
Test �6 execution failed!
-------------------------------------------------------------------------------------------------------------------
Runtime error:
Unhandled Exception: System.OverflowException: Value was either too large or too small for an Int32.
   at System.Number.ParseInt32(String s, NumberStyles style, NumberFormatInfo info)
   at Test.Program.Main(String[] args)
===================================================================================================================
Test �7 execution failed!
-------------------------------------------------------------------------------------------------------------------
Runtime error:
Unhandled Exception: System.FormatException: Input string was not in a correct format.
   at System.Number.StringToNumber(String str, NumberStyles options, NumberBuffer& number, NumberFormatInfo info, Boolean parseDecimal)
   at System.Number.ParseInt32(String s, NumberStyles style, NumberFormatInfo info)
   at Test.Program.Main(String[] args)
===================================================================================================================
Test �8 execution failed!
-------------------------------------------------------------------------------------------------------------------
Runtime error:
Unhandled Exception: System.FormatException: Input string was not in a correct format.
   at System.Number.StringToNumber(String str, NumberStyles options, NumberBuffer& number, NumberFormatInfo info, Boolean parseDecimal)
   at System.Number.ParseInt32(String s, NumberStyles style, NumberFormatInfo info)
   at Test.Program.Main(String[] args)
===================================================================================================================
Test �9 execution failed!
-------------------------------------------------------------------------------------------------------------------
Runtime error:
Unhandled Exception: System.FormatException: Input string was not in a correct format.
   at System.Number.StringToNumber(String str, NumberStyles options, NumberBuffer& number, NumberFormatInfo info, Boolean parseDecimal)
   at System.Number.ParseInt32(String s, NumberStyles style, NumberFormatInfo info)
   at Test.Program.Main(String[] args)
===================================================================================================================
Test �10 execution successful!
-------------------------------------------------------------------------------------------------------------------
Answer incorrect!
Time used (in milliseconds): 20.5079
Memory used (in bytes): 765952
===================================================================================================================
Solution accepted
-------------------------------------------------------------------------------------------------------------------
*/