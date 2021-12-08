using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Cliptok.Modules
{
    class MessageEvent
    {
        public static Dictionary<string, string[]> wordLists = new();
        readonly static Regex emoji_rx = new("((\u203c|\u2049|\u2139|[\u2194-\u2199]|[\u21a9-\u21aa]|[\u231a-\u231b]|\u23cf|[\u23e9-\u23f3]|[\u23f8-\u23fa]|\u24c2|[\u25aa–\u25ab]|\u25b6|\u25c0|[\u25fb–\u25fe]|[\u2600–\u2604]|\u260E|\u2611|[\u2614–\u2615]|\u2618|\u261D|\u2620|[\u2622–\u2623]|\u2626|\u262A|[\u262E–\u262F]|[\u2638–\u263A]|\u2640|\u2642|[\u2648–\u2653]|[\u265F–\u2660]|\u2663|[\u2665–\u2666]|\u2668|\u267B|[\u267E–\u267F]|[\u2692–\u2697]|\u2699|[\u269B–\u269C]|[\u26A0–\u26A1]|\u26A7|[\u26AA–\u26AB]|[\u26B0–\u26B1]|[\u26BD–\u26BE]|[\u26C4–\u26C5]|\u26C8|[\u26CE–\u26CF]|\u26D1|[\u26D3–\u26D4]|[\u26E9–\u26EA]|[\u26F0–\u26F5]|[\u26F7–\u26FA]|\u26FD|\u2702|\u2705|[\u2708–\u270D]|\u270F|\u2712|\u2714|\u2716|\u271D|\u2721|\u2728|[\u2733–\u2734]|\u2744|\u2747|\u274C|\u274E|[\u2753–\u2755]|\u2757|[\u2763–\u2764]|[\u2795–\u2797]|\u27A1|\u27B0|\u27BF|[\u2934–\u2935]|[\u2B05–\u2B07]|[\u2B1B–\u2B1C]|\u2B50|\u2B55|\u3030|\u303D|\u3297|\u3299|\ud83c[\ud000-\udfff]|\ud83d[\ud000-\udfff]|\ud83e[\ud000-\udfff]))|(<a{0,1}:[a-zA-Z0-9_.]{2,32}:[0-9]+>)");
        readonly static Regex modmaiL_rx = new("User ID: ([0-9]+)");
        readonly static Regex invite_rx = new("(?:discord|discordapp)\\.(?:gg|com\\/invite)\\/([\\w+-]+)");
        public static Dictionary<ulong, DateTime> supportRatelimit = new();

        public static List<string> allowedInviteCodes = new();
        public static List<string> disallowedInviteCodes = new();

        static bool CheckForNaughtyWords(string input, WordListJson naughtyWordList)
        {
            string[] naughtyWords = naughtyWordList.Words;
            input = input.Replace("\0", "");
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
                        string distinctString = new(arrayOfWords[i].Replace(naughty, "#").Distinct().ToArray());
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

        static async Task SendInfringingMessaageAsync(DiscordChannel channel, DiscordMessage infringingMessage, string reason, string messageURL)
        {
            var embed = new DiscordEmbedBuilder()
            .WithDescription(infringingMessage.Content)
            .WithColor(new DiscordColor(0xf03916))
            .WithTimestamp(infringingMessage.Timestamp)
            .WithFooter(
                $"User ID: {infringingMessage.Author.Id}",
                null
            )
            .WithAuthor(
                $"{infringingMessage.Author.Username}#{infringingMessage.Author.Discriminator} in #{infringingMessage.Channel.Name}",
                null,
                infringingMessage.Author.AvatarUrl
            )
            .AddField("Reason", reason, true);
            if (messageURL != null)
                embed.AddField("Message link", $"[`Jump to warning`]({messageURL})", true);

            await channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} Deleted infringing message by {infringingMessage.Author.Mention} in {infringingMessage.Channel.Mention}:", embed);
        }

        static async Task DeleteAndWarnAsync(DiscordMessage message, string reason, DiscordClient client)
        {
            _ = message.DeleteAsync();
            try
            {
                _ = SendInfringingMessaageAsync(Program.logChannel, message, reason, null);
            }
            catch
            {
                // still warn anyway
            }
            DiscordMessage msg = await message.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
            var warning = await Warnings.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), message.Channel, " automatically ");
            await SendInfringingMessaageAsync(Program.badMsgLog, message, reason, warning.ContextLink);
        }

        public static async Task MessageHandlerAsync(DiscordClient client, DiscordMessage message, DiscordChannel channel, bool isAnEdit = false)
        {
            try
            {
                if (message.Author == null)
                    return;

                if (!isAnEdit && message.Author.Id == Program.cfgjson.ModmailUserId && message.Content == "@here" && message.Embeds[0].Footer.Text.Contains("User ID:"))
                {
                    Console.WriteLine($"Processing modmail message {message.Id} in {message.Channel} with {isAnEdit}");
                    var idString = modmaiL_rx.Match(message.Embeds[0].Footer.Text).Groups[1].Captures[0].Value;
                    DiscordMember modmailMember = default;
                    try
                    {
                        modmailMember = await message.Channel.Guild.GetMemberAsync(Convert.ToUInt64(idString));
                    }
                    catch (DSharpPlus.Exceptions.NotFoundException)
                    {
                        return;
                    }

                    DiscordRole muted = message.Channel.Guild.GetRole(Program.cfgjson.MutedRole);
                    if (modmailMember.Roles.Contains(muted))
                    {
                        await channel.SendMessageAsync(null, Warnings.GenerateWarningsEmbed(modmailMember));
                    }
                }

                // handle #giveaways
                if (!isAnEdit && message.Author.Id == Program.cfgjson.GiveawayBot && message.Channel.Id == Program.cfgjson.GiveawaysChannel && message.Content == Program.cfgjson.GiveawayTriggerMessage)
                {
                    string giveawayTitle = message.Embeds[0].Author.Name;

                    if (giveawayTitle.Length > 100)
                    {
                        giveawayTitle = Warnings.Truncate(giveawayTitle, 100, false);
                    }

                    await message.CreateThreadAsync(giveawayTitle, AutoArchiveDuration.Day, "Automatically creating giveaway thread.");
                }

                // Skip DMs, external guilds, and messages from bots, beyond this point.
                if (message.Channel.IsPrivate || message.Channel.Guild.Id != Program.cfgjson.ServerID || message.Author.IsBot)
                    return;

                // Skip messages from moderators beyond this point.
                DiscordMember member = await message.Channel.Guild.GetMemberAsync(message.Author.Id);
                if (Warnings.GetPermLevel(member) >= ServerPermLevel.TrialModerator)
                    return;

                if (message.MentionedUsers.Count > Program.cfgjson.MassMentionBanThreshold)
                {
                    _ = message.DeleteAsync();
                    await channel.Guild.BanMemberAsync(message.Author.Id, 7, $"Mentioned more than {Program.cfgjson.MassMentionBanThreshold} users in one message.");
                }

                bool match = false;

                // Matching word list
                var wordListKeys = Program.cfgjson.WordListList.Keys;
                foreach (string key in wordListKeys)
                {
                    if (Program.cfgjson.WordListList[key].ExcludedChannels.Contains(message.Channel.Id))
                    {
                        continue;
                    }
                    else if (CheckForNaughtyWords(message.Content.ToLower(), Program.cfgjson.WordListList[key]))
                    {
                        string reason = Program.cfgjson.WordListList[key].Reason;
                        try
                        {
                            _ = message.DeleteAsync();
                            await SendInfringingMessaageAsync(Program.logChannel, message, reason, null);
                        }
                        catch
                        {
                            // still warn anyway
                        }

                        if (key == "autoban.txt" && Warnings.GetPermLevel(member) < ServerPermLevel.Tier4)
                        {
                            _ = message.DeleteAsync();
                            await Bans.BanFromServerAsync(message.Author.Id, reason, client.CurrentUser.Id, message.Channel.Guild, 0, message.Channel, default, true);
                            return;
                        }

                        //var tmp = message.Channel.Type;

                        match = true;

                        DiscordMessage msg = await message.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                        var warning = await Warnings.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), message.Channel, " automatically ");
                        await SendInfringingMessaageAsync(Program.badMsgLog, message, reason, warning.ContextLink);
                        return;
                    }
                    if (match)
                        return;
                }

                if (match)
                    return;

                // Mass mentions
                if (message.MentionedUsers.Count >= Program.cfgjson.MassMentionThreshold && Warnings.GetPermLevel(member) < ServerPermLevel.Tier3)
                {
                    string reason = "Mass mentions";
                    try
                    {
                        _ = message.DeleteAsync();
                        _ = SendInfringingMessaageAsync(Program.logChannel, message, reason, null);
                    }
                    catch
                    {
                        // still warn anyway
                    }

                    DiscordMessage msg = await message.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                    var warning = await Warnings.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), message.Channel, " automatically ");
                    await SendInfringingMessaageAsync(Program.badMsgLog, message, reason, warning.ContextLink);
                    return;
                }

                // Unapproved invites
                if (Warnings.GetPermLevel(member) < (ServerPermLevel)Program.cfgjson.InviteTierRequirement)
                {

                    string checkedMessage = message.Content.Replace('\\', '/');

                    if (checkedMessage.Contains("dsc.gg/") ||
                        checkedMessage.Contains("invite.gg/")
                       )
                    {
                        string reason = "Sent an unapproved invite";
                        _ = message.DeleteAsync();
                        try
                        {
                            _ = SendInfringingMessaageAsync(Program.logChannel, message, reason, null);
                        }
                        catch
                        {
                            // still warn anyway
                        }

                        DiscordMessage msg = await message.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**");
                        var warning = await Warnings.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), message.Channel, " automatically ");
                        await SendInfringingMessaageAsync(Program.badMsgLog, message, reason, warning.ContextLink);
                        return;
                    }

                    var matches = invite_rx.Matches(checkedMessage);

                    if (matches.Count > 3)
                    {
                        string reason = "Sent too many invites";
                        await DeleteAndWarnAsync(message, reason, client);
                        return;
                    }

                    foreach (Match currentMatch in matches)
                    {
                        string code = currentMatch.Groups[1].Value;

                        if (allowedInviteCodes.Contains(code) || Program.cfgjson.InviteExclusion.Contains(code))
                        {
                            continue;
                        }
                        else if (disallowedInviteCodes.Contains(code))
                        {
                            string reason = "Sent an unapproved invite";
                            await DeleteAndWarnAsync(message, reason, client);
                            return;
                        }

                        DiscordInvite invite;
                        try
                        {
                            invite = await client.GetInviteByCodeAsync(code);
                        }
                        catch (DSharpPlus.Exceptions.NotFoundException)
                        {
                            allowedInviteCodes.Add(code);
                            continue;
                        }

                        if (invite.Channel.Type == ChannelType.Group || (!Program.cfgjson.InviteExclusion.Contains(code) && !Program.cfgjson.InviteIDExclusion.Contains(invite.Guild.Id)))
                        {
                            disallowedInviteCodes.Add(code);
                            string reason = "Sent an unapproved invite";
                            await DeleteAndWarnAsync(message, reason, client);
                        }
                    }
                }

                // Mass emoji
                if (!Program.cfgjson.UnrestrictedEmojiChannels.Contains(message.ChannelId) && message.Content.Length >= Program.cfgjson.MassEmojiThreshold)
                {
                    char[] tempArray = message.Content.Replace("🏻", "").Replace("🏼", "").Replace("🏽", "").Replace("🏾", "").Replace("🏿", "").ToCharArray();
                    int pos = 0;
                    foreach (char c in tempArray)
                    {

                        if (c == '™' || c == '®' || c == '©')
                        {
                            tempArray[pos] = ' ';
                        }
                        if (c == '\u200d')
                        {
                            tempArray[pos] = ' ';
                            tempArray[pos + 1] = ' ';
                        }
                        ++pos;
                    }
                    string input = new(tempArray);

                    var matches = emoji_rx.Matches(input);
                    if (matches.Count > Program.cfgjson.MassEmojiThreshold)
                    {
                        string reason = "Mass emoji";
                        _ = message.DeleteAsync();

                        if (Warnings.GetPermLevel(member) == ServerPermLevel.Nothing && !Program.db.HashExists("emojiPardoned", message.Author.Id.ToString()))
                        {
                            await Program.db.HashSetAsync("emojiPardoned", member.Id.ToString(), false);
                            DiscordMessage msgOut;
                            if (Program.cfgjson.UnrestrictedEmojiChannels.Count > 0)
                                msgOut = await message.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Information} {message.Author.Mention}, if you want to play around with lots of emoji, please use <#{Program.cfgjson.UnrestrictedEmojiChannels[0]}> to avoid punishment.");
                            else
                                msgOut = await message.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Information} {message.Author.Mention} Your message was automatically deleted for mass emoji.");

                            await SendInfringingMessaageAsync(Program.badMsgLog, message, reason, Warnings.MessageLink(msgOut));
                            return;
                        }

                        string output = $"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**";
                        if (Program.cfgjson.UnrestrictedEmojiChannels.Count > 0 && (!Program.db.HashExists("emojiPardoned", message.Author.Id.ToString()) || Program.db.HashGet("emojiPardoned", message.Author.Id.ToString()) == false))
                        {
                            output += $"\nIf you want to play around with lots of emoji, please use <#{Program.cfgjson.UnrestrictedEmojiChannels[0]}> to avoid punishment.";
                            await Program.db.HashSetAsync("emojiPardoned", member.Id.ToString(), true);
                        }

                        DiscordMessage msg = await message.Channel.SendMessageAsync(output);
                        var warning = await Warnings.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), message.Channel, " automatically ");
                        await SendInfringingMessaageAsync(Program.badMsgLog, message, reason, warning.ContextLink);
                        return;
                    }

                    if (message.Channel.Id == Program.cfgjson.TechSupportChannel && message.Content.Contains($"<@&{Program.cfgjson.CommunityTechSupportRoleID}>"))
                    {
                        if (supportRatelimit.ContainsKey(message.Author.Id))
                        {
                            if (supportRatelimit[message.Author.Id] > DateTime.Now)
                                return;
                            else
                                supportRatelimit.Remove(message.Author.Id);
                        }

                        supportRatelimit.Add(message.Author.Id, DateTime.Now.Add(TimeSpan.FromMinutes(Program.cfgjson.SupportRatelimitMinutes)));

                        DiscordChannel supportLogChannel = await client.GetChannelAsync(Program.cfgjson.SupportLogChannel);
                        var embed = new DiscordEmbedBuilder()
                            .WithTimestamp(DateTime.Now)
                            .WithAuthor(message.Author.Username + '#' + message.Author.Discriminator, null, $"https://cdn.discordapp.com/avatars/{message.Author.Id}/{message.Author.AvatarHash}.png?size=128");

                        var lastMsgs = await message.Channel.GetMessagesBeforeAsync(message.Id, 50);
                        var msgMatch = lastMsgs.FirstOrDefault(m => m.Author.Id == message.Author.Id);

                        if (msgMatch != null)
                        {
                            var matchContent = Warnings.Truncate(string.IsNullOrWhiteSpace(msgMatch.Content) ? "`[No content]`" : msgMatch.Content, 1020, true);
                            embed.AddField("Previous message", matchContent);
                            if (msgMatch.Attachments.Count != 0)
                            {
                                embed.WithImageUrl(msgMatch.Attachments[0].Url);
                            }
                        }

                        var messageContent = Warnings.Truncate(string.IsNullOrWhiteSpace(message.Content) ? "`[No content]`" : message.Content, 1020, true);
                        embed.AddField("Current message", messageContent);
                        if (message.Attachments.Count != 0)
                        {
                            if (embed.ImageUrl == null)
                                embed.WithImageUrl(message.Attachments[0].Url);
                            else
                                embed.ImageUrl = message.Attachments[0].Url;
                        }

                        embed.AddField("Message Link", $"[`Jump to message`](https://discord.com/channels/{message.Channel.Guild.Id}/{message.Channel.Id}/{message.Id})");
                        var logOut = await supportLogChannel.SendMessageAsync(null, embed);
                        _ = logOut.CreateReactionAsync(DiscordEmoji.FromName(client, ":CliptokAcknowledge:", true));
                    }
                }

                // line limit
                var lineCount = CountNewlines(message.Content);

                if (!Program.cfgjson.LineLimitExcludedChannels.Contains(channel.Id)
                    && (lineCount >= Program.cfgjson.IncreasedLineLimit
                    || (lineCount >= Program.cfgjson.LineLimit && Warnings.GetPermLevel(member) < (ServerPermLevel)Program.cfgjson.LineLimitTier)))
                {
                    string reason = "Too many lines in a single message";
                    _ = message.DeleteAsync();

                    if (!Program.db.HashExists("linePardoned", message.Author.Id.ToString()))
                    {
                        await Program.db.HashSetAsync("linePardoned", member.Id.ToString(), false);
                        var msgOut = await message.Channel.SendMessageAsync($"{Program.cfgjson.Emoji.Information} {message.Author.Mention}, your message was deleted for containing too many lines.\n" +
                            $"Please consider using a Pastebin-style website or <#{Program.cfgjson.UnrestrictedEmojiChannels[0]}> to avoid further punishment.");
                        await SendInfringingMessaageAsync(Program.badMsgLog, message, reason, Warnings.MessageLink(msgOut));
                        return;
                    }
                    else
                    {
                        string output = $"{Program.cfgjson.Emoji.Denied} {message.Author.Mention} was automatically warned: **{reason.Replace("`", "\\`").Replace("*", "\\*")}**\n" +
                            $"Please consider using a Pastebin-style website or <#{Program.cfgjson.UnrestrictedEmojiChannels[0]}> to avoid punishment.";
                        DiscordMessage msg = await message.Channel.SendMessageAsync(output);
                        var warning = await Warnings.GiveWarningAsync(message.Author, client.CurrentUser, reason, contextLink: Warnings.MessageLink(msg), message.Channel, " automatically ");
                        await SendInfringingMessaageAsync(Program.badMsgLog, message, reason, warning.ContextLink);
                        return;
                    }

                }

            }
            catch (Exception e)
            {
                client.Logger.LogError(eventId: Program.CliptokEventID, e.ToString());

                var exs = new List<Exception>();
                if (e is AggregateException ae)
                    exs.AddRange(ae.InnerExceptions);
                else
                    exs.Add(e);

                var cliptokChannel = await client.GetChannelAsync(Program.cfgjson.HomeChannel);

                foreach (var ex in exs)
                {

                    var embed = new DiscordEmbedBuilder
                    {
                        Color = new DiscordColor("#FF0000"),
                        Title = "An exception occurred when processing a message event.",
                        Description = $"{Program.cfgjson.Emoji.BSOD} `{e.GetType()}` occurred when processing [this message]({Warnings.MessageLink(message)})!",
                        Timestamp = DateTime.UtcNow
                    };
                    embed.WithFooter(client.CurrentUser.Username, client.CurrentUser.AvatarUrl)
                        .AddField("Message", ex.Message);
                    await cliptokChannel.SendMessageAsync(embed: embed.Build()).ConfigureAwait(false);
                }
            }

        }

        public static int CountNewlines(string input)
        {
            int count = 0;
            int len = input.Length;
            for (int i = 0; i != len; ++i)
                switch (input[i])
                {
                    case '\r':
                        ++count;
                        if (i + 1 != len && input[i + 1] == '\n')
                            ++i;
                        break;
                    case '\n':
                        ++count;
                        break;
                }
            return count;
        }

    }
}
