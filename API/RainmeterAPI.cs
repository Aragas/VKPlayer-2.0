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

        internal PluginSkin() { }
        public PluginSkin(RainmeterAPI api)
        {
            Path = api.ReadPath(PluginMeasure.PluginMeasureName, "");
            Path = Path.Replace("\\" + Path.Split('\\')[7], "\\");
        }

        public abstract void Created();
        public abstract void Closed();
    }

    /// <summary>
    /// A Measure that will return any value requested by Rainmeter. Can receive command via ExecuteBang(COMMAND).
    /// </summary>
    public abstract class PluginMeasure
    {
        /// <summary>
        /// Specifies which Measure should be used.
        /// </summary>
        public const string PluginMeasureName = "PluginMeasureName";
        /// <summary>
        /// Specifies which type in a Measure should be used.
        /// </summary>
        public const string PluginMeasureType = "PluginMeasureType";


        public PluginSkin Skin { get; }
        public string Path => Skin.Path;

        internal PluginMeasure() { }
        public PluginMeasure(PluginSkin skin, RainmeterAPI api) { Skin = skin; }

        public abstract void Reload(RainmeterAPI api, ref double maxValue);
        public abstract double GetNumeric();
        public abstract string GetString();
        public abstract void ExecuteBang(string command);
        public abstract void Finalize();
    }
    /// <summary>
    /// A Measure that will return any value requested by Rainmeter. Can receive command via ExecuteBang(COMMAND).
    /// </summary>
    /// <typeparam name="TEnum">An Enumerable that will specify which Measure type to use</typeparam>
    public abstract class PluginMeasure<TEnum> : PluginMeasure where TEnum : struct, IConvertible
    {
        public TEnum TypeEnum { get; }

        public PluginMeasure(string measureType, PluginSkin skin, RainmeterAPI api) : base(skin, api)
        {
            if (!typeof(TEnum).IsEnum)
                throw new ArgumentException("TEnum must be an enumerated type");

            TEnum typeEnum = default(TEnum);
            if (!Enum.TryParse<TEnum>(measureType, true, out typeEnum))
                RainmeterAPI.Log(RainmeterAPI.LogType.Error, $"{System.IO.Path.GetFileName(Assembly.GetExecutingAssembly().Location)} {PluginMeasure.PluginMeasureType}={measureType} not valid.");
            TypeEnum = typeEnum;
        }

        public override string ToString() => $"{GetType().Name.Replace("Measure", "")}[{TypeEnum.ToString()}]";
    }

    /// <summary>
    /// Each instance of this class represents one Rainmeter Skin. This feature allow us to use one plugin for multiple skins, without a static Measure handler. See examples.
    /// </summary>
    internal partial class RainmeterSkinHandler : IDisposable
    {
        /// <summary>
        /// Used when received an IntPtr.Zero Measure pointer.
        /// </summary>
        private static RainmeterSkinHandler Empty { get; } = new RainmeterSkinHandler(IntPtr.Zero);

        #region Assembly Dependency Resolver. Store any required by plugin .dll in %MY_DOCUMENTS%\Rainmeter\Skins\%SKIN%.
        static RainmeterSkinHandler() { AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve; }
        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // -- Basically, will try find any needed .dll in every Skin that is using the plugin.
            foreach (var skinHandlers in SkinHandlerBySkinPtr.Values)
                foreach (var pluginSkin in skinHandlers.PluginSkins.Values)
                {
                    var path = string.Format("{0}{1}.dll", pluginSkin.Path, new AssemblyName(args.Name).Name);
                    if (File.Exists(path))
                        return Assembly.LoadFrom(path);
                }

            return null;
        }
        #endregion Assembly Dependency Resolver. Store any required .dll in %MY_DOCUMENTS%\Rainmeter\Skins\%SKIN%.

        #region Fast SkinPtr->SkinHandler Reference.
        private static Dictionary<IntPtr, RainmeterSkinHandler> SkinHandlerBySkinPtr = new Dictionary<IntPtr, RainmeterSkinHandler>();
        internal static RainmeterSkinHandler GetSkinHandlerBySkinPtr(IntPtr skinPtr)
        {
            RainmeterSkinHandler skinHandler;
            if (!SkinHandlerBySkinPtr.TryGetValue(skinPtr, out skinHandler))
                SkinHandlerBySkinPtr.Add(skinPtr, (skinHandler = new RainmeterSkinHandler(skinPtr)));
            return skinHandler;
        }
        #endregion Fast SkinPtr->SkinHandler Reference.

        #region Fast MeasurePtr->SkinHandler Reference.
        private static Dictionary<IntPtr, RainmeterSkinHandler> SkinHandlerByMeasurePtr = new Dictionary<IntPtr, RainmeterSkinHandler>();
        internal static RainmeterSkinHandler GetSkinHandlerByMeasurePtr(IntPtr measurePtr)
        {
            if (measurePtr == IntPtr.Zero)
                return Empty;

            return SkinHandlerByMeasurePtr[measurePtr];
        }
        internal static void AddMeasurePtr(IntPtr measurePtr, RainmeterSkinHandler skinHandler)
        {
            if (measurePtr == IntPtr.Zero)
                return;

            SkinHandlerByMeasurePtr.Add(measurePtr, skinHandler);
        }
        internal static void RemoveMeasurePtr(IntPtr measurePtr)
        {
            if (measurePtr == IntPtr.Zero)
                return;

            SkinHandlerByMeasurePtr.Remove(measurePtr);
        }
        #endregion Fast MeasurePtr->SkinHandler Reference.

        #region Creating Instances of Implemented Classes by Known Scheme.
        private static Type GetPluginSkinType(string measureType) =>
            Assembly.GetCallingAssembly().GetTypes().SingleOrDefault(assembly => assembly.Name.ToLowerInvariant() == $"{measureType}Skin".ToLowerInvariant());

        private static PluginSkin CreatePluginSkin(string measureType, RainmeterAPI api) =>
            (PluginSkin) Activator.CreateInstance(GetPluginSkinType(measureType), new object[] { api });

        private static Type GetPluginMeasureType(string measureType) =>
            Assembly.GetCallingAssembly().GetTypes().SingleOrDefault(assembly => assembly.Name.ToLowerInvariant() == $"{measureType}Measure".ToLowerInvariant());

        private static PluginMeasure CreatePluginMeasure(string measureType, string pluginType, PluginSkin skin, RainmeterAPI api) =>
            (PluginMeasure) Activator.CreateInstance(GetPluginMeasureType(measureType), new object[] { pluginType, skin, api });
        #endregion Creating Instances of Implemented Classes by Known Scheme.


        #region Fast MeasurePtr->PluginSkin Reference.
        private Dictionary<IntPtr, PluginSkin> PluginSkinByMeasurePtr = new Dictionary<IntPtr, PluginSkin>();
        private PluginMeasure GetPluginMeasureType(IntPtr ptr) => PluginSkinByMeasurePtr[ptr].PluginMeasureTypes[ptr];
        private void AddPluginMeasure(IntPtr ptr, PluginSkin skin, PluginMeasure pluginMeasure)
        {
            PluginSkinByMeasurePtr.Add(ptr, skin);
            PluginSkinByMeasurePtr[ptr].PluginMeasureTypes.Add(ptr, pluginMeasure);
        }
        private void RemovePluginMeasure(IntPtr ptr)
        {
            PluginSkinByMeasurePtr[ptr].PluginMeasureTypes.Remove(ptr);
            PluginSkinByMeasurePtr.Remove(ptr);
        }
        #endregion Fast MeasurePtr->PluginSkin Reference.

        private IntPtr SkinPtr { get; set; }

        private Dictionary<Type, PluginSkin> PluginSkins = new Dictionary<Type, PluginSkin>();

        internal RainmeterSkinHandler(IntPtr skinPtr) { SkinPtr = skinPtr; }
        
        internal void   M_Initialize(ref IntPtr measurePtr, RainmeterAPI api)
        {
            var measureName = api.ReadString(PluginMeasure.PluginMeasureName, string.Empty);
            var measureType = api.ReadString(PluginMeasure.PluginMeasureType, string.Empty);
            var measureTypeType = GetPluginMeasureType(measureName);
            if (measureTypeType == null) // -- Error checking.
            {
                if(string.IsNullOrEmpty(measureName))
                    RainmeterAPI.Log(RainmeterAPI.LogType.Error, $"{PluginMeasure.PluginMeasureName}= Not found.");

                if (string.IsNullOrEmpty(measureType))
                    RainmeterAPI.Log(RainmeterAPI.LogType.Error, $"{PluginMeasure.PluginMeasureType}= Not found.");

                if(!string.IsNullOrEmpty(measureName) && !string.IsNullOrEmpty(measureType))
                    RainmeterAPI.Log(RainmeterAPI.LogType.Error, $"Missing .dll's");

                // Not sure how bad this is on rainmeter side, but it seems fine. That's the only safe way to handle errors.
                // Pointer will be reset anyway by refreshin theg skin with fixed errors.
                measurePtr = IntPtr.Zero; 
                return;
            }

            // -- Check if a PluginSkin exist for this type of MeasureType. Create and add if not.
            if (!PluginSkins.ContainsKey(measureTypeType))
            {
                var skin = CreatePluginSkin(measureName, api);
                PluginSkins.Add(measureTypeType, skin);
                skin.Created();
            }

            var pluginMeasure = CreatePluginMeasure(measureName, measureType, PluginSkins[measureTypeType], api);
            measurePtr = GCHandle.ToIntPtr(GCHandle.Alloc(pluginMeasure));
            AddPluginMeasure(measurePtr, PluginSkins[measureTypeType], pluginMeasure);
        }
        internal void   M_Reload(IntPtr measurePtr, RainmeterAPI api, ref double maxValue)
        {
            if (measurePtr == IntPtr.Zero)
                return;

            GetPluginMeasureType(measurePtr).Reload(api, ref maxValue);
        }
        internal double M_GetNumeric(IntPtr measurePtr)
        {
            if (measurePtr == IntPtr.Zero)
                return 0.0;

            return GetPluginMeasureType(measurePtr).GetNumeric();
        }
        internal string M_GetString(IntPtr measurePtr)
        {
            if (measurePtr == IntPtr.Zero)
                return string.Empty;

            return GetPluginMeasureType(measurePtr).GetString();
        }
        internal void   M_ExecuteBang(IntPtr measurePtr, string args)
        {
            if (measurePtr == IntPtr.Zero)
                return;

            GetPluginMeasureType(measurePtr).ExecuteBang(args);
        }
        internal void   M_Finalize(IntPtr measurePtr)
        {
            if (measurePtr == IntPtr.Zero)
                return;

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
                PluginSkins.Remove(type);

            if (PluginSkins.Count == 0)
                Dispose();
        }

        public void Dispose()
        {
            SkinHandlerBySkinPtr.Remove(SkinPtr);
        }


        public override string ToString() => SkinPtr.ToString();
    }


    /// <summary>
    /// Wrapper around the Rainmeter C API.
    /// </summary>
    public class RainmeterAPI
    {
        private IntPtr m_Rm;

        public RainmeterAPI(IntPtr rm) { m_Rm = rm; }

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

        public string ReadString(string option, string defValue, bool replaceMeasures = true) => Marshal.PtrToStringUni(RmReadString(m_Rm, option, defValue, replaceMeasures));

        public string ReadPath(string option, string defValue) => Marshal.PtrToStringUni(RmPathToAbsolute(m_Rm, ReadString(option, defValue)));

        public double ReadDouble(string option, double defValue) => RmReadFormula(m_Rm, option, defValue);

        public int ReadInt(string option, int defValue) => (int) RmReadFormula(m_Rm, option, defValue);

        public string ReplaceVariables(string str) => Marshal.PtrToStringUni(RmReplaceVariables(m_Rm, str));

        public string GetMeasureName() => Marshal.PtrToStringUni(RmGet(m_Rm, RmGetType.MeasureName));

        public IntPtr GetSkin() => RmGet(m_Rm, RmGetType.Skin);

        public string GetSettingsFile() => Marshal.PtrToStringUni(RmGet(m_Rm, RmGetType.SettingsFile));

        public string GetSkinName() => Marshal.PtrToStringUni(RmGet(m_Rm, RmGetType.SkinName));

        public IntPtr GetSkinWindow() => RmGet(m_Rm, RmGetType.SkinWindowHandle);

        public static void Log(LogType type, string message) => LSLog(type, null, message);
    }

    /// <summary>
    /// Wrapper around the Rainmeter Plugin C API.
    /// </summary>
    public static class PluginAPI
    {
        private static IntPtr StringBuffer = IntPtr.Zero;

        [DllExport]
        public static void Initialize(ref IntPtr measurePtr, IntPtr apiPtr)
        {
            RainmeterAPI api = new RainmeterAPI(apiPtr);
            RainmeterSkinHandler skinHandler = RainmeterSkinHandler.GetSkinHandlerBySkinPtr(api.GetSkin());
            skinHandler.M_Initialize(ref measurePtr, api);
            RainmeterSkinHandler.AddMeasurePtr(measurePtr, skinHandler);
        }

        [DllExport]
        public static void Reload(IntPtr measurePtr, IntPtr apiPtr, ref double maxValue)
        {
            RainmeterSkinHandler skinHandler = RainmeterSkinHandler.GetSkinHandlerByMeasurePtr(measurePtr);
            skinHandler.M_Reload(measurePtr, new RainmeterAPI(apiPtr), ref maxValue);
        }

        [DllExport]
        public static double Update(IntPtr measurePtr)
        {
            RainmeterSkinHandler skinHandler = RainmeterSkinHandler.GetSkinHandlerByMeasurePtr(measurePtr);
            return skinHandler.M_GetNumeric(measurePtr);
        }

        [DllExport]
        public static IntPtr GetString(IntPtr measurePtr)
        {
            RainmeterSkinHandler skinHandler = RainmeterSkinHandler.GetSkinHandlerByMeasurePtr(measurePtr);
            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }

            string stringValue = skinHandler.M_GetString(measurePtr);
            if (stringValue != null)
                StringBuffer = Marshal.StringToHGlobalUni(stringValue);

            return StringBuffer;
        }

        [DllExport]
        public static void ExecuteBang(IntPtr measurePtr, IntPtr argsPtr)
        {
            RainmeterSkinHandler skinHandler = RainmeterSkinHandler.GetSkinHandlerByMeasurePtr(measurePtr);
            skinHandler.M_ExecuteBang(measurePtr, Marshal.PtrToStringUni(argsPtr));
        }

        [DllExport]
        public static void Finalize(IntPtr measurePtr)
        {
            RainmeterSkinHandler skinHandler = RainmeterSkinHandler.GetSkinHandlerByMeasurePtr(measurePtr);
            skinHandler.M_Finalize(measurePtr);

            if(measurePtr != IntPtr.Zero)
                GCHandle.FromIntPtr(measurePtr).Free();

            RainmeterSkinHandler.RemoveMeasurePtr(measurePtr);

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
