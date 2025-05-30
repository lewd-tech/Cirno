﻿namespace Cliptok.CommandChecks
{
    public class ServerPerms
    {
        public enum ServerPermLevel
        {
            Muted = -1,
            Nothing = 0,
            Tier1,
            Tier2,
            Tier3,
            Tier4,
            Tier5,
            Tier6,
            Tier7,
            Tier8,
            TierS,
            TierX,
            TechnicalQueriesSlayer,
            TrialModerator,
            Moderator,
            Admin,
            Owner = int.MaxValue
        }

        public static async Task<ServerPermLevel> GetPermLevelAsync(DiscordMember target)
        {
            if (target is null || target.Guild is null || target.Guild.Id != Program.cfgjson.ServerID)
                return ServerPermLevel.Nothing;

            // Torch approved of this.
            if (target.IsOwner)
                return ServerPermLevel.Owner;
            else if (target.Roles.Contains(await target.Guild.GetRoleAsync(Program.cfgjson.AdminRole)) || target.Id == Program.discord.CurrentUser.Id)
                return ServerPermLevel.Admin;
            else if (target.Roles.Contains(await target.Guild.GetRoleAsync(Program.cfgjson.ModRole)))
                return ServerPermLevel.Moderator;
            else if (target.Roles.Contains(await target.Guild.GetRoleAsync(Program.cfgjson.MutedRole)))
                return ServerPermLevel.Muted;
            else if (target.Roles.Contains(await target.Guild.GetRoleAsync(Program.cfgjson.TrialModRole)))
                return ServerPermLevel.TrialModerator;
            else if (target.Roles.Contains(await target.Guild.GetRoleAsync(Program.cfgjson.TqsRoleId)))
                return ServerPermLevel.TechnicalQueriesSlayer;
            else if (target.Roles.Contains(await target.Guild.GetRoleAsync(Program.cfgjson.TierRoles[9])))
                return ServerPermLevel.TierX;
            else if (target.Roles.Contains(await target.Guild.GetRoleAsync(Program.cfgjson.TierRoles[8])))
                return ServerPermLevel.TierS;
            else if (target.Roles.Contains(await target.Guild.GetRoleAsync(Program.cfgjson.TierRoles[7])))
                return ServerPermLevel.Tier8;
            else if (target.Roles.Contains(await target.Guild.GetRoleAsync(Program.cfgjson.TierRoles[6])))
                return ServerPermLevel.Tier7;
            else if (target.Roles.Contains(await target.Guild.GetRoleAsync(Program.cfgjson.TierRoles[5])))
                return ServerPermLevel.Tier6;
            else if (target.Roles.Contains(await target.Guild.GetRoleAsync(Program.cfgjson.TierRoles[4])))
                return ServerPermLevel.Tier5;
            else if (target.Roles.Contains(await target.Guild.GetRoleAsync(Program.cfgjson.TierRoles[3])))
                return ServerPermLevel.Tier4;
            else if (target.Roles.Contains(await target.Guild.GetRoleAsync(Program.cfgjson.TierRoles[2])))
                return ServerPermLevel.Tier3;
            else if (target.Roles.Contains(await target.Guild.GetRoleAsync(Program.cfgjson.TierRoles[1])))
                return ServerPermLevel.Tier2;
            else if (target.Roles.Contains(await target.Guild.GetRoleAsync(Program.cfgjson.TierRoles[0])))
                return ServerPermLevel.Tier1;
            else
                return ServerPermLevel.Nothing;
        }

        [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
        public class RequireHomeserverPermAttribute : ContextCheckAttribute
        {
            public ServerPermLevel TargetLvl { get; set; }
            public bool WorkOutside { get; set; }

            public bool OwnerOverride { get; set; }

            public RequireHomeserverPermAttribute(ServerPermLevel targetlvl, bool workOutside = false, bool ownerOverride = false)
            {
                WorkOutside = workOutside;
                OwnerOverride = ownerOverride;
                TargetLvl = targetlvl;
            }
        }

        public class RequireHomeserverPermCheck : IContextCheck<RequireHomeserverPermAttribute>
        {
            public async ValueTask<string?> ExecuteCheckAsync(RequireHomeserverPermAttribute attribute, CommandContext ctx)
            {
                // If the command is supposed to stay within the server and its being used outside, fail silently
                if (!attribute.WorkOutside && (ctx.Channel.IsPrivate || ctx.Guild.Id != Program.cfgjson.ServerID))
                    return "This command must be used in the home server, but was executed outside of it.";

                // bot owners can bypass perm checks ONLY if the command allows it.
                if (attribute.OwnerOverride && Program.cfgjson.BotOwners.Contains(ctx.User.Id))
                    return null;

                DiscordMember member;
                if (ctx.Channel.IsPrivate || ctx.Guild.Id != Program.cfgjson.ServerID)
                {
                    var guild = await ctx.Client.GetGuildAsync(Program.cfgjson.ServerID);
                    try
                    {
                        member = await guild.GetMemberAsync(ctx.User.Id);
                    }
                    catch (DSharpPlus.Exceptions.NotFoundException)
                    {
                        return "The invoking user must be a member of the home server; they are not.";
                    }
                }
                else
                {
                    member = ctx.Member;
                }

                var level = await GetPermLevelAsync(member);
                if (level >= attribute.TargetLvl)
                    return null;

                return "The invoking user does not have permission to use this command.";
            }
        }

        public class HomeServerAttribute : ContextCheckAttribute;

        public class HomeServerCheck : IContextCheck<HomeServerAttribute>
        {
            public async ValueTask<string?> ExecuteCheckAsync(HomeServerAttribute attribute, CommandContext ctx)
            {
                return !ctx.Channel.IsPrivate && ctx.Guild.Id == Program.cfgjson.ServerID ? null : "This command must be used in the home server, but was executed outside of it.";
            }
        }
    }
}
