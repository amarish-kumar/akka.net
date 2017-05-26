//-----------------------------------------------------------------------
// <copyright file="ConsoleMessageSinkActor.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Akka.Actor;
using Akka.Event;
using Akka.MultiNodeTestRunner.Shared.Reporting;

namespace Akka.MultiNodeTestRunner.Shared.Sinks
{
    /// <summary>
    /// <see cref="MessageSinkActor"/> implementation that logs all of its output directly to the <see cref="Console"/>.
    /// 
    /// Has no persistence capabilities. Can optionally use a <see cref="TestRunCoordinator"/> to provide total "end of test" reporting.
    /// </summary>
    public class ConsoleMessageSinkActor : TestCoordinatorEnabledMessageSink
    {
        private readonly bool _teamCity;
        private readonly TeamCityTagGenerator _teamCityTagGenerator = new TeamCityTagGenerator();
        public ConsoleMessageSinkActor(bool useTestCoordinator, bool teamCity) : base(useTestCoordinator)
        {
            _teamCity = teamCity;
        }

        #region Message handling

        protected override void AdditionalReceives()
        {
            Receive<FactData>(data => ReceiveFactData(data));
        }

        protected override void ReceiveFactData(FactData data)
        {
            PrintSpecRunResults(data);
        }

        private void PrintSpecRunResults(FactData data)
        {
            WriteSpecMessage($"Results for {data.FactName}");
            WriteSpecMessage($"Start time: {new DateTime(data.StartTime, DateTimeKind.Utc)}");
            foreach (var node in data.NodeFacts)
            {
                WriteSpecMessage(
                    $" --> Node {node.Value.NodeIndex}:{node.Value.NodeRole} : {(node.Value.Passed.GetValueOrDefault(false) ? "PASS" : "FAIL")} [{node.Value.Elapsed} elapsed]");
            }
            WriteSpecMessage(
                $"End time: {new DateTime(data.EndTime.GetValueOrDefault(DateTime.UtcNow.Ticks), DateTimeKind.Utc)}");
            WriteSpecMessage(
                $"FINAL RESULT: {(data.Passed.GetValueOrDefault(false) ? "PASS" : "FAIL")} after {data.Elapsed}.");

            //If we had a failure
            if (data.Passed.GetValueOrDefault(false) == false)
            {
                WriteSpecMessage("Failure messages by Node");
                foreach (var node in data.NodeFacts)
                {
                    if (node.Value.Passed.GetValueOrDefault(false) == false)
                    {
                        WriteSpecMessage($"<----------- BEGIN NODE {node.Key}:{node.Value.NodeRole} ----------->");
                        foreach (var resultMessage in node.Value.ResultMessages)
                        {
                            WriteSpecMessage($" --> {resultMessage.Message}");
                        }
                        if(node.Value.ResultMessages == null || node.Value.ResultMessages.Count == 0)
                            WriteSpecMessage("[received no messages - SILENT FAILURE].");
                        WriteSpecMessage($"<----------- END NODE {node.Key}:{node.Value.NodeRole} ----------->");
                    }
                }
            }
        }

        protected override void HandleNodeSpecFail(NodeCompletedSpecWithFail nodeFail)
        {
            WriteSpecFail(nodeFail.NodeIndex, nodeFail.NodeRole, nodeFail.Message);

            base.HandleNodeSpecFail(nodeFail);
        }

        protected override void HandleTestRunEnd(EndTestRun endTestRun)
        {
            WriteSpecMessage("Test run complete.");
            
            base.HandleTestRunEnd(endTestRun);
        }

        protected override void HandleTestRunTree(TestRunTree tree)
        {
            var passedSpecs = tree.Specs.Count(x => x.Passed.GetValueOrDefault(false));
            WriteSpecMessage(
                $"Test run completed in [{tree.Elapsed}] with {passedSpecs}/{tree.Specs.Count()} specs passed.");
            foreach (var factData in tree.Specs)
            {
                PrintSpecRunResults(factData);
            }
        }

        protected override void HandleNewSpec(BeginNewSpec newSpec)
        {
            var teamCityWrapper = _teamCity
                ? $@"##teamcity[testStarted name='{newSpec.ClassName}.{newSpec.MethodName}']{Environment.NewLine}"
                : String.Empty;
            WriteSpecMessage($@"Beginning spec {newSpec.ClassName}.{newSpec.MethodName} on {newSpec.Nodes.Count} nodes", teamCityWrapper);

            base.HandleNewSpec(newSpec);
        }

        protected override void HandleEndSpec(EndSpec endSpec)
        {
            WriteSpecMessage("Spec completed.");

            base.HandleEndSpec(endSpec);
        }

        protected override void HandleNodeMessageFragment(LogMessageFragmentForNode logMessage)
        {
            WriteNodeMessage(logMessage);

            base.HandleNodeMessageFragment(logMessage);
        }

        protected override void HandleRunnerMessage(LogMessageForTestRunner node)
        {
            WriteRunnerMessage(node);
            
            base.HandleRunnerMessage(node);
        }

