using System.Text.Json;

internal static class FakeCodexServer
{
    internal static async Task RunAsync()
    {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        var experimental = false;
        while (await Console.In.ReadLineAsync() is { } line)
        {
            if (Environment.GetEnvironmentVariable("QUOTA_FLOAT_TEST_INVALID_RPC") == "1")
            {
                Console.WriteLine("private-protocol-not-json");
                await Console.Out.FlushAsync();
                continue;
            }
            using var message = JsonDocument.Parse(line);
            if (!message.RootElement.TryGetProperty("id", out var id)) continue;
            var method = message.RootElement.GetProperty("method").GetString();
            if (method == "initialize")
                experimental = message.RootElement.GetProperty("params").GetProperty("capabilities").GetProperty("experimentalApi").GetBoolean();
            if (method == "hooks/list" && !experimental)
                throw new InvalidOperationException("钩子协议需声明实验接口能力");
            object result = method switch
            {
                "initialize" => new { userAgent = "fixture" },
                "config/read" => new { config = new { features = new { hooks = true } } },
                "hooks/list" => HookResult(message.RootElement),
                "account/read" => new { account = new { type = "chatgpt", email = "fixture@demo.local", planType = "plus" } },
                "account/rateLimits/read" => new { rateLimits = new { planType = "plus",
                    primary = new { usedPercent = 20, windowDurationMins = 300, resetsAt = 2000000000 },
                    secondary = new { usedPercent = 55, windowDurationMins = 10080, resetsAt = 2000000000 } } },
                _ => throw new InvalidOperationException("测试不允许的协议方法")
            };
            Console.WriteLine("\ufeff{\"method\":\"fixture/notification\",\"params\":{}}");
            Console.WriteLine(JsonSerializer.Serialize(new { id = id.GetInt32(), result }));
            await Console.Out.FlushAsync();
        }
    }

    private static object HookResult(JsonElement request)
    {
        if (!request.GetProperty("params").GetProperty("cwds").EnumerateArray().Any())
            throw new InvalidOperationException("必须显式查询用户配置范围");
        return new { data = new[] { new { errors = Array.Empty<object>(), hooks = new[] { new
        {
            eventName = "sessionStart", source = "user", handlerType = "command", enabled = true,
            statusMessage = QuotaFloat.Core.CodexSessionHook.Marker, trustStatus = "modified",
            command = QuotaFloat.Core.CodexSessionHook.BuildCommand(Path.Combine(AppContext.BaseDirectory, "CodexQuotaFloat.exe"))
        } } } } };
    }
}
