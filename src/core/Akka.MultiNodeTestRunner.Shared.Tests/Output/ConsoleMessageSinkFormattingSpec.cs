using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.MultiNodeTestRunner.Shared.Reporting;
using Akka.MultiNodeTestRunner.Shared.Sinks;
using Akka.TestKit;
using Xunit;
using Xunit.Abstractions;

namespace Akka.MultiNodeTestRunner.Shared.Tests.Output
{
    public class ConsoleMessageSinkFormattingSpec : AkkaSpec
    {
        private readonly ITestOutputHelper _output;

        public ConsoleMessageSinkFormattingSpec(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TestCoordinatorEnabledMessageSink_should_receive_TestRunTree_when_EndTestRun_is_received()
        {
            var consoleMessageSink = Sys.ActorOf(Props.Create(() => new TestOutputHelperMessageSinkActor(true, true, _output)));
            var nodeIndexes = Enumerable.Range(1, 4).ToArray();
            var nodeTests = NodeMessageHelpers.BuildNodeTests(nodeIndexes);

            var beginSpec = new BeginNewSpec(nodeTests.First().TypeName, nodeTests.First().MethodName, nodeTests);
            consoleMessageSink.Tell(beginSpec);

            // create some messages for each node, the test runner, and some result messages
            // just like a real MultiNodeSpec
            var allMessages = NodeMessageHelpers.GenerateMessageSequence(nodeIndexes, 300);
            var runnerMessages = NodeMessageHelpers.GenerateTestRunnerMessageSequence(20);
            var passMessages = NodeMessageHelpers.GenerateResultMessage(nodeIndexes, true);
            allMessages.UnionWith(runnerMessages);
            allMessages.UnionWith(passMessages);

            foreach (var message in allMessages)
                consoleMessageSink.Tell(message);

            //end the spec
            consoleMessageSink.Tell(new EndSpec());

            //end the test run...
            var sinkReadyToTerminate =
                consoleMessageSink.AskAndWait<MessageSinkActor.SinkCanBeTerminated>(new EndTestRun(),
                    TimeSpan.FromSeconds(10));
            Assert.NotNull(sinkReadyToTerminate);
        }

        private class TestOutputHelperMessageSinkActor : ConsoleMessageSinkActor
        {
            private readonly ITestOutputHelper _output;
            public TestOutputHelperMessageSinkActor(bool useTestCoordinator, bool teamCity, ITestOutputHelper output) : base(useTestCoordinator, teamCity)
            {
                _output = output;
            }

            protected override void WriteSpecMessage(string message, string teamCityWrapper)
            {
                string specMessage = $"[RUNNER][{DateTime.UtcNow.ToShortTimeString()}]: {message}";
                _output.WriteLine(WrapWithTeamCityTag(specMessage, teamCityWrapper));
            }
        }
    }
}
