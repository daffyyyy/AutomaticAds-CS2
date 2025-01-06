﻿using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Cvars;

namespace AutomaticAds;

public static class MessageColorFormatter
{
    public static string FormatMessage(string message, string playerName = "")
    {
        // var validColors = new Dictionary<string, string>
        // {
        //     { "{GREEN}", ChatColors.Green.ToString() },
        //     { "{RED}", ChatColors.Red.ToString() },
        //     { "{YELLOW}", ChatColors.Yellow.ToString() },
        //     { "{BLUE}", ChatColors.Blue.ToString() },
        //     { "{PURPLE}", ChatColors.Purple.ToString() },
        //     { "{ORANGE}", ChatColors.Orange.ToString() },
        //     { "{WHITE}", ChatColors.White.ToString() },
        //     { "{NORMAL}", ChatColors.White.ToString() },
        //     { "{GREY}", ChatColors.Grey.ToString() },
        //     { "{LIGHT_RED}", ChatColors.LightRed.ToString() },
        //     { "{LIGHT_BLUE}", ChatColors.LightBlue.ToString() },
        //     { "{LIGHT_PURPLE}", ChatColors.LightPurple.ToString() },
        //     { "{LIGHT_YELLOW}", ChatColors.LightYellow.ToString() },
        //     { "{DARK_RED}", ChatColors.DarkRed.ToString() },
        //     { "{DARK_BLUE}", ChatColors.DarkBlue.ToString() },
        //     { "{BLUE_GREY}", ChatColors.BlueGrey.ToString() },
        //     { "{OLIVE}", ChatColors.Olive.ToString() },
        //     { "{LIME}", ChatColors.Lime.ToString() },
        //     { "{GOLD}", ChatColors.Gold.ToString() },
        //     { "{SILVER}", ChatColors.Silver.ToString() },
        //     { "{MAGENTA}", ChatColors.Magenta.ToString() },
        // };
        //
        // foreach (var color in validColors)
        // {
        //     message = message.Replace(color.Key, color.Value);
        // }

        message = message.ReplaceColorTags();

        message = message.Replace("{playername}", playerName).Replace("{PLAYERNAME}", playerName);
        message = message.Replace("\n", "\u2029");
        message = ReplaceServerVariables(message);

        return message;
    }

    public static string[] SplitMessages(string message)
    {
        return message.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
    }

    private static string ReplaceServerVariables(string message)
    {
        string ip = $"{ConVar.Find("ip")?.StringValue}:{ConVar.Find("hostport")?.GetPrimitiveValue<int>().ToString()}";
        string hostname = ConVar.Find("hostname")?.StringValue ?? "Unknown";
        string map = Server.MapName;
        string time = DateTime.Now.ToString("HH:mm");
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        int players = Utilities.GetPlayers().Count(p => p is { IsBot: false, IsHLTV: false });
        int maxPlayers = Server.MaxPlayers;

        var variables = new Dictionary<string, string>
        {
            { "{ip}", ip },
            { "{hostname}", hostname },
            { "{servername}", hostname },
            { "{map}", map },
            { "{time}", time },
            { "{date}", date },
            { "{players}", players.ToString() },
            { "{maxplayers}", maxPlayers.ToString() }
        };

        return variables.Aggregate(message, (current, variable) => Regex.Replace(current, variable.Key, variable.Value, RegexOptions.IgnoreCase));
    }
}