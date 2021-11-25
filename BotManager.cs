using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using Discord;
using System.Linq;
using Newtonsoft.Json;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Discord.Rest;

namespace GepBot
{
    public class BotManager
    {
        public static bool ClientReady;

        private static CommandService _commands;
        private static DiscordSocketClient _client;
        private static IServiceProvider _services;

        public static readonly Emoji ThumbsUp = new("👍");
        public static readonly Emoji ThumbsDown = new("👎");

        private static readonly Dictionary<ulong, ulong> buildCategoryChannels = new()
        {
            { 913335663573729290, 913335770566258709 }, // testcategory, top-testcategory
        };

        private const string VALID_BUILD_LINK = @"https://outward.fandom.com/wiki/Build:";

        public BotManager(IServiceProvider services)
        {
            _commands = services.GetRequiredService<CommandService>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _services = services;

            // Event handlers
            _client.Ready += ClientReadyAsync;
            _client.MessageReceived += HandleMessageAsync;
        }

        public static JObject GetConfig()
        {
            using StreamReader configJson = new StreamReader(Directory.GetCurrentDirectory() + @"/Config.json");
            return (JObject)JsonConvert.DeserializeObject(configJson.ReadToEnd());
        }

        public async Task InitializeAsync() => await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

        private async Task ClientReadyAsync()
        {
            // Set the online status
            await _client.SetStatusAsync(UserStatus.Online);
            // Set the playing status
            await _client.SetGameAsync("over Aurai", "", ActivityType.Watching);

            ClientReady = true;
        }

        private async Task HandleMessageAsync(SocketMessage socketMsg)
        {
            if (socketMsg.Author.IsBot || socketMsg is not SocketUserMessage message || message.Channel is IDMChannel)
                return;

            var context = new SocketCommandContext(_client, message);

            // Handle messages posted in the build link categories
            if (buildCategoryChannels.ContainsKey(context.Channel.Id))
            {
                try
                {
                    string content = message.Content.Replace("<", "").Replace(">", "");

                    // Ensure the message.Content is a valid URL and nothing else.
                    Uri uri = new(content);
                    if (!uri.IsAbsoluteUri)
                        throw new ArgumentException("Invalid Wiki link format.");
                    // Ensure it starts with the valid build string.
                    if (!content.StartsWith(VALID_BUILD_LINK))
                        throw new ArgumentException("Invalid Wiki link format.");
                }
                catch 
                {
                    await message.DeleteAsync(new RequestOptions() { AuditLogReason = "Invalid wiki build link." });
                    return;
                }

                // remove embeds
                await message.ModifySuppressionAsync(true);

                // add thumbs reactions
                await message.AddReactionAsync(ThumbsUp); 
                await message.AddReactionAsync(ThumbsDown);
            }
            else // check for bot commands
            {
                int argPos = -1;
                if (message.HasStringPrefix("!", ref argPos))
                {
                    var result = await _commands.ExecuteAsync(context, argPos, _services);

                    if (!result.IsSuccess && result.Error.HasValue)
                        await context.Channel.SendMessageAsync($":x: {result.ErrorReason}");
                }
            }
        }

        public static async Task UpdateTopBuilds()
        {
            Program.Log($"[{DateTime.Now}] Updating top builds");

            foreach (var entry in buildCategoryChannels)
            {
                var buildChannel = _client.GetChannel(entry.Key) as SocketTextChannel;
                var topChannel = _client.GetChannel(entry.Value) as SocketTextChannel;

                // Get the build channel messages and add them into a sorted set
                var messages = await buildChannel.GetMessagesAsync(999).FlattenAsync();
                var sorted = new List<IMessage>(messages);
                sorted.Sort(BuildComparer.Instance);

                // get the message in the top builds channel
                var topMessages = await topChannel.GetMessagesAsync(1).FlattenAsync();
                var topMessage = topMessages.FirstOrDefault() as RestUserMessage;

                // if no message, send one first so we can edit it.
                if (topMessage == null)
                {
                    var sendMsgTask = topChannel.SendMessageAsync("temp");
                    await sendMsgTask;
                    topMessage = sendMsgTask.Result;
                }

                // modify the top message to contain the new sorted top builds
                await topMessage.ModifyAsync((MessageProperties msg) => 
                {
                    var sb = new StringBuilder();
                
                    int count = 0;
                    foreach (var buildMessage in sorted)
                    {
                        count++;
                        string name = ExtractBuildName(buildMessage.Content);
                        int votes = BuildComparer.GetVoteTally(buildMessage);

                        sb.AppendLine($"{count}: **{name}** ({votes}) | {buildMessage.Content}");
                
                        if (count >= 10)
                            break;
                    }
                
                    msg.Content = sb.ToString();
                });

                await topMessage.ModifySuppressionAsync(true);
            }
        }

        private static string ExtractBuildName(string url)
        {
            return url[VALID_BUILD_LINK.Length..].Replace('_', ' ');
        }

        public class BuildComparer : IComparer<IMessage>
        {
            public static readonly BuildComparer Instance = new();

            public int Compare(IMessage x, IMessage y) => GetVoteTally(y).CompareTo(GetVoteTally(x));

            public static int GetVoteTally(IMessage message)
            {
                int votes = 0;
                foreach (var reaction in message.Reactions)
                {
                    if (reaction.Key.Name == ThumbsUp.Name)
                        votes += reaction.Value.ReactionCount;
                    else if (reaction.Key.Name == ThumbsDown.Name)
                        votes -= reaction.Value.ReactionCount;
                    else
                        Console.WriteLine("Unsupported reaction name: " + reaction.Key.Name);
                }
                return votes;
            }
        }
    }
}