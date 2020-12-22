using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using MicrosoftBot.Modules;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicrosoftBot
{
    class Program : BaseCommandModule
    {
        public static DiscordClient discord;
        static CommandsNextExtension commands;
        public static Random rnd = new Random();
        public static ConfigJson cfgjson;
        public static ConnectionMultiplexer redis;
        public static IDatabase db;
        public static DiscordChannel logChannel;
        public static List<ulong> processedMessages = new List<ulong>();
        public static Dictionary<string, string[]> wordLists = new Dictionary<string, string[]>();

        static bool CheckForNaughtyWords(string input, WordListJson naughtyWordList)
        {
            string[] naughtyWords = naughtyWordList.Words;
            if (naughtyWordList.WholeWord)
            { 
                input = input.Replace("\'", " ")
                    .Replace("-", " ")
                    .Replace("_", " ")
                    .Replace(".", " ")
                    .Replace(":", " ")
                    .Replace("/", " ")
                    .Replace(",", " ");

                char[] tempArray = input.ToCharArray();

                tempArray = Array.FindAll(tempArray, c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c));
                input = new string(tempArray);

                string[] arrayOfWords = input.Split(' ');

                for (int i = 0; i < arrayOfWords.Length; i++)
                {
                    bool isNaughty = false;
                    foreach (string naughty in naughtyWords)
                    {
                        string distinctString = new string(arrayOfWords[i].Replace(naughty, "#").Distinct().ToArray());
                        if (distinctString.Length <= 3 && arrayOfWords[i].Contains(naughty))
                        {
                            if (distinctString.Length == 1)
                            {
                                isNaughty = true;

                            }
                            else if (distinctString.Length == 2 && (naughty.EndsWith(distinctString[1].ToString()) || naughty.StartsWith(distinctString[0].ToString())))
                            {
                                isNaughty = true;
                            }
                            else if (distinctString.Length == 3 && naughty.EndsWith(distinctString[1].ToString()) && naughty.StartsWith(distinctString[0].ToString()))
                            {
                                isNaughty = true;
                            }
                        }
                        if (arrayOfWords[i] == "")
                        {
                            isNaughty = false;
                        }
                    }
                    if (isNaughty)
                    {
                        return true;
                    }
                }
                return false;
            }
            else
            {
                foreach (string word in naughtyWords)
                {
                    if (input.Contains(word))
                    {
                        return true;
                    }
                }
                return false;
            }

        }

        static void Main(string[] args)
        {
            MainAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] _)
        {
            var json = "";
            using (var fs = File.OpenRead("config.json"))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();

            cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);

            var keys = cfgjson.WordListList.Keys;
            foreach (string key in keys)
            {
                var listOutput = File.ReadAllLines($"Lists/{key}");
                cfgjson.WordListList[key].Words = listOutput;
            }

            string redisHost;
            if (Environment.GetEnvironmentVariable("REDIS_DOCKER_OVERRIDE") != null)
                redisHost = "redis";
            else
                redisHost = cfgjson.Redis.Host;
            redis = ConnectionMultiplexer.Connect($"{redisHost}:{cfgjson.Redis.Port}");
            db = redis.GetDatabase();
            db.KeyDelete("messages");

            discord = new DiscordClient(new DiscordConfiguration
            {
                Token = cfgjson.Core.Token,
                TokenType = TokenType.Bot,
                MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Debug,
                Intents = DiscordIntents.All
            });

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            async Task OnReady(DiscordClient client, ReadyEventArgs e)
            {
                Console.WriteLine($"Logged in as {client.CurrentUser.Username}#{client.CurrentUser.Discriminator}");
                logChannel = await discord.GetChannelAsync(cfgjson.LogChannel);
                Mutes.CheckMutesAsync();
                ModCmds.CheckBansAsync();
            }

            async Task MessageCreated(DiscordClient client, MessageCreateEventArgs e)
            {
                if (e.Channel.IsPrivate || e.Guild.Id != cfgjson.ServerID || e.Author.IsBot)
                {
                    Console.WriteLine("146: true (message shouldn't be processed)");
                    return;
                }
                Console.WriteLine("146: false (message should be processed)");

                if (processedMessages.Contains(e.Message.Id))
                {
                    Console.WriteLine("154: true (message already processed)");
                    return;
                }
                else
                {
                    Console.WriteLine("154: false (message not yet processed, ID: " + e.Message.Id.ToString() + ")");
                    processedMessages.Add(e.Message.Id);
                }

                DiscordMember member = await e.Guild.GetMemberAsync(e.Author.Id);
                if (Warnings.GetPermLevel(member) >= ServerPermLevel.TrialMod)
                {
                    Console.WriteLine("165: true (user trial mod or higher)");
                    return;
                }
                Console.WriteLine("165: false (user not at least trial mod)");

                bool match = false;

                var wordListKeys = cfgjson.WordListList.Keys;
                foreach (string key in wordListKeys)
                {
                    if (CheckForNaughtyWords(e.Message.Content, cfgjson.WordListList[key]))
                    {
                        Console.WriteLine("177: true (message contained naughty words)");
                        try
                        {
                            e.Message.DeleteAsync();
                            DiscordChannel logChannel = await discord.GetChannelAsync(Program.cfgjson.LogChannel);
                            var embed = new DiscordEmbedBuilder()
                                .WithDescription(e.Message.Content)
                                .WithColor(new DiscordColor(0xf03916))
                                .WithTimestamp(e.Message.Timestamp)
                                .WithFooter(
                                    $"User ID: {e.Author.Id}",
                                    null
                                )
                                .WithAuthor(
                                    $"{e.Author.Username}#{e.Author.Discriminator} in #{e.Channel.Name}",
                                    null,
                                    e.Author.AvatarUrl
                                );
                            logChannel.SendMessageAsync($"{cfgjson.Emoji.Denied} Deleted infringing message by {e.Author.Mention} in {e.Channel.Mention}:", false, embed);
                        }
                        catch
                        {
                            // still warn anyway
                            Console.WriteLine("179: exception (embed failed miserably)");
                        }

                        match = true;
                        string reason = cfgjson.WordListList[key].Reason;
                        DiscordMessage msg = await e.Channel.SendMessageAsync($"{cfgjson.Emoji.Denied} {e.Message.Author.Mention} was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                        Warnings.GiveWarningAsync(e.Message.Author, discord.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), e.Channel);
                        return;
                    }
                    
                    Console.WriteLine("177: false (message contained no naughty words)");
                    if (match)
                        return;
                }

                if (match)
                    return;
                    
                Console.WriteLine("213-217: false (no message match)");

                if (e.Message.MentionedUsers.Count >= cfgjson.MassMentionThreshold)
                {
                    Console.WriteLine("222: true (too many mentions)");
                    DiscordChannel logChannel = await discord.GetChannelAsync(Program.cfgjson.LogChannel);
                    try
                    {
                        e.Message.DeleteAsync();
                        var embed = new DiscordEmbedBuilder()
                            .WithDescription(e.Message.Content)
                            .WithColor(new DiscordColor(0xf03916))
                            .WithTimestamp(e.Message.Timestamp)
                            .WithFooter(
                                $"User ID: {e.Author.Id}",
                                null
                            )
                            .WithAuthor(
                                $"{e.Author.Username}#{e.Author.Discriminator} in #{e.Channel.Name}",
                                null,
                                e.Author.AvatarUrl
                            );
                        logChannel.SendMessageAsync($"{cfgjson.Emoji.Denied} Deleted infringing message by {e.Author.Mention} in {e.Channel.Mention}:", false, embed);

                    }
                    catch
                    {
                        // still warn anyway
                        Console.WriteLine("226: exception (embed failed miserably)");
                    }

                    string reason = "Mass mentions";
                    DiscordMessage msg = await e.Channel.SendMessageAsync($"{cfgjson.Emoji.Denied} {e.Message.Author.Mention} was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                    await Warnings.GiveWarningAsync(e.Message.Author, discord.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), e.Channel);
                    return;
                }
                Console.WriteLine("222: false (acceptable number of mentions)");
                if (Warnings.GetPermLevel(member) < (ServerPermLevel)cfgjson.InviteTierRequirement)
                {
                    Console.WriteLine("257: true (user does not meet invite tier requirement)");
                    string inviteExclusion = "microsoft";
                    if (cfgjson.InviteExclusion != null)
                    {
                        Console.WriteLine("261: true (no custom server invite found in config, inviteExclusion: " + inviteExclusion + ")");
                        inviteExclusion = cfgjson.InviteExclusion;
                    }
                    
                    string checkedMessage = e.Message.Content.Replace($"discord.gg/{inviteExclusion}", "").Replace($"discord.com/invite/{inviteExclusion}", "");
                    Console.WriteLine("267: checked message: " + checkedMessage);
                    if (checkedMessage.Contains("discord.gg/") || checkedMessage.Contains("discord.com/invite/"))
                    {
                        Console.WriteLine("269: nice (checked message contains invite)");
                        try
                        {
                            e.Message.DeleteAsync();
                            var embed = new DiscordEmbedBuilder()
                                .WithDescription(e.Message.Content)
                                .WithColor(new DiscordColor(0xf03916))
                                .WithTimestamp(e.Message.Timestamp)
                                .WithFooter(
                                    $"User ID: {e.Author.Id}",
                                    null
                                )
                                .WithAuthor(
                                    $"{e.Author.Username}#{e.Author.Discriminator} in #{e.Channel.Name}",
                                    null,
                                    e.Author.AvatarUrl
                                );
                            logChannel.SendMessageAsync($"{cfgjson.Emoji.Denied} Deleted infringing message by {e.Author.Mention} in {e.Channel.Mention}:", false, embed);

                        }
                        catch
                        {
                            // still warn anyway
                            Console.WriteLine("272: exception (embed failed miserably)");
                        }
                        string reason = "Sent an invite";
                        DiscordMessage msg = await e.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} {e.Message.Author.Mention} was warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                        await Warnings.GiveWarningAsync(e.Message.Author, discord.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), e.Channel);
                        return;
                    }
                    Console.WriteLine("269: not nice (checked message doesn't contain invite)");

                }
                Console.WriteLine("303: end (no more checks to make)");
            }
            

            async Task GuildMemberAdded(DiscordClient client, GuildMemberAddEventArgs e)
            {
                if (e.Guild.Id != cfgjson.ServerID)
                    return;

                if (await db.HashExistsAsync("mutes", e.Member.Id))
                {
                    // todo: store per-guild
                    DiscordRole mutedRole = e.Guild.GetRole(cfgjson.MutedRole);
                    await e.Member.GrantRoleAsync(mutedRole);
                }
            }

            discord.Ready += OnReady;
            discord.MessageCreated += MessageCreated;
            discord.GuildMemberAdded += GuildMemberAdded;


            commands = discord.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefixes = cfgjson.Core.Prefixes
            }); ;

            commands.RegisterCommands<Warnings>();
            commands.RegisterCommands<MuteCmds>();
            commands.RegisterCommands<UserRoleCmds>();
            commands.RegisterCommands<ModCmds>();

            await discord.ConnectAsync();

            while (true)
            {
                await Task.Delay(60000);
                Mutes.CheckMutesAsync();
                ModCmds.CheckBansAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            }

        }
    }


}
