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
        public class ReservationComparer : IComparer<RangeReservation>
        {
            public bool Ascending;

            public ReservationComparer(bool ascending)
            {
                this.Ascending = ascending;
            }

            public int Compare(RangeReservation x, RangeReservation y)
            {
                return Ascending
                    ? x.start.CompareTo(y.start)
                    : y.start.CompareTo(x.start);
            }
        }

        public enum ReservationType
        {
            ItemOrStatus,
            PhotonView,
        }

        public class PendingReservation
        {
            public RangeReservation reservation;
            public ReservationType type;

            public PendingReservation(RangeReservation reservation, ReservationType type)
            {
                this.reservation = reservation;
                this.type = type;
            }
        }

        public struct RangeReservation
        {
            public RangeReservation(int start, int end, string name)
            {
                this.start = start;
                this.end = end;
                this.name = name;
            }

            public int start, end;
            public string name;
        }

        const string REPO_URL = "https://github.com/Mefino/ModdingCommunityResources/";
        const string REPO_PATH = "OutwardModdingCommunity_Resources";

        static readonly string IDRANGES_PATH = Path.Combine(REPO_PATH, "id-reservations", "id-reservations.json");
        static readonly string PHOTONVIEWIDS_PATH = Path.Combine(REPO_PATH, "id-reservations", "photon-viewid-reservations.json");

        const long RESERVE_IDS_CHANNELID = 799163523061514241;

        internal static SocketTextChannel reserveIdsChannel;
        private static readonly Dictionary<string, PendingReservation> pendingReservations = new();

        public static void Init()
        {
            GithubManager.CreateRepository(REPO_URL, REPO_PATH);

            var guild = BotManager.DiscordClient.Guilds.First(it => it.Id == DiscordUtils.OUTWARD_MODDING_DISCORD_ID);
            reserveIdsChannel = (SocketTextChannel)guild.GetChannel(RESERVE_IDS_CHANNELID);
        }

        private static string GetFilePathForType(ReservationType type) => type switch
        {
            ReservationType.ItemOrStatus => IDRANGES_PATH,
            ReservationType.PhotonView => PHOTONVIEWIDS_PATH,
            _ => throw new NotImplementedException()
        };

        private static List<RangeReservation> GetReservations(ReservationType type)
        {
            GithubManager.Pull(REPO_PATH);

            string filePath = GetFilePathForType(type);
            var ret = JsonConvert.DeserializeObject<RangeReservation[]>(File.ReadAllText(filePath)).ToList();
            ret.Sort(new ReservationComparer(type != ReservationType.ItemOrStatus));
            return ret;
        }

        public static async Task Cmd_RequestRangeReservation(SocketUser user, ReservationType type)
        {
            if (pendingReservations.TryGetValue(user.Username, out PendingReservation existing))
            {
                await reserveIdsChannel.SendMessageAsync(
                    $"@{user.Mention} You have a pending reservation already. " +
                    $"Please type !confirm for `{existing.reservation.start} -> {existing.reservation.end}`.");
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
                start = 960;
                end = 969;
                foreach (var range in ranges)
                {
                    if (range.start == start)
                    {
                        start += 10;
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
                pendingReservations.Remove(forUsername);

                var range = pending.reservation;
                var type = pending.type;
                string filePath = GetFilePathForType(type);

                // Add the range to the array, then write to the json file
                var ranges = GetReservations(type);
                ranges.Add(range);
                File.WriteAllText(filePath, JsonConvert.SerializeObject(ranges.ToArray(), Formatting.Indented));

                // Push it to the repository
                GithubManager.CommitAndPush(REPO_PATH, $"Reserving {type} ID {range.start} -> {range.end} for {forUsername}");

                await reserveIdsChannel.SendMessageAsync(
                    $"Thank you for confirming. Reserved {type} IDs `{range.start} -> {range.end}` for {forUsername}.", false);
            }
            else
                await reserveIdsChannel.SendMessageAsync($"Sorry! There are no pending reservations for you, @{forUsername}.");
        }

        private static async Task DeletePendingReservationAfterDelay(string forUsername)
        {
            await Task.Delay(60 * 1000); // 60 seconds

            if (pendingReservations.ContainsKey(forUsername))
            {
                pendingReservations.Remove(forUsername);
                await reserveIdsChannel.SendMessageAsync(
                    $"Timed out waiting for @{forUsername} to confirm their reservation!", false);
            }
        }
    }
}
