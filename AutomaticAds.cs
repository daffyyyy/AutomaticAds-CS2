using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using PlayerSettings;

namespace AutomaticAds;

[MinimumApiVersion(290)]
public class AutomaticAdsBase : BasePlugin, IPluginConfig<BaseConfigs>
{
    public override string ModuleName => "AutomaticAds";
    public override string ModuleVersion => "1.0.9a";
    public override string ModuleAuthor => "luca.uy fork by daffyy";
    public override string ModuleDescription => "I send automatic messages to the chat and play a sound alert for users to see the message.";

    private readonly Dictionary<BaseConfigs.AdConfig, DateTime> _lastAdTimes = new();
    private readonly List<CounterStrikeSharp.API.Modules.Timers.Timer> _timers = [];
    private CounterStrikeSharp.API.Modules.Timers.Timer? _adTimer;
    
    private static readonly Random Random = new();
    private HashSet<int> _playersDisabledAds = [];

    private int _currentAdIndex;
    private string _currentMap = "";

    public override void Load(bool hotReload)
    {
        if (hotReload)
        {
            OnMapStart(string.Empty);
        }

        RegisterListener<Listeners.OnMapEnd>(() => Unload(true));
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        
        // AddCommand("ads_disable", "Disables the AutomaticAds plugin.", (player, commandInfo) =>
        // {
        //
        //     if (player == null) return;
        //     MessageColorFormatter formatter = new MessageColorFormatter();
        //     string formattedPrefix = MessageColorFormatter.FormatMessage(Config.ChatPrefix);
        //
        //     var permissionValidator = new RequiresPermissions("@css/root");
        //     if (!permissionValidator.CanExecuteCommand(player))
        //     {
        //         player.PrintToChat($" {formattedPrefix} {Localizer["NoPermissions"]}");
        //         return;
        //     }
        //
        //     Server.ExecuteCommand("css_plugins unload AutomaticAds");
        //     commandInfo.ReplyToCommand($" {formattedPrefix} {Localizer["Disabled"]}");
        // });
        //
        // AddCommand("ads_enable", "Enable the AutomaticAds plugin.", (player, commandInfo) =>
        // {
        //
        //     if (player == null) return;
        //     MessageColorFormatter formatter = new MessageColorFormatter();
        //     string formattedPrefix = MessageColorFormatter.FormatMessage(Config.ChatPrefix);
        //
        //     var permissionValidator = new RequiresPermissions("@css/root");
        //     if (!permissionValidator.CanExecuteCommand(player))
        //     {
        //         player.PrintToChat($" {formattedPrefix} {Localizer["NoPermissions"]}");
        //         return;
        //     }
        //
        //     Server.ExecuteCommand("css_plugins load AutomaticAds");
        //     commandInfo.ReplyToCommand($" {formattedPrefix} {Localizer["Enabled"]}");
        // });
        //
        // AddCommand("ads_reload", "Reloads the AutomaticAds plugin configuration.", (player, commandInfo) =>
        // {
        //
        //     if (player == null) return;
        //     MessageColorFormatter formatter = new MessageColorFormatter();
        //     string formattedPrefix = MessageColorFormatter.FormatMessage(Config.ChatPrefix);
        //
        //     var permissionValidator = new RequiresPermissions("@css/root");
        //     if (!permissionValidator.CanExecuteCommand(player))
        //     {
        //         player.PrintToChat($" {formattedPrefix} {Localizer["NoPermissions"]}");
        //         return;
        //     }
        //
        //     try
        //     {
        //         Server.ExecuteCommand("css_plugins reload AutomaticAds");
        //         commandInfo.ReplyToCommand($" {formattedPrefix} {Localizer["Reloaded"]}");
        //     }
        //     catch (Exception ex)
        //     {
        //         commandInfo.ReplyToCommand($" {formattedPrefix} {Localizer["FailedToReload"]}: {ex.Message}");
        //     }
        // });
    }
    
    private ISettingsApi? _settings;
    private readonly PluginCapability<ISettingsApi?> _settingsCapability = new("settings:nfcore");
    public override void OnAllPluginsLoaded(bool hotReload)
    {
        try
        {
            _settings = _settingsCapability.Get();
        }
        catch (Exception)
        {
            Logger.LogError("PlayerSettings not installed!");
        }
    }

