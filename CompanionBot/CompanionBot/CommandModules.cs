﻿using Discord;
using Discord.Addons.CommandsExtension;
using Discord.Commands;
using Discord.WebSocket;
using Interactivity;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CompanionBot
{
    public class General : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _commands;
        private readonly GuildSettings _settings;
        public General(CommandService commands, GuildSettings settings)
        {
            _commands = commands;
            _settings = settings;
        }

        [Command("repost"), Summary("Re-posts a message.")]
        public async Task RepostAsync([Remainder, Summary("The Message to repost.")] string message)
        {
            await ReplyAsync(message);
        }

        [Command("help"), Summary("Shows info on Commands.")]
        public async Task ShowHelpAsync([Summary("The name of the Command you want help for."), Remainder] string commandName = null)
        {
            var embed = _commands.GetDefaultHelpEmbed(commandName, "" + _settings[Context.Guild.Id].Prefix);
            await ReplyAsync(embed: embed);
        }

        [Command("my-settings"), Alias("ms"), Summary("Shows your current Settings.")]
        public Task ShowSettings()
        {
            string response = "Your current Settings are:";
            Settings settings = _settings[Context.Guild.Id];
            response += $"\nPrefix:\t{settings.Prefix}";
            response += $"\nPokeNav Mod-Channel:\t{(settings.PNavChannel != null ? "<#" + settings.PNavChannel + ">" : "none")}";
            return ReplyAsync(response);
        }
    }

    [Name("PoI Management")]
    public class PoIManagement : ModuleBase<SocketCommandContext>
    {
        private readonly MessageQueue _queue;
        public PoIManagement(MessageQueue queue)
        {
            _queue = queue;
        }

        [Command("createmultiple"), Alias("cm"), Summary("Receives data for multiple PoI from the IITC plugin and sends the data one by one for the PokeNav Bot."), RequireWebhook(Group = "Perm"), RequireOwner(Group = "Perm")]
        public async Task CreatePoIAsync([Remainder, Summary("The PoI data from the IITC plugin.")] List<string[]> data)
        {
            //order of params: type name lat lng (isEx)

            List<string> commands = new List<string>();
            foreach (string[] current in data)
            {
                if (current.Length < 4 || current.Length > 5)
                {
                    await ReplyAsync($"Bad Format! Length was {current.Length}, but only 4 or 5 are possible! Skipped the corrupt entry!");
                }
                else
                {
                    if (Enum.TryParse(current[0], out LocationType type))
                        commands.Add($"create poi {type} «{current[1]}» {current[2]} {current[3]}{(current.Length > 4 && current[4] == "1" ? " \"ex_eligible: 1\"" : "")}");
                    else
                        await ReplyAsync($"Bad Format! Unknown Location Type {current[0]}! expected 0 or 1! Skipped the corrupt entry!");
                }
            }
            await _queue.EnqueueCreate(Context, commands);
        }

        [Command("pause"), Alias("p", "stop"), Summary("Pauses the Bulk Export. To start again, run the `resume` Command.")]
        public Task PauseCM()
        {
            return _queue.Pause(Context);
        }

        [Command("resume"), Alias("r", "restart"), Summary("Resume the Bulk Export.")]
        public Task ResumeCM()
        {
            return _queue.Resume(Context);
        }

        [Command("edit"), Alias("e"), Summary("Receives a list of Edits to make from the IITC Plugin, sends the PoI Info Command to obtain the PokeNav id and makes the Edit afterwards."), RequireWebhook(Group = "g"), RequireOwner(Group = "g")]
        public Task Edit([Remainder, Summary("List of Edits to make, provided by the IITC Plugin.")] List<EditData> data)
        {
            return _queue.EnqueueEdit(Context, data);
        }
    }

    [Group("set"), Alias("s"), Name("Configuration"), Summary("Configure the Bot"), RequireUserPermission(GuildPermission.ManageGuild)]
    public class ConfigurationModule : ModuleBase<SocketCommandContext>
    {
        private readonly GuildSettings _settings;
        private readonly InteractivityService _interactive;
        private readonly string prefix;
        public ConfigurationModule(GuildSettings settings, InteractivityService inter, IConfiguration config)
        {
            _settings = settings;
            _interactive = inter;
            prefix = $"<@{config["pokeNavId"]}> ";
        }

        [Command("mod-channel", RunMode = RunMode.Async), Alias("mc"), Summary("Sets the PokeNav Moderation Channel for this Server by sending `show mod-channel`-Command to PokeNav.")]
        public async Task SetModChannel()
        {
            var T = ReplyAsync($"{prefix}show mod-channel");
            var result = await _interactive.NextMessageAsync((message) =>
            {
                return message.Author.Id == 428187007965986826 && message.Channel.Id == Context.Channel.Id && message.MentionedChannels.Count == 1;
            }, null, TimeSpan.FromSeconds(10));
            await T;
            if (result.IsSuccess)
            {
                var channel = result.Value.MentionedChannels.First();
                var currentSettings = _settings[Context.Guild.Id];
                currentSettings.PNavChannel = channel.Id;
                _settings[Context.Guild.Id] = currentSettings;
                await ReplyAsync($"Moderation Channel successfully set to <#{channel.Id}>");
            }
            else
            {
                await ReplyAsync($"Did not receive a Response from PokeNav in time!\nMake sure PokeNav is able to respond in this Channel!");
            }
        }

        [Command("prefix"), Alias("p"), Summary("Sets the Prefix for this Bot on the Server.")]
        public async Task SetPrefix([Summary("The new Prefix for the Bot")] char prefix)
        {
            Settings current = _settings[Context.Guild.Id];
            current.Prefix = prefix;
            _settings[Context.Guild.Id] = current;
            await ReplyAsync($"Prefix successfully set to '{prefix}'.");
        }
    }

    public struct EditData
    {
        // t: type, n: name, a: l**a**titude, o: l**o**ngitude, e: ex-eligibility (or edits on top-level)
        public LocationType t;
        public string n;
        public IDictionary<char, string> e;
    }

    public enum LocationType
    {
        pokestop, gym, none
    }
}
