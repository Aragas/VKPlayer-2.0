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

namespace Rainmeter.AudioPlayer
{

    public static class Player
    {
        private static AudioDownloader AudioDownloader;
        private static WaveChannel32 AudioStream;
        private static WaveOut OutputDevice = new WaveOut();
        
        private static int _currentIndex;
        private static List<Audio> _audioList;
        private static List<Audio> AudioList
        {
            get
            {
                if (_audioList != null)
                    return _audioList;

                User user;
                AudioGetParams @params = new AudioGetParams { OwnerId = MeasureHandler.API.UserId };
                _audioList = new List<Audio>(MeasureHandler.API.Audio.Get(out user, @params));
                return _audioList;
            }
        }


        #region Variables

        public static bool Repeat = false;
        public static bool Shuffle = false;
        public static bool SaveToFile = false;

        public static string Artist
        {
            get
            {
                if (_audioList != null)
                    return AudioList[_currentIndex].Artist;
                return string.Empty;
            }
        }
        public static string Title
        {
            get
            {
                if (_audioList != null)
                    return AudioList[_currentIndex].Title;
                return string.Empty;
            }
        }

        private static Uri Url => AudioList[_currentIndex].Url;

        public static double Duration
        {
            get
            {
                if (_audioList != null)
                    return AudioList[_currentIndex].Duration;
                return 0.0;
            }
        }
        public static double Position
        {
            get
            {
                if (OutputDevice.PlaybackState == PlaybackState.Stopped)
                    return 0.0;

                return AudioStream.CurrentTime.TotalSeconds;
            }
        }
        public static double Progress
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

        public static PlaybackState PlayingState => OutputDevice.PlaybackState;

        public static float Volume { get { return AudioStream.Volume; } private set { AudioStream.Volume = value; } }

        #endregion Variables

        #region Execute

        public static void Execute(string command)
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

        private static void ManagePlay()
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

        private static void Play()
        {
            AudioDownloader?.Stop();

            if(File.Exists(Path.Combine(Measure.AudioCache, AudioList[_currentIndex].Id.Value.ToString())))
            {
                PlayFromCache();
                return;
            }

            AudioDownloader = new AudioDownloader(Url);
            AudioDownloader.PreDownloaded += Downloader_PreDownloaded;
            AudioDownloader.Downloaded += Downloader_Downloaded;

            AudioDownloader.DownloadAsync();
        }
        private static void PlayFromCache()
        {
            using (var fs = File.OpenRead(Path.Combine(Measure.AudioCache, AudioList[_currentIndex].Id.Value.ToString())))
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
        private static void Downloader_PreDownloaded(AudioDownloader downloader)
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
        private static void Downloader_Downloaded(AudioDownloader downloader)
        {
            if(SaveToFile)
            {
                using (var fs = File.Create(Path.Combine(Measure.AudioCache, AudioList[_currentIndex].Id.Value.ToString())))
                {
                    downloader.Stream.CopyTo(fs);
                }
            }
        }
        private static void Pause()
        {
            OutputDevice.Pause();
        }
        private static void Resume()
        {
            OutputDevice.Resume();
        }
        private static void Stop()
        {
            AudioDownloader?.Stop();
            OutputDevice.Stop();
        }

        // Still not working.
        private static void SetPosition(string text)
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

        private static void PlayPrevious()
        {
            if (_currentIndex <= 0)
                return;

            _currentIndex -= 1;

            Play();
        }
        private static void PlayNext()
        {
            if (_currentIndex >= AudioList.Count)
                return;

            _currentIndex += 1;

            Play();
        }

        private static void SetVolume(string text)
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
        private static void ChangeVolume(float value)
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

        private static void SetShuffle(string value)
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
        private static void SetRepeat(string value)
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
        private static void SetSaveToFile(string value)
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