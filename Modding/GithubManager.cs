using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;

namespace GepBot.Modding
{
    public static class GithubManager
    {
        private const string AuthConfigFilePath = "config_github.json";

        internal static string username;
        internal static string password;

        internal static void Init()
        {
            Program.Log($"Initializing GithubManager...");

            if (!File.Exists(AuthConfigFilePath))
            {
                var json = JsonConvert.SerializeObject(new { username = "USERNAME", password = "PASSWORD" }, Formatting.Indented);
                File.WriteAllText(AuthConfigFilePath, json);
                Program.Log("GitHub auth config file did not exist! Created blank file as template.");
                return;
            }

            var authJson = JsonConvert.DeserializeObject<JToken>(File.ReadAllText(AuthConfigFilePath));
            username = authJson["username"].ToString();
            password = authJson["password"].ToString();
        }

        public static Identity GetIdentity() => new("Gep", "@sinai-dev");

        public static Signature GetSignature() => new(GetIdentity(), DateTime.Now);

        public static void CheckCreateRepository(string repoUrl, string pathToPullInto)
        {
            if (!Directory.Exists(pathToPullInto))
            {
                Repository.Clone(repoUrl, pathToPullInto);
                Repository.Init(pathToPullInto);
            }
            else
                Pull(pathToPullInto);
        }

        public static void Pull(string repoPath)
        {
            Program.Log($"Pulling {repoPath}...");

            using var repo = new Repository(repoPath);
            LibGit2Sharp.Commands.Pull(repo, GetSignature(), new PullOptions());
        }

        public static void CommitAndPush(string repoPath, string message)
        {
            Program.Log($"Comitting and pushing {repoPath} with message: {message}");

            using var repo = new Repository(repoPath);
            var sig = GetSignature();

            LibGit2Sharp.Commands.Stage(repo, "*");

            repo.Commit(message, sig, sig);

            var remote = repo.Network.Remotes["origin"];
            var pushRefSpec = @"refs/heads/main";

            var pushOptions = new PushOptions();
            pushOptions.CredentialsProvider = (url, user, types) => new UsernamePasswordCredentials()
            {
                Username = username,
                Password = password,
            };

            repo.Network.Push(remote, pushRefSpec, pushOptions);
        }
    }
}