    public required BaseConfigs Config { get; set; }
    public void OnConfigParsed(BaseConfigs config)
    {
        ValidateConfig(config);
        Config = config;

        SendMessages();
        
        if (Config.GlobalInterval > 0)
            return;
        
        foreach (var ad in Config.Ads.Where(ad => !_lastAdTimes.ContainsKey(ad)))
        {
            _lastAdTimes[ad] = DateTime.MinValue;
        }
    }

    private static void ValidateConfig(BaseConfigs config)
    {
        foreach (var ad in config.Ads)
        {
            if (ad.Interval > 3600)
            {
                ad.Interval = 3600;
            }

            if (ad.Interval < 10)
            {
                ad.Interval = 10;
            }
        }

        if (string.IsNullOrWhiteSpace(config.PlaySoundName))
        {
            config.PlaySoundName = "";
        }
    }

    private void OnMapStart(string mapName)
    {
        _currentMap = Server.MapName;
        
        if (Config.GlobalInterval <= 0)
            SendMessages();
    }

    private void SendMessages()
    {
        if (Config.SendAdsInOrder)
        {
            ScheduleNextAd();
        }
        else
        {
            if (Config.GlobalInterval > 0)
            {
                AddTimer(Config.GlobalInterval, () =>
                {
                    
                    int randomIndex;
                    
                    do
                    {
                        randomIndex = Random.Next(Config.Ads.Count);
                    } while (randomIndex == _currentAdIndex);
                    
                    _currentAdIndex = randomIndex;
                    SendAdToPlayers(Config.Ads[randomIndex]);
                }, TimerFlags.REPEAT);
            }
            else
            {
                foreach (var timer in Config.Ads.Select(ad => AddTimer(1.00f, () =>
                         {
                             if (!CanSendAd(ad)) return;
                             SendAdToPlayers(ad);
                             _lastAdTimes[ad] = DateTime.Now;
                         }, TimerFlags.REPEAT)))
                {
                    _timers.Add(timer);
                }
            }
        }
    }

    private void ScheduleNextAd()
    {
        if (Config.Ads.Count == 0) return;

        var currentAd = Config.Ads[_currentAdIndex];
        
        float interval = currentAd.Interval;

        _adTimer?.Kill();
        _adTimer = AddTimer(interval, () =>
        {
            if (CanSendAd(currentAd))
            {
                SendAdToPlayers(currentAd);
                _lastAdTimes[currentAd] = DateTime.Now;
            }

            _currentAdIndex = (_currentAdIndex + 1) % Config.Ads.Count;
            ScheduleNextAd();
        });
    }

    private bool CanSendAd(BaseConfigs.AdConfig ad)
    {
        if (!_lastAdTimes.TryGetValue(ad, out DateTime value))
        {
            value = DateTime.MinValue;
            _lastAdTimes[ad] = value;
        }

        if (value == DateTime.MinValue)
        {
            return true;
        }

        string currentMap = _currentMap;
        if (ad.Map != "all" && ad.Map != currentMap)
        {
            return false;
        }

        var secondsSinceLastMessage = (int)(DateTime.Now - value).TotalSeconds;

        bool canSend = secondsSinceLastMessage >= ad.Interval;
        return canSend;
    }

    private void SendAdToPlayers(BaseConfigs.AdConfig ad)
    {
        var players = Utilities.GetPlayers();

        if (players.Count == 0)
        {
            return;
        }

        string formattedPrefix = MessageColorFormatter.FormatMessage(Config.ChatPrefix, Config);
        // string formattedMessage = formatter.FormatMessage(ad.Message);

        foreach (var player in players.Where(p => p is { IsValid: true, Connected: PlayerConnectedState.PlayerConnected, IsHLTV: false }))
        {
            if (player.UserId.HasValue && _playersDisabledAds.Contains(player.UserId.Value))
                continue;
            
            bool canView = string.IsNullOrWhiteSpace(ad.ViewFlag) || ad.ViewFlag == "all" || AdminManager.PlayerHasPermissions(player, ad.ViewFlag);
            bool isExcluded = !string.IsNullOrWhiteSpace(ad.ExcludeFlag) && AdminManager.PlayerHasPermissions(player, ad.ExcludeFlag);

            if (!canView || isExcluded) continue;
            
            foreach (var message in MessageColorFormatter.SplitMessages(ad.Message))
            {
                string formattedMessage = MessageColorFormatter.FormatMessage(message, Config, player.PlayerName);
                player.PrintToChat($" {formattedPrefix} {formattedMessage}");
            }

            if (!ad.DisableSound && !string.IsNullOrWhiteSpace(Config.PlaySoundName))
            {
                player.ExecuteClientCommand($"play {Config.PlaySoundName}");
            }
        }
    }

