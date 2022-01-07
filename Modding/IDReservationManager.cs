using Discord;
using Discord.WebSocket;
using LibGit2Sharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GepBot.Modding
{
    public static class IDReservationManager
    {
        const string REPO_URL = "https://github.com/Mefino/ModdingCommunityResources/";
        const string LOCAL_REPO_PATH = "OutwardModdingCommunity_Resources";

        static readonly string IDRANGES_PATH = Path.Combine(LOCAL_REPO_PATH, "id-reservations", "id-reservations.json");
        static readonly string PHOTONVIEWIDS_PATH = Path.Combine(LOCAL_REPO_PATH, "id-reservations", "photon-viewid-reservations.json");

        const long RESERVE_IDS_CHANNELID = 799163523061514241;
        public static SocketTextChannel reserveIdsChannel;

        static readonly Dictionary<string, PendingReservation> pendingReservations = new();

        public static void Init()
        {
            Program.Log($"Initializing IDReservationManager...");

            GithubManager.CheckCreateRepository(REPO_URL, LOCAL_REPO_PATH);

            reserveIdsChannel = (ModdingServicesManager.ModdingDiscord as SocketGuild).GetChannel(RESERVE_IDS_CHANNELID) as SocketTextChannel;
        }

        private static string GetFilePathForType(ReservationType type) => type switch
        {
            ReservationType.ItemOrStatus => IDRANGES_PATH,
            ReservationType.PhotonView => PHOTONVIEWIDS_PATH,
            _ => throw new NotImplementedException()
        };

        private static List<RangeReservation> GetReservations(ReservationType type)
        {
            Program.Log($"Fetching {type} ID reservations...");

            GithubManager.Pull(LOCAL_REPO_PATH);

            string filePath = GetFilePathForType(type);
            var ret = JsonConvert.DeserializeObject<RangeReservation[]>(File.ReadAllText(filePath)).ToList();
            ret.Sort(new ReservationComparer(type != ReservationType.ItemOrStatus));
            return ret;
        }

        public static async Task Cmd_RequestRangeReservation(SocketUser user, ReservationType type)
        {
            Program.Log($"Processing {type} ID request for {user.Username}...");

            if (pendingReservations.Any())
            {
                var existing = pendingReservations.First().Value;
                await reserveIdsChannel.SendMessageAsync(
                    $"A reservation is already pending. Please wait a few seconds for {existing.reservation.name} to !confirm.");
                return;
            }

            var ranges = GetReservations(type);
            int start = 0;
            int end = 0;
            
            if (type == ReservationType.ItemOrStatus)
            {
                foreach (var range in ranges)
                {
                    if (range.start > 0)
                        continue;
                    if (range.start == start)
                    {
                        start -= 1000;
                        end = start - 999;
                    }
                    else
                        break;
                }
            }
            else if (type == ReservationType.PhotonView)
            {
                start = 910;
                end = 919;
                foreach (var range in ranges)
                {
                    if (range.start == start)
                    {
                        start = range.end + 1;
                        end = start + 9;
                    }
                    else
                        break;
                }
            }

            var reservation = new RangeReservation(start, end, user.Username);
            pendingReservations.Add(user.Username, new PendingReservation(reservation, type));

            DeletePendingReservationAfterDelay(user.Username).GetAwaiter();

            await reserveIdsChannel.SendMessageAsync(
                $"{user.Mention} **Reply with `!confirm` to accept.** Your {type} range will be `{reservation.start} -> {reservation.end}`.");
        }

        public static async Task Cmd_Confirm(string forUsername)
        {
            if (pendingReservations.TryGetValue(forUsername, out PendingReservation pending))
            {
                Program.Log($"Confirming reservation for {forUsername}...");

                pendingReservations.Remove(forUsername);

                var range = pending.reservation;
                var type = pending.type;
                string filePath = GetFilePathForType(type);

                // Add the range to the array, then write to the json file
                var ranges = GetReservations(type);
                ranges.Add(range);
                File.WriteAllText(filePath, JsonConvert.SerializeObject(ranges.ToArray(), Formatting.Indented));

                // Push it to the repository
                GithubManager.CommitAndPush(LOCAL_REPO_PATH, $"Reserving {type} ID {range.start} -> {range.end} for {forUsername}");

                await reserveIdsChannel.SendMessageAsync(
                    $"Thank you for confirming. Reserved {type} IDs `{range.start} -> {range.end}` for {forUsername}.", false);
            }
            else
                await reserveIdsChannel.SendMessageAsync($"Sorry! There are no pending reservations for you, {forUsername}.");
        }

        public static async Task Cmd_GetRanges(ReservationType type)
        {
            Program.Log($"Listing reserved IDs for {type}...");

            var ranges = GetReservations(type);

            var sb = new StringBuilder();
            sb.AppendLine($"The following {type} ranges are reserved:");
            foreach (var range in ranges)
                sb.AppendLine($"* {range.name}: `{range.start} -> {range.end}`");

            await reserveIdsChannel.SendMessageAsync(sb.ToString());
        }

        private static async Task DeletePendingReservationAfterDelay(string forUsername)
        {
            await Task.Delay(30 * 1000); // 30 seconds

            if (pendingReservations.ContainsKey(forUsername))
            {
                pendingReservations.Remove(forUsername);
                await reserveIdsChannel.SendMessageAsync(
                    $"Timed out waiting for {forUsername} to confirm their reservation!", false);
            }
        }
    }
}
