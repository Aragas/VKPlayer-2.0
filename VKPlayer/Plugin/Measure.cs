using Rainmeter;
using Rainmeter.AudioPlayer;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace VKPlayer.Plugin
{
    internal class Measure
    {
        internal static string AudioCache
        {
            get
            {
                if (!Directory.Exists(System.IO.Path.Combine(Path, "AudioCache")))
                    Directory.CreateDirectory(System.IO.Path.Combine(Path, "AudioCache"));
                return System.IO.Path.Combine(Path, "AudioCache");
            }
        }
        static string Path;
        static Measure()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, args) =>
            {
                if (args.Name.ToLowerInvariant().Contains("NAudio".ToLowerInvariant()))
                    return Assembly.LoadFrom(Path + "NAudio.dll");

                else if (args.Name.ToLowerInvariant().Contains("HtmlAgilityPack".ToLowerInvariant()))
                    return Assembly.LoadFrom(Path + "HtmlAgilityPack.dll");

                else if (args.Name.ToLowerInvariant().Contains("Newtonsoft.Json".ToLowerInvariant()))
                    return Assembly.LoadFrom(Path + "Newtonsoft.Json.dll");

                else if (args.Name.ToLowerInvariant().Contains("VkNet".ToLowerInvariant()))
                    return Assembly.LoadFrom(Path + "VkNet.dll");

                return null;
            };
        }

        private enum PlayerType
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
        private PlayerType _type;

        internal void Reload(Rainmeter.API rm, ref double maxValue)
        {
            if (string.IsNullOrEmpty(Path))
            {
                Path = rm.ReadPath("PlayerType", "");
                Path = Path.Replace("\\" + Path.Split('\\')[7], "\\");
            }
            
            string type = rm.ReadString("PlayerType", "");
            switch (type.ToLowerInvariant())
            {
                case "credits":
                    _type = PlayerType.Credits;
                    break;

                case "state":
                    _type = PlayerType.State;
                    break;

                case "artist":
                    _type = PlayerType.Artist;
                    break;

                case "title":
                    _type = PlayerType.Title;
                    break;

                case "duration":
                    _type = PlayerType.Duration;
                    break;

                case "position":
                    _type = PlayerType.Position;
                    break;

                case "repeat":
                    _type = PlayerType.Repeat;
                    break;

                case "shuffle":
                    _type = PlayerType.Shuffle;
                    break;

                case "volume":
                    _type = PlayerType.Volume;
                    break;

                case "progress":
                    _type = PlayerType.Progress;
                    break;

                case "savetofile":
                    _type = PlayerType.SaveToFile;
                    break;

                default:
                    Rainmeter.API.Log(Rainmeter.API.LogType.Error, $"VKPlayer.dll PlayerType={type} not valid.");
                    break;
            }
        }

        internal double Update()
        {
            switch (_type)
            {
                case PlayerType.Duration:
                    return Player.Duration;

                case PlayerType.Position:
                    return Math.Round(Player.Position);

                case PlayerType.State:
                    return (int) Player.PlayingState;

                case PlayerType.Repeat:
                    return Player.Repeat ? 0.0 : 1.0;

                case PlayerType.Shuffle:
                    return Player.Shuffle ? 0.0 : 1.0;

                case PlayerType.SaveToFile:
                    return Player.SaveToFile ? 0.0 : 1.0;

                case PlayerType.Progress:
                    return Player.Progress;

                default:
                    return 0.0;
            }
        }

        internal string GetString()
        {
            switch (_type)
            {
                case PlayerType.Credits:
                    return "VKPlayer by Aragas (Aragasas)";

                case PlayerType.Artist:
                    return Player.Artist ?? "Not Initialized";

                case PlayerType.Title:
                    return Player.Title ?? "Click Play";

                default:
                    return null;
            }
        }

        internal void ExecuteBang(string command)
        {
            MeasureHandler.Start(command);
        }
    }

    public static class Plugin
    {
        static IntPtr StringBuffer = IntPtr.Zero;

        [DllExport]
        public static void Initialize(ref IntPtr data, IntPtr rm)
        {
            data = GCHandle.ToIntPtr(GCHandle.Alloc(new Measure()));
        }

        [DllExport]
        public static void Finalize(IntPtr data)
        {
            GCHandle.FromIntPtr(data).Free();

            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }
        }

        [DllExport]
        public static void Reload(IntPtr data, IntPtr rm, ref double maxValue)
        {
            Measure measure = (Measure) GCHandle.FromIntPtr(data).Target;
            measure.Reload(new Rainmeter.API(rm), ref maxValue);
        }

        [DllExport]
        public static double Update(IntPtr data)
        {
            Measure measure = (Measure) GCHandle.FromIntPtr(data).Target;
            return measure.Update();
        }

        [DllExport]
        public static IntPtr GetString(IntPtr data)
        {
            Measure measure = (Measure) GCHandle.FromIntPtr(data).Target;
            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }

            string stringValue = measure.GetString();
            if (stringValue != null)
            {
                StringBuffer = Marshal.StringToHGlobalUni(stringValue);
            }

            return StringBuffer;
        }

        [DllExport]
        public static void ExecuteBang(IntPtr data, IntPtr args)
        {
            Measure measure = (Measure) GCHandle.FromIntPtr(data).Target;
            measure.ExecuteBang(Marshal.PtrToStringUni(args));
        }
    }
}