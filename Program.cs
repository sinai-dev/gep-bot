using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO;

namespace GepBot
{
    class Program
    {
        static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            using var services = ConfigureServices();

            Console.WriteLine("Ready for takeoff...");
            var client = services.GetRequiredService<DiscordSocketClient>();

            client.Log += Log;
            services.GetRequiredService<CommandService>().Log += Log;

            // Get the bot token from the Config.json file.
            JObject config = BotManager.GetConfig();
            string token = config["token"].Value<string>();

            // Log in to Discord and start the bot.
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            await services.GetRequiredService<BotManager>().InitializeAsync();

            // Wait for client to be ready.
            while (!BotManager.ClientReady)
                await Task.Delay(100);

            // Run the automatic updating function forever
            while (true)
            {
                await BotManager.UpdateTopBuilds();
                // one hour delay
                await Task.Delay(3600000);
            }

            //// Run the bot forever.
            //await Task.Delay(-1);
        }

        public static ServiceProvider ConfigureServices()
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

        public static void Log(object o) => Console.WriteLine(o?.ToString());

        public static Task Log(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }
    }
}
