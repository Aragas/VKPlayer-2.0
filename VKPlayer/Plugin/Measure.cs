using RainMeasure;
using RainMeasure.AudioPlayer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using VkNet;
using VkNet.Enums.Filters;
using VkNet.Exception;
using VKPlayer.Forms;

namespace VKPlayer.Plugin
{
    public class ShitHandler
    {
        public Player AudioPlayer { get; }

        internal VkApi API = new VkApi();
        internal VKAuthorization AuthorizationForm;

        public ShitHandler(PluginSkin skin) { AudioPlayer = new Player(skin); }
    }
    /// <summary>
    /// Каждый скин содержит только один подобный класс
    /// </summary>
    public class AudioPlayerSkin : PluginSkin
    {
        public ShitHandler ShitHandler { get; set; }

        
        public AudioPlayerSkin(RainmeterAPI api) : base(api) { /* AudioPlayer = new Player(this); */ }

        public override void Created()
        {
            ShitHandler = new ShitHandler(this);
        }
        public override void Closed()
        {
        }



        public void ExecuteBang(string command)
        {
            if (!ShitHandler.API.IsAuthorized)
            {
                ShitHandler.API.OnTokenExpires += API_OnTokenExpires;

                ShitHandler.AuthorizationForm = new VKAuthorization();
                ShitHandler.AuthorizationForm.ConfirmClicked += AuthorizationForm_SubmitClicked;
                ShitHandler.AuthorizationForm.ShowDialog();

                if (!ShitHandler.API.IsAuthorized)
                    return;

                ShitHandler.AudioPlayer.Execute(command);
            }
            else
            {
                ShitHandler.AudioPlayer.Execute(command);
            }
        }

        private void API_OnTokenExpires(VkApi api)
        {
            ShitHandler.AuthorizationForm = new VKAuthorization();
            ShitHandler.AuthorizationForm.ConfirmClicked += AuthorizationForm_SubmitClicked;
            ShitHandler.AuthorizationForm.ShowDialog();
        }

        private void AuthorizationForm_SubmitClicked(string login, string pass, string twofactor)
        {
            ShitHandler.AuthorizationForm.Close();

            try
            {
                ShitHandler.API.Authorize(new ApiAuthParams
                {
                    ApplicationId = 3328403,
                    Login = login,
                    Password = pass,
                    Settings = Settings.Audio,
                    TwoFactorAuthorization = () => twofactor,
                });
            }
            catch (VkApiAuthorizationException) { }
        }
    }

    public enum AudioPlayerMeasureEnum
    {
        Credits,
        Artist,
        Title,
        NextArtist,
        NextTitle,
        Duration,
        Position,
        State,
        Repeat,
        Shuffle,
        Volume,
        Progress,
        SaveToFile
    }
    /// <summary>
    /// Каждый скин создает класс для каждого значения.
    /// </summary>
    public class AudioPlayerMeasure : PluginMeasure<AudioPlayerMeasureEnum>
    {
        public AudioPlayerSkin AudioPlayerSkin => (AudioPlayerSkin) Skin;
        public Player Player => AudioPlayerSkin.ShitHandler.AudioPlayer;

        public AudioPlayerMeasure(string pluginType, PluginSkin skin, RainmeterAPI api) : base(pluginType, skin, api) { }

        public override void Reload(RainmeterAPI api, ref double maxValue) { }

        public override double GetNumeric()
        {
            switch (TypeEnum)
            {
                case AudioPlayerMeasureEnum.Duration:
                    return Player.Duration;

                case AudioPlayerMeasureEnum.Position:
                    return Math.Round(Player.Position);

                case AudioPlayerMeasureEnum.State:
                    return (int) Player.PlayingState;

                case AudioPlayerMeasureEnum.Repeat:
                    return Player.Repeat ? 0.0 : 1.0;

                case AudioPlayerMeasureEnum.Shuffle:
                    return Player.Shuffle ? 0.0 : 1.0;

                case AudioPlayerMeasureEnum.SaveToFile:
                    return Player.SaveToFile ? 0.0 : 1.0;

                case AudioPlayerMeasureEnum.Progress:
                    return Player.Progress;

                default:
                    return 0.0;
            }
        }

        public override string GetString()
        {
            switch (TypeEnum)
            {
                case AudioPlayerMeasureEnum.Credits:
                    return "VKPlayer by Aragas (Aragasas)";

                case AudioPlayerMeasureEnum.Artist:
                    return string.IsNullOrEmpty(Player.Artist) ? "Not Initialized" : Player.Artist;

                case AudioPlayerMeasureEnum.Title:
                    return string.IsNullOrEmpty(Player.Title) ? "Click Play" : Player.Title;

                default:
                    return null;
            }
        }

        public override void ExecuteBang(string command) { AudioPlayerSkin.ExecuteBang(command); }

        public override void Finalize()
        {

        }
    }
}