        protected override void HandleNodeSpecPass(NodeCompletedSpecWithSuccess nodeSuccess)
        {
            WriteSpecPass(nodeSuccess.NodeIndex, nodeSuccess.NodeRole, nodeSuccess.Message);

            base.HandleNodeSpecPass(nodeSuccess);
        }

        #endregion

        #region Console output methods

        /// <summary>
        /// Used to print a spec status message (spec starting, finishing, failed, etc...)
        /// </summary>
        protected virtual void WriteSpecMessage(string message, string teamCityWrapper = null)
        {
            string specMessage = $"[RUNNER][{DateTime.UtcNow.ToShortTimeString()}]: {message}";
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            if (_teamCity && teamCityWrapper != null)
            {
                Console.WriteLine(WrapWithTeamCityTag(specMessage, teamCityWrapper));
            }
            else
            {
                Console.WriteLine(specMessage);
            }
            Console.ResetColor();
        }

        protected virtual void WriteSpecPass(int nodeIndex, string nodeRole, string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[NODE{0}:{1}][{2}]: SPEC PASSED: {3}", nodeIndex, nodeRole, DateTime.UtcNow.ToShortTimeString(), message);
            Console.ResetColor();
        }

        protected virtual void WriteSpecFail(int nodeIndex, string nodeRole, string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[NODE{0}:{1}][{2}]: SPEC FAILED: {3}", nodeIndex, nodeRole, DateTime.UtcNow.ToShortTimeString(), message);
            Console.ResetColor();
        }

        protected virtual void WriteRunnerMessage(LogMessageForTestRunner nodeMessage)
        {
            Console.ForegroundColor = ColorForLogLevel(nodeMessage.Level);
            Console.WriteLine(nodeMessage.ToString());
            Console.ResetColor();
        }

        protected virtual void WriteNodeMessage(LogMessageFragmentForNode nodeMessage)
        {
            Console.WriteLine(nodeMessage.ToString());
        }

        protected virtual string WrapWithTeamCityTag(string message, string teamCityMessage)
        {
            return $@"{teamCityMessage}{message}";
        }

        private static ConsoleColor ColorForLogLevel(LogLevel level)
        {
            var color = ConsoleColor.DarkGray;
            switch (level)
            {
                case LogLevel.DebugLevel:
                    color = ConsoleColor.Gray;
                    break;
                case LogLevel.InfoLevel:
                    color = ConsoleColor.White;
                    break;
                case LogLevel.WarningLevel:
                    color = ConsoleColor.Yellow;
                    break;
                case LogLevel.ErrorLevel:
                    color = ConsoleColor.Red;
                    break;
            }

            return color;
        }

        private enum TeamCityTag
        {   
            TestSuiteStarted,
            TestStarted,
            TestSuiteFinished,
            TestFinished,
            TestFailed,
            TestPassed
        }

        private class TeamCityTagGenerator
        {
            private readonly Dictionary<TeamCityTag, string> _tagMapping = 
                new Dictionary<TeamCityTag, string>();

            public TeamCityTagGenerator()
            {
                _tagMapping.Add(TeamCityTag.TestSuiteStarted, "testSuiteStarted");
                _tagMapping.Add(TeamCityTag.TestStarted, "testStarted");
                _tagMapping.Add(TeamCityTag.TestSuiteFinished, "testSuiteFinished");
                _tagMapping.Add(TeamCityTag.TestFinished, "testFinished");
                _tagMapping.Add(TeamCityTag.TestFailed, "testFailed");
                _tagMapping.Add(TeamCityTag.TestPassed, "testPassed");
            }

            private string GenerateTeamCityTag(TeamCityTag tag)
            {
                string stringTag;
                if (_tagMapping.TryGetValue(tag, out stringTag)) { return stringTag; }
                throw new KeyNotFoundException();
            }
        }

        private static string EscapeForTeamCity(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input.Replace("|", "||")
                .Replace("'", "|'")
                .Replace("\n", "|n")
                .Replace("\r", "|r")
                .Replace(char.ConvertFromUtf32(int.Parse("0086", NumberStyles.HexNumber)), "|x")
                .Replace(char.ConvertFromUtf32(int.Parse("2028", NumberStyles.HexNumber)), "|l")
                .Replace(char.ConvertFromUtf32(int.Parse("2029", NumberStyles.HexNumber)), "|p")
                .Replace("[", "|[")
                .Replace("]", "|]");
        }

        #endregion
    }

    /// <summary>
    /// <see cref="IMessageSink"/> implementation that writes directly to the console.
    /// </summary>
    public class ConsoleMessageSink : MessageSink
    {
        public ConsoleMessageSink(bool teamCity)
            : base(Props.Create(() => new ConsoleMessageSinkActor(true, teamCity)))
        {
        }

        protected override void HandleUnknownMessageType(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Unknown message: {0}", message);
            Console.ResetColor();
        }
    }
}

