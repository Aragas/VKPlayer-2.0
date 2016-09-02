/*
  Copyright (C) 2011 Birunthan Mohanathas

  This program is free software; you can redistribute it and/or
  modify it under the terms of the GNU General Public License
  as published by the Free Software Foundation; either version 2
  of the License, or (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;
using System.Diagnostics;

namespace Rainmeter
{
    public abstract class PluginSkin
    {
        internal Dictionary<IntPtr, PluginMeasure> PluginMeasureTypes = new Dictionary<IntPtr, PluginMeasure>();

        public string Path { get; }

        public PluginSkin(RainmeterAPI api)
        {
            Path = api.ReadPath("PluginType", "");
            Path = Path.Replace("\\" + Path.Split('\\')[7], "\\");
        }

        public abstract void Created();
        public abstract void Closed();
    }

    public abstract class PluginMeasure
    {
        public PluginSkin Skin { get; }
        public string Path => Skin.Path;

        public PluginMeasure(PluginSkin skin, RainmeterAPI api) { Skin = skin; }

        public abstract void Reload(RainmeterAPI api, ref double maxValue);
        public abstract double GetNumeric();
        public abstract string GetString();
        public abstract void ExecuteBang(string command);
        public abstract void Finalize();
    }
    public abstract class PluginMeasure<TEnum> : PluginMeasure where TEnum : struct, IConvertible
    {
        public TEnum TypeEnum { get; }

        public PluginMeasure(string MeasureType, PluginSkin skin, RainmeterAPI api) : base(skin, api)
        {
            if (!typeof(TEnum).IsEnum)
                throw new ArgumentException("TEnum must be an enumerated type");

            TEnum typeEnum = default(TEnum);
            if (!Enum.TryParse<TEnum>(MeasureType, true, out typeEnum))
            {
                RainmeterAPI.Log(RainmeterAPI.LogType.Error, $"{System.IO.Path.GetFileName(Assembly.GetExecutingAssembly().Location)} MeasureType={MeasureType} not valid.");
            }

            TypeEnum = typeEnum;
        }

        public override string ToString() => $"{GetType().Name.Replace("Measure", "")}[{TypeEnum.ToString()}]";
    }

    public class SkinHandler
    {
        static SkinHandler() { AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve; }
        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            foreach (var entry in SkinHandlers)
            {
                foreach(var entry2 in entry.Value.PluginSkins)
                {
                    var path = entry2.Value.Path + new AssemblyName(args.Name) + ".dll";
                    if(File.Exists(path))
                        return Assembly.LoadFrom(path);
                }
            }

            return null;
        }

        // -- Each SkinHandler is unique for each Rainmeter's Skin IntPtr.
        private static Dictionary<IntPtr, SkinHandler> SkinHandlers = new Dictionary<IntPtr, SkinHandler>();
        public static SkinHandler GetSkinHandlerBySkinPtr(IntPtr skinPtr)
        {
            SkinHandler skinHandler;
            if (!SkinHandlers.TryGetValue(skinPtr, out skinHandler))
            {
                SkinHandlers.Add(skinPtr, (skinHandler = new SkinHandler(skinPtr)));
            }

            return skinHandler;
        }
        public static SkinHandler GetSkinHandlerByMeasurePtr(IntPtr data)
        {
            foreach (var entry in SkinHandlers)
            {
                if (entry.Value.MeasureToSkinRef.ContainsKey(data))
                {
                    return entry.Value;
                }
            }

            return null;
        }
        // -- Each SkinHandler is unique for each Rainmeter's Skin IntPtr.

        // -- Creating instances of implemented classes by known scheme.
        private static Type GetPluginSkinType(string measureType) => Assembly.GetCallingAssembly().GetTypes().Single(assembly => assembly.Name.ToLowerInvariant() == $"{measureType}Skin".ToLowerInvariant());
        private static PluginSkin CreatePluginSkin(string measureType, RainmeterAPI api) => (PluginSkin) Activator.CreateInstance(GetPluginSkinType(measureType), new object[] { api });
        private static Type GetPluginMeasureType(string measureType) => Assembly.GetCallingAssembly().GetTypes().Single(assembly => assembly.Name.ToLowerInvariant() == $"{measureType}Measure".ToLowerInvariant());
        private static PluginMeasure CreatePluginMeasure(string measureType, string pluginType, PluginSkin skin, RainmeterAPI api) => (PluginMeasure) Activator.CreateInstance(GetPluginMeasureType(measureType), new object[] { pluginType, skin, api });
        // -- Creating instances of implemented classes by known scheme.

        // -- Fast references, without them we would need to make .Contains() call to each skin.
        private Dictionary<IntPtr, PluginSkin> MeasureToSkinRef = new Dictionary<IntPtr, PluginSkin>();
        private PluginMeasure GetPluginMeasureType(IntPtr ptr) => MeasureToSkinRef[ptr].PluginMeasureTypes[ptr];
        private void AddPluginMeasure(IntPtr ptr, Type type, PluginMeasure pluginMeasure)
        {
            MeasureToSkinRef.Add(ptr, PluginSkins[type]);
            MeasureToSkinRef[ptr].PluginMeasureTypes.Add(ptr, pluginMeasure);
        }
        private void RemovePluginMeasure(IntPtr ptr)
        {
            MeasureToSkinRef[ptr].PluginMeasureTypes.Remove(ptr);
            MeasureToSkinRef.Remove(ptr);
        }
        // -- Fast references, without them we would need to make .Contains() call to each skin.


        private IntPtr SkinPtr { get; set; }

        private Dictionary<Type, PluginSkin> PluginSkins = new Dictionary<Type, PluginSkin>();

        public SkinHandler(IntPtr skinPtr)
        {
            SkinPtr = skinPtr;
        }
        
        public IntPtr Initialize(RainmeterAPI api)
        {
            var measureType = api.ReadString("MeasureType", string.Empty);
            var pluginType = api.ReadString("PluginType", string.Empty);
            var pluginMeasureType = GetPluginMeasureType(measureType);

            if (!PluginSkins.ContainsKey(pluginMeasureType))
            {
                var skin = CreatePluginSkin(measureType, api);
                PluginSkins.Add(pluginMeasureType, skin);
                skin.Created();
            }

            var pluginMeasure = CreatePluginMeasure(measureType, pluginType, PluginSkins[pluginMeasureType], api);
            var ptr = GCHandle.ToIntPtr(GCHandle.Alloc(pluginMeasure));
            AddPluginMeasure(ptr, pluginMeasureType, pluginMeasure);
            return ptr;
        }

        public void Reload(IntPtr measurePtr, RainmeterAPI api, ref double maxValue) => GetPluginMeasureType(measurePtr).Reload(api, ref maxValue);

        public double GetNumeric(IntPtr measurePtr) => GetPluginMeasureType(measurePtr).GetNumeric();
        public string GetString(IntPtr measurePtr) => GetPluginMeasureType(measurePtr).GetString();

        public void ExecuteBang(IntPtr measurePtr, string args) => GetPluginMeasureType(measurePtr).ExecuteBang(args);

        public void Finalize(IntPtr measurePtr)
        {
            GetPluginMeasureType(measurePtr).Finalize();


            RemovePluginMeasure(measurePtr);

            List<Type> removeList = new List<Type>();
            foreach (var skin in PluginSkins)
            {
                if(skin.Value.PluginMeasureTypes.Count == 0)
                {
                    skin.Value.Closed();
                    removeList.Add(skin.Key);
                }
            }
            foreach (var type in removeList)
            {
                PluginSkins.Remove(type);
            }
        }
    }


    /// <summary>
    /// Wrapper around the Rainmeter C API.
    /// </summary>
    public class RainmeterAPI
    {
        private IntPtr m_Rm;

        public RainmeterAPI(IntPtr rm)
        {
            m_Rm = rm;
        }

        [DllImport("Rainmeter.dll", CharSet = CharSet.Unicode)]
        private extern static IntPtr RmReadString(IntPtr rm, string option, string defValue, bool replaceMeasures);

        [DllImport("Rainmeter.dll", CharSet = CharSet.Unicode)]
        private extern static double RmReadFormula(IntPtr rm, string option, double defValue);

        [DllImport("Rainmeter.dll", CharSet = CharSet.Unicode)]
        private extern static IntPtr RmReplaceVariables(IntPtr rm, string str);

        [DllImport("Rainmeter.dll", CharSet = CharSet.Unicode)]
        private extern static IntPtr RmPathToAbsolute(IntPtr rm, string relativePath);

        [DllImport("Rainmeter.dll", EntryPoint = "RmExecute", CharSet = CharSet.Unicode)]
        public extern static void Execute(IntPtr skin, string command);

        [DllImport("Rainmeter.dll")]
        private extern static IntPtr RmGet(IntPtr rm, RmGetType type);

        [DllImport("Rainmeter.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private extern static int LSLog(LogType type, string unused, string message);

        private enum RmGetType
        {
            MeasureName = 0,
            Skin = 1,
            SettingsFile = 2,
            SkinName = 3,
            SkinWindowHandle = 4
        }

        public enum LogType
        {
            Error = 1,
            Warning = 2,
            Notice = 3,
            Debug = 4
        }

        public string ReadString(string option, string defValue, bool replaceMeasures = true)
        {
            return Marshal.PtrToStringUni(RmReadString(m_Rm, option, defValue, replaceMeasures));
        }

        public string ReadPath(string option, string defValue)
        {
            return Marshal.PtrToStringUni(RmPathToAbsolute(m_Rm, ReadString(option, defValue)));
        }

        public double ReadDouble(string option, double defValue)
        {
            return RmReadFormula(m_Rm, option, defValue);
        }

        public int ReadInt(string option, int defValue)
        {
            return (int)RmReadFormula(m_Rm, option, defValue);
        }

        public string ReplaceVariables(string str)
        {
            return Marshal.PtrToStringUni(RmReplaceVariables(m_Rm, str));
        }

        public string GetMeasureName()
        {
            return Marshal.PtrToStringUni(RmGet(m_Rm, RmGetType.MeasureName));
        }

        public IntPtr GetSkin()
        {
            return RmGet(m_Rm, RmGetType.Skin);
        }

        public string GetSettingsFile()
        {
            return Marshal.PtrToStringUni(RmGet(m_Rm, RmGetType.SettingsFile));
        }

        public string GetSkinName()
        {
            return Marshal.PtrToStringUni(RmGet(m_Rm, RmGetType.SkinName));
        }

        public IntPtr GetSkinWindow()
        {
            return RmGet(m_Rm, RmGetType.SkinWindowHandle);
        }

        public static void Log(LogType type, string message)
        {
            LSLog(type, null, message);
        }
    }

    public static class PluginAPI
    {
        private static IntPtr StringBuffer = IntPtr.Zero;

        [DllExport]
        public static void Initialize(ref IntPtr measurePtr, IntPtr apiPtr)
        {
            RainmeterAPI api = new RainmeterAPI(apiPtr);
            SkinHandler skinHandler = SkinHandler.GetSkinHandlerBySkinPtr(api.GetSkin());
            measurePtr = skinHandler.Initialize(api);
        }

        [DllExport]
        public static void Reload(IntPtr measurePtr, IntPtr apiPtr, ref double maxValue)
        {
            RainmeterAPI api = new RainmeterAPI(apiPtr);
            SkinHandler skinHandler = SkinHandler.GetSkinHandlerBySkinPtr(api.GetSkin());
            skinHandler.Reload(measurePtr, api, ref maxValue);
        }

        [DllExport]
        public static double Update(IntPtr measurePtr)
        {
            SkinHandler skinHandler = SkinHandler.GetSkinHandlerByMeasurePtr(measurePtr);
            return skinHandler.GetNumeric(measurePtr);
        }

        [DllExport]
        public static IntPtr GetString(IntPtr measurePtr)
        {
            SkinHandler skinHandler = SkinHandler.GetSkinHandlerByMeasurePtr(measurePtr);
            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }

            string stringValue = skinHandler.GetString(measurePtr);
            if (stringValue != null)
                StringBuffer = Marshal.StringToHGlobalUni(stringValue);

            return StringBuffer;
        }

        [DllExport]
        public static void ExecuteBang(IntPtr measurePtr, IntPtr argsPtr)
        {
            SkinHandler skinHandler = SkinHandler.GetSkinHandlerByMeasurePtr(measurePtr);
            skinHandler.ExecuteBang(measurePtr, Marshal.PtrToStringUni(argsPtr));
        }

        [DllExport]
        public static void Finalize(IntPtr measurePtr)
        {
            SkinHandler skinHandler = SkinHandler.GetSkinHandlerByMeasurePtr(measurePtr);
            skinHandler.Finalize(measurePtr);

            GCHandle.FromIntPtr(measurePtr).Free();

            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }
        }
    }

    /// <summary>
    /// Dummy attribute to mark method as exported for DllExporter.exe.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class DllExport : Attribute
    {
        public DllExport() { }
    }
}
