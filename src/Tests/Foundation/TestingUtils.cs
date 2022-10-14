using System.Runtime.CompilerServices;

namespace OrleansMultitenant.Tests;

class TestingUtils
{
    /// <summary> Run the predicate until it succeed or times out </summary>
    /// <param name="predicate">The predicate to run</param>
    /// <param name="timeout">The timeout value</param>
    /// <param name="delayOnFail">The time to delay next call upon failure</param>
    /// <returns>True if the predicate succeed, false otherwise</returns>
    internal static async Task WaitUntilAsync(Func<bool, Task<bool>> predicate, TimeSpan timeout, TimeSpan? delayOnFail = null)
    {
        delayOnFail ??= TimeSpan.FromSeconds(1);
        bool[] keepGoing = new[] { true };

        var task = loop();
        try
        {
            _ = await Task.WhenAny(task, Task.Delay(timeout));
        }
        finally
        {
            keepGoing[0] = false;
        }

        await task;

        async Task loop()
        {
            bool passed;
            do
            {
                // need to wait a bit to before re-checking the condition.
                await Task.Delay(delayOnFail.Value);
                passed = await predicate(false);
            }
            while (!passed && keepGoing[0]);
            if (!passed)
                _ = await predicate(true);
        }
    }

    internal static string ThisTestMethodId(string suffix = "", [CallerFilePath] string path = @"\", [CallerMemberName] string method = "")
        => $"{path.Split(@"Tests\").Last().Replace(".cs", "", StringComparison.OrdinalIgnoreCase)}.{method}{suffix}";
}