    [GameEventHandler]
    public HookResult OnPlayerActivateEvent(EventPlayerActivate @event, GameEventInfo info)
    {
        if (@event.Userid is not { } player || player.IsBot)
            return HookResult.Continue;

        if (bool.TryParse(_settings?.GetPlayerSettingsValue(player, "Ads_Disabled", "false"), out var adsDisabled) && adsDisabled)
        {
            if (player.UserId.HasValue)
            {
                _playersDisabledAds.Add(player.UserId.Value);
            }
        }

        if (!Config.EnableWelcomeMessage || player is not { IsValid: true, IsBot: false }) return HookResult.Continue;
        foreach (var welcome in Config.Welcome)
        {
            if (string.IsNullOrWhiteSpace(welcome.ViewFlag))
            {
                welcome.ViewFlag = "all";
            }

            if (string.IsNullOrWhiteSpace(welcome.ExcludeFlag))
            {
                welcome.ExcludeFlag = "";
            }

            bool canView = string.IsNullOrWhiteSpace(welcome.ViewFlag) || welcome.ViewFlag == "all" || AdminManager.PlayerHasPermissions(player, welcome.ViewFlag);
            bool isExcluded = !string.IsNullOrWhiteSpace(welcome.ExcludeFlag) && AdminManager.PlayerHasPermissions(player, welcome.ExcludeFlag);

            if (canView && !isExcluded)
            {
                AddTimer(3.0f, () =>
                {
                    string prefix = MessageColorFormatter.FormatMessage(Config.ChatPrefix, Config);

                    foreach (var message in MessageColorFormatter.SplitMessages(welcome.WelcomeMessage))
                    {
                        var formattedMessage = MessageColorFormatter.FormatMessage(message, Config, player.PlayerName);
                        player.PrintToChat($" {prefix} {formattedMessage}");
                    }
                    
                    // string welcomeMessage = formatter.FormatMessage(welcome.WelcomeMessage);

                    if (!welcome.DisableSound && !string.IsNullOrWhiteSpace(Config.PlaySoundName))
                    {
                        player.ExecuteClientCommand($"play {Config.PlaySoundName}");
                    }
                });
            }
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnPlayerDisconnectEvent(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event.Userid is not { } player || player.IsBot)
            return HookResult.Continue;

        if (player.UserId != null)
            _playersDisabledAds.Remove(player.UserId.Value);
        
        return HookResult.Continue;
    }

    public override void Unload(bool hotReload)
    {
        if (Config.GlobalInterval > 0)
            return;
        
        _adTimer?.Kill();
        foreach (var timer in _timers)
        {
            timer.Kill();
        }
        _timers.Clear();
    }
    
    [ConsoleCommand("css_disableads")]
    [ConsoleCommand("css_reklamy")]
    [ConsoleCommand("css_reklama")]
    [ConsoleCommand("css_ads")]
    // [RequiresPermissions("@css/svip")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnDisableAdsCommand(CCSPlayerController? caller, CommandInfo commandInfo)
    {
        if (caller == null)
            return;
        if (!caller.UserId.HasValue)
            return;
        if (!string.IsNullOrEmpty(Config.FlagToDisable) && !AdminManager.PlayerHasPermissions(caller, Config.FlagToDisable))
            return;
        
        string prefix = MessageColorFormatter.FormatMessage(Config.ChatPrefix, Config);
        if (!_playersDisabledAds.Add(caller.UserId.Value))
        {
            _playersDisabledAds.Remove(caller.UserId.Value);
            _settings?.SetPlayerSettingsValue(caller, "Ads_Disabled", "false");
            
            foreach (var message in MessageColorFormatter.SplitMessages(Localizer["Ads_Enabled"]))
            {
                var formattedMessage = MessageColorFormatter.FormatMessage(message, Config, caller.PlayerName);
                caller.PrintToChat($" {prefix} {formattedMessage}");
            }
        }
        else
        {
            _playersDisabledAds.Add(caller.UserId.Value);
            _settings?.SetPlayerSettingsValue(caller, "Ads_Disabled", "true");

            foreach (var message in MessageColorFormatter.SplitMessages(Localizer["Ads_Disabled"]))
            {
                var formattedMessage = MessageColorFormatter.FormatMessage(message, Config, caller.PlayerName);
                caller.PrintToChat($" {prefix} {formattedMessage}");
            }
        }
    }
}
