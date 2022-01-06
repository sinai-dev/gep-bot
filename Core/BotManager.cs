using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Discord;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Discord.Rest;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using GepBot.Modding;

namespace GepBot
{
    public class BotManager
    {
        public static bool ClientReady { get; private set; }
        public static DiscordSocketClient DiscordClient => discordClient;
        public static HttpClient HttpClient => httpClient;

        private static CommandService commandService;
        private static DiscordSocketClient discordClient;
        private static IServiceProvider serviceProvider;
        private static readonly HttpClient httpClient = new();

        public BotManager(IServiceProvider services)
        {
            commandService = services.GetRequiredService<CommandService>();
            discordClient = services.GetRequiredService<DiscordSocketClient>();
            serviceProvider = services;

            // Event handlers
            discordClient.Ready += ClientReadyAsync;
            discordClient.MessageReceived += HandleMessageAsync;
            discordClient.ReactionAdded += TopBuildsManager.OnReaction;
            discordClient.ReactionAdded += Modding.ReactionRoles.OnReaction;
        }

        public static JObject GetConfig()
        {
            using StreamReader configJson = new StreamReader(Directory.GetCurrentDirectory() + @"/Config.json");
            return (JObject)JsonConvert.DeserializeObject(configJson.ReadToEnd());
        }

        public async Task InitializeAsync() => await commandService.AddModulesAsync(Assembly.GetEntryAssembly(), serviceProvider);

        private async Task ClientReadyAsync()
        {
            // Set the online status
            await discordClient.SetStatusAsync(UserStatus.Online);
            // Set the playing status
            await discordClient.SetGameAsync("over Aurai", "", ActivityType.Watching);

            DiscordUtils.OnDiscordReady();

            // Setup Github stuff
            GithubManager.Init();
            IDReservationManager.Init();

            ClientReady = true;
        }

        private async Task HandleMessageAsync(SocketMessage socketMsg)
        {
            if (socketMsg.Author.Id == DiscordClient.CurrentUser.Id
                || socketMsg is not SocketUserMessage message 
                || message.Channel is IDMChannel)
                return;

            var context = new SocketCommandContext(discordClient, message);

            if (message.Channel.Id == DiscordUtils.POST_YOUR_BUILDS_CHANNELID)
                await BuildPostManager.HandleMessageAsync(message);
            else
            {
                // check for commands
                int argPos = -1;
                if (message.HasStringPrefix("!", ref argPos))
                {
                    await commandService.ExecuteAsync(context, argPos, serviceProvider);

                    //if (!result.IsSuccess && result.Error.HasValue)
                    //    await context.Channel.SendMessageAsync($"This worries me! Unknown command: {context.Message}");
                }
                else
                {
                    // Check for wiki links
                    await WikiLinkManager.CheckMessage(message);
                }
            }
        }
    }
}