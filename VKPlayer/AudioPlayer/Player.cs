using NAudio.CoreAudioApi;
using NAudio.Wave;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using VkNet.Model;
using VkNet.Model.Attachments;
using VkNet.Model.RequestParams;
using VKPlayer.AudioPlayer;
using VKPlayer.Plugin;

namespace RainMeasure.AudioPlayer
{

    public class Player
    {
        private AudioDownloader AudioDownloader;
        private WaveChannel32 AudioStream;
        private WaveOut OutputDevice = new WaveOut();

        private int _currentIndex;
        private List<Audio> _audioList;
        private List<Audio> AudioList
        {
            get
            {
                if (_audioList != null)
                    return _audioList;

                User user;
                AudioGetParams @params = new AudioGetParams { OwnerId = ((AudioPlayerSkin) Skin).ShitHandler.API.UserId };
                _audioList = new List<Audio>(((AudioPlayerSkin) Skin).ShitHandler.API.Audio.Get(out user, @params));
                return _audioList;
            }
        }


        #region Variables

        public bool Repeat = false;
        public bool Shuffle = false;
        public bool SaveToFile = false;

        public string Artist
        {
            get
            {
                if (_audioList != null)
                    return AudioList[_currentIndex].Artist;
                return string.Empty;
            }
        }
        public string Title
        {
            get
            {
                if (_audioList != null)
                    return AudioList[_currentIndex].Title;
                return string.Empty;
            }
        }

        private Uri Url => AudioList[_currentIndex].Url;

        public double Duration
        {
            get
            {
                if (_audioList != null)
                    return AudioList[_currentIndex].Duration;
                return 0.0;
            }
        }
        public double Position
        {
            get
            {
                if (OutputDevice.PlaybackState == PlaybackState.Stopped)
                    return 0.0;

                return AudioStream.CurrentTime.TotalSeconds;
            }
        }
        public double Progress
        {
            get
            {
                if (OutputDevice.PlaybackState != PlaybackState.Stopped)
                {
                    return Position / Duration;
                }
                return 0.0;
            }
        }

        public PlaybackState PlayingState => OutputDevice.PlaybackState;

        public float Volume { get { return AudioStream.Volume; } private set { AudioStream.Volume = value; } }

        #endregion Variables

        private PluginSkin Skin { get; }
        public string AudioCache
        {
            get
            {
                if (!Directory.Exists(System.IO.Path.Combine(Skin.Path, "AudioCache")))
                    Directory.CreateDirectory(System.IO.Path.Combine(Skin.Path, "AudioCache"));
                return System.IO.Path.Combine(Skin.Path, "AudioCache");
            }
        }

        public Player(PluginSkin skin) { Skin = skin; }

        #region Execute

        public void Execute(string command)
        {
            if (command == "PlayPause") ManagePlay();
            else if (command == "Play") ManagePlay();
            else if (command == "Pause") ManagePlay();
            else if (command == "Stop") Stop();
            else if (command == "Next") PlayNext();
            else if (command == "Previous") PlayPrevious();
            else if (command.Contains("SetVolume")) SetVolume(command.Remove(0, 10));
            else if (command.Contains("SetShuffle")) SetShuffle(command.Remove(0, 11));
            else if (command.Contains("SetRepeat")) SetRepeat(command.Remove(0, 10));
            else if (command.Contains("SetSaveToFile")) SetSaveToFile(command.Remove(0, 14));
            else if (command.Contains("SetPosition")) SetPosition(command.Remove(0, 12));
        }

        private void ManagePlay()
        {
            switch (PlayingState)
            {
                case PlaybackState.Playing:
                    Pause();
                    break;

                case PlaybackState.Paused:
                    Resume();
                    break;

                case PlaybackState.Stopped:
                    Play();
                    break;
            }
        }

