using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Discord.Rest;
using GepBot.Modding;

namespace GepBot
{
    class Program
    {
        public const string VERSION = "1.1";

        static void Main() => MainAsync().GetAwaiter().GetResult();

        public static async Task MainAsync()
        {
            Log($"Initializing Gep-bot {VERSION}...");

            using var services = ConfigureServices();
            var client = services.GetRequiredService<DiscordSocketClient>();

            // Handling client logging
            client.Log += Log;
            services.GetRequiredService<CommandService>().Log += Log;

            // Add OnClientReady listeners
            BotManager.OnClientReady += DiscordUtils.Init;
            BotManager.OnClientReady += ModdingServicesManager.Init;

            Log($"Reading token config file...");

            // Get the bot token from the Config.json file.
            string token = GetConfig()["token"].Value<string>();

            Log($"Initializing Discord...");

            // Log in to Discord and start the bot.
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();
            await services.GetRequiredService<BotManager>().InitializeAsync();

            // Update top builds every hour
            IntervalTask.Create(TimeSpan.FromHours(1), TopBuildsManager.UpdateTopBuilds);

            Log($"Initializion complete, awaiting OnClientReady...");

            // Run the bot until it is closed.
            await Task.Delay(-1);
        }

        static JObject GetConfig()
        {
            using StreamReader configJson = new(Path.Combine(Directory.GetCurrentDirectory(), "Config.json"));
            return (JObject)JsonConvert.DeserializeObject(configJson.ReadToEnd());
        }

        static ServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
                {
                    MessageCacheSize = 0,
                    LogLevel = LogSeverity.Info,
                }))
                .AddSingleton(new CommandService(new CommandServiceConfig
                {
                    LogLevel = LogSeverity.Info,
                    DefaultRunMode = RunMode.Async,
                    CaseSensitiveCommands = false
                }))
                .AddSingleton<BotManager>()
                .BuildServiceProvider();
        }

        public static void Log(object o)
        {
            Console.WriteLine($"{DateTime.Now.ToShortTimeString()} | {o ?? "<null log>"}");
        }

        public static Task Log(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }
    }
}
