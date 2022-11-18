using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using LogLevel = BepInEx.Logging.LogLevel;

namespace TheOtherRoles
{
    class Logger
    {
        public static bool isDetail = false;
        public static bool isAlsoInGame = false;
        public static void SendInGame(string text)
        {
            if (FastDestroyableSingleton<HudManager>.Instance) FastDestroyableSingleton<HudManager>.Instance.Notifier.AddItem(text);
        }
        private static void SendToFile(string text, LogLevel level = LogLevel.Info, string tag = "", int lineNumber = 0)
        {
            string t = DateTime.Now.ToString("HH:mm:ss");
            string log_text = $"[{t}][{tag}]{text}";
            if (isDetail && TheOtherRolesPlugin.DebugMode.Value)
            {
                StackFrame stack = new(2);
                string class_name = stack.GetMethod().ReflectedType.Name;
                string method_name = stack.GetMethod().Name;
                log_text = $"[{t}][{class_name}.{method_name}({lineNumber})][{tag}]{text}";
            }
            TheOtherRolesPlugin.Logger.Log(level, log_text);
            if (isAlsoInGame) SendInGame(text);
        }
        public static void info(string text, string tag = "", [CallerLineNumber] int lineNumber = 0) => SendToFile(text, LogLevel.Info, tag, lineNumber);
        public static void warn(string text, string tag = "", [CallerLineNumber] int lineNumber = 0) => SendToFile(text, LogLevel.Warning, tag, lineNumber);
        public static void error(string text, string tag = "", [CallerLineNumber] int lineNumber = 0) => SendToFile(text, LogLevel.Error, tag, lineNumber);
        public static void fatal(string text, string tag = "", [CallerLineNumber] int lineNumber = 0) => SendToFile(text, LogLevel.Fatal, tag, lineNumber);
        public static void msg(string text, string tag = "", [CallerLineNumber] int lineNumber = 0) => SendToFile(text, LogLevel.Message, tag, lineNumber);
        public static void currentMethod([CallerLineNumber] int lineNumber = 0, [CallerFilePath] string filePath = "")
        {
            StackFrame stack = new(1);
            Logger.msg($"\"{stack.GetMethod().ReflectedType.Name}.{stack.GetMethod().Name}\" Called in \"{Path.GetFileName(filePath)}({lineNumber})\"", "Method");
        }
    }
}
