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
        static void Main() => MainAsync().GetAwaiter().GetResult();

        public static async Task MainAsync()
        {
            using var services = ConfigureServices();

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

            // Update top builds every hour
            IntervalTask.Create(new TimeSpan(1, 0, 0), TopBuildsManager.UpdateTopBuilds);

            // Run the bot until it is closed.
            await Task.Delay(-1);
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

        // Debug stuff

        private static async Task GetGheeyomMessages(DiscordSocketClient client)
        {
            // temp debug
            Console.WriteLine($"Fetching messages...");
            var channel = client.Guilds.First(it => it.Id == DiscordUtils.OUTWARD_DISCORD_ID).GetChannel(245626447568437249) as SocketTextChannel;
            var messages = await channel.GetMessagesAsync(921099326665666670, Direction.After, 5000).FlattenAsync();

            Console.WriteLine($"Sorting {messages.Count()} messages...");
            var sorted = new List<RestUserMessage>();
            var comparer = new Comparison<IMessage>(DiscordUtils.GetOlderMessage);
            foreach (var message in messages)
                sorted.Add(message as RestUserMessage);
            sorted.Sort(comparer);

            Console.WriteLine($"Parsing {sorted.Count} messages...");
            var list = new List<string>();
            foreach (var message in sorted)
            {
                if (message.Author.Id != 152455138152415233)
                    continue;

                if (message.Reference != null)
                {
                    var refMessage = await channel.GetMessageAsync(message.Reference.MessageId.Value);
                    list.Add($"Q: {refMessage.Content}");
                    list.Add($"A: {message.Content}");
                }
                else
                    list.Add(message.Content);
            }

            Console.WriteLine($"Finished parsing, got {list.Count} messages.");
            File.WriteAllLines("gheeyom.txt", list.ToArray());
        }
    }
}
