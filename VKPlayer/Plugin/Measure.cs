using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Rainmeter;

using VkNet;
using VkNet.Enums.Filters;
using VkNet.Exception;

using VKPlayer.AudioPlayer;
using VKPlayer.Forms;

namespace VKPlayer.Plugin
{
    /// <summary>
    /// Каждый скин содержит только один подобный класс
    /// </summary>
    public class AudioPlayerSkin : PluginSkin
    {
        public Player AudioPlayer;

        internal VkApi API;
        internal VKAuthorization AuthorizationForm;


        public AudioPlayerSkin(RainmeterAPI api) : base(api) { }

        public override void Created()
        {
            API = new VkApi();
            API.OnTokenExpires += API_OnTokenExpires;

            AudioPlayer = new Player(this);
        }
        public override void Closed()
        {
            AudioPlayer.Dispose();
        }



        public void ExecuteBang(string command)
        {
            if (!API.IsAuthorized)
            {
                AuthorizationForm = new VKAuthorization();
                AuthorizationForm.ConfirmClicked += AuthorizationForm_SubmitClicked;
                AuthorizationForm.ShowDialog();

                if (!API.IsAuthorized)
                    return;

                AudioPlayer.Execute(command);
            }
            else
            {
                AudioPlayer.Execute(command);
            }
        }

        private void API_OnTokenExpires(VkApi api)
        {
            AuthorizationForm = new VKAuthorization();
            AuthorizationForm.ConfirmClicked += AuthorizationForm_SubmitClicked;
            AuthorizationForm.ShowDialog();
        }

        private void AuthorizationForm_SubmitClicked(string login, string pass, string twofactor)
        {
            AuthorizationForm.Close();

            try
            {
                API.Authorize(new ApiAuthParams
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
        public Player Player => AudioPlayerSkin.AudioPlayer;

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