using System.Reflection;
using System.Threading;
using Xunit.Sdk;

namespace Clockwise.AzureServiceBus.Tests
{
    internal class TrackCurrentTestAttribute : BeforeAfterTestAttribute
    {
        private static readonly AsyncLocal<string> currentTestName = new AsyncLocal<string>();

        public override void Before(MethodInfo currentTestMethod)
        {
            currentTestName.Value = currentTestMethod.Name;
        }

        public static  string CurrentTestName => currentTestName.Value;
    }
}