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
        public static event Action OnClientReady;

        public static DiscordSocketClient DiscordClient => discordClient;
        private static DiscordSocketClient discordClient;

        public static HttpClient HttpClient => httpClient;
        private static readonly HttpClient httpClient = new();

        private readonly CommandService commandService;
        private readonly IServiceProvider serviceProvider;

        public BotManager(IServiceProvider services)
        {
            Program.Log($"Initializing BotManager services...");

            commandService = services.GetRequiredService<CommandService>();
            discordClient = services.GetRequiredService<DiscordSocketClient>();
            serviceProvider = services;

            // Event handlers
            discordClient.Ready += ClientReadyAsync;
            discordClient.MessageReceived += HandleMessageAsync;
            
            discordClient.ReactionAdded += TopBuildsManager.OnReaction;
            discordClient.ReactionAdded += ReactionRoles.OnReaction;
        }

        public async Task InitializeAsync() => await commandService.AddModulesAsync(Assembly.GetEntryAssembly(), serviceProvider);

        private async Task ClientReadyAsync()
        {
            Program.Log($"BotManager.ClientReadyAsync");

            // Set the online status
            await discordClient.SetStatusAsync(UserStatus.Online);
            // Set the playing status
            await discordClient.SetGameAsync("over Aurai", "", ActivityType.Watching);

            ClientReady = true;
            OnClientReady?.Invoke();

            Program.Log($"ClientReadyAsync finished.");
        }

        private async Task HandleMessageAsync(SocketMessage socketMsg)
        {
            if (socketMsg.Author.Id == DiscordClient.CurrentUser.Id
                || socketMsg is not SocketUserMessage message 
                || message.Channel is IDMChannel)
                return;

            var context = new SocketCommandContext(discordClient, message);
            int argPos = -1;

            // Check if the message was posted in the Outward Discord "Post your builds" channel.
            if (message.Channel.Id == DiscordUtils.POST_YOUR_BUILDS_CHANNELID)
            {
                Program.Log($"Handling post-your-builds message.");

                await BuildPostManager.HandleBuildPost(message);
            }
            // check for commands
            else if (message.HasStringPrefix("!", ref argPos))
            {
                Program.Log($"Handling !command: {message.CleanContent}");

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