        private void Play()
        {
            AudioDownloader?.Stop();

            if(File.Exists(Path.Combine(AudioCache, AudioList[_currentIndex].Id.Value.ToString())))
            {
                PlayFromCache();
                return;
            }

            AudioDownloader = new AudioDownloader(Url);
            AudioDownloader.PreDownloaded += Downloader_PreDownloaded;
            AudioDownloader.Downloaded += Downloader_Downloaded;

            AudioDownloader.DownloadAsync();
        }
        private void PlayFromCache()
        {
            using (var fs = File.OpenRead(Path.Combine(AudioCache, AudioList[_currentIndex].Id.Value.ToString())))
            {
                if (PlayingState != PlaybackState.Stopped)
                    OutputDevice.Stop();

                OutputDevice = new WaveOut();
                AudioStream = new WaveChannel32(new Mp3FileReader(fs)) { PadWithZeroes = false };
                OutputDevice.Init(AudioStream);
                AudioStream.Volume = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).AudioEndpointVolume.MasterVolumeLevelScalar;
                OutputDevice.Play();
            }
        }
        private void Downloader_PreDownloaded(AudioDownloader downloader)
        {
            if (PlayingState != PlaybackState.Stopped)
                OutputDevice.Stop();

            OutputDevice.Dispose();
            OutputDevice = new WaveOut();
            AudioStream = new WaveChannel32(new Mp3FileReader(downloader.Stream)) { PadWithZeroes = false };
            OutputDevice.Init(AudioStream);
            AudioStream.Volume = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).AudioEndpointVolume.MasterVolumeLevelScalar;
            OutputDevice.Play();
        }
        private void Downloader_Downloaded(AudioDownloader downloader)
        {
            if(SaveToFile)
            {
                using (var fs = File.Create(Path.Combine(AudioCache, AudioList[_currentIndex].Id.Value.ToString())))
                {
                    downloader.Stream.CopyTo(fs);
                }
            }
        }
        private void Pause()
        {
            OutputDevice.Pause();
        }
        private void Resume()
        {
            OutputDevice.Resume();
        }
        private void Stop()
        {
            AudioDownloader?.Stop();
            OutputDevice.Stop();
        }

        // Still not working.
        private void SetPosition(string text)
        {
            double value;

            if (PlayingState == PlaybackState.Stopped) return;
            if ((text.StartsWith("+") || text.StartsWith("-")) && double.TryParse(text.Substring(1), out value))
            {
                bool plus = (text.Contains("+"));
                double seconds = value / 100d * Duration;

                if (plus)
                    AudioStream.CurrentTime += TimeSpan.FromSeconds(seconds);
                else
                    AudioStream.CurrentTime -= TimeSpan.FromSeconds(seconds);
            }
            else if (double.TryParse(text, out value))
            {
                double seconds = value / 100d * Duration;
                AudioStream.CurrentTime = TimeSpan.FromSeconds(seconds);
            }
        }

        private void PlayPrevious()
        {
            if (_currentIndex <= 0)
                return;

            _currentIndex -= 1;

            Play();
        }
        private void PlayNext()
        {
            if (_currentIndex >= AudioList.Count)
                return;

            _currentIndex += 1;

            Play();
        }

        private void SetVolume(string text)
        {
            float value;

            if ((text.StartsWith("+") || text.StartsWith("-")) && float.TryParse(text.Substring(1), out value))
            {
                bool plus = (text.StartsWith("+"));
                if (plus)
                    ChangeVolume(+value / 100f);
                else
                    ChangeVolume(-value / 100f);
            }
            else if (float.TryParse(text, out value))
            {
                AudioStream.Volume = value / 100f;
            }
        }
        private void ChangeVolume(float value)
        {
            if (Volume + value >= 1.0f)
            {
                Volume = 1.0f;
                return;
            }

            if (Volume + value <= 0.0f)
            {
                Volume = 0.0f;
                return;
            }

            Volume += value;
        }

        private void SetShuffle(string value)
        {
            switch (value)
            {
                case "1":
                    Shuffle = true;
                    break;

                case "0":
                    Shuffle = false;
                    break;

                case "-1":
                    if (Shuffle) Shuffle = false;
                    else
                    {
                        Shuffle = true;
                        Repeat = false;
                    }
                    break;
            }
        }
        private void SetRepeat(string value)
        {
            switch (value)
            {
                case "1":
                    Repeat = true;
                    break;

                case "0":
                    Repeat = false;
                    break;

                case "-1":
                    if (Repeat) Repeat = false;
                    else
                    {
                        Repeat = true;
                        Shuffle = false;
                    }
                    break;
            }
        }
        private void SetSaveToFile(string value)
        {
            switch (value)
            {
                case "1":
                    SaveToFile = true;
                    break;

                case "0":
                    SaveToFile = false;
                    break;

                case "-1":
                    SaveToFile = !SaveToFile;
                    break;
            }
        }

        #endregion Execute
    }
}