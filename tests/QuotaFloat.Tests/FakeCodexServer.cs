using System.Text.Json;

internal static class FakeCodexServer
{
    internal static async Task RunAsync()
    {
        while (await Console.In.ReadLineAsync() is { } line)
        {
            using var message = JsonDocument.Parse(line);
            if (!message.RootElement.TryGetProperty("id", out var id)) continue;
            var method = message.RootElement.GetProperty("method").GetString();
            object result = method switch
            {
                "initialize" => new { userAgent = "fixture" },
                "account/read" => new { account = new { type = "chatgpt", email = "fixture@demo.local", planType = "plus" } },
                "account/rateLimits/read" => new { rateLimits = new { planType = "plus",
                    primary = new { usedPercent = 20, windowDurationMins = 300, resetsAt = 2000000000 },
                    secondary = new { usedPercent = 55, windowDurationMins = 10080, resetsAt = 2000000000 } } },
                _ => throw new InvalidOperationException("测试不允许的协议方法")
            };
            Console.WriteLine("{\"method\":\"fixture/notification\",\"params\":{}}");
            Console.WriteLine(JsonSerializer.Serialize(new { id = id.GetInt32(), result }));
            await Console.Out.FlushAsync();
        }
    }
}
