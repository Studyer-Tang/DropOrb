using System;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;

namespace DropOrb
{
    internal static class Program
    {
        public static bool InspectMode { get; private set; }
        public static string InspectDropPath { get; private set; }
        public static bool InspectHelp { get; private set; }
        public static bool InspectActivity { get; private set; }
        public static string InspectRadialPath { get; private set; }
        public static bool InspectCommand { get; private set; }
        public static string[] InitialPaths { get; private set; }

        [STAThread]
        private static void Main(string[] args)
        {
            args = args ?? new string[0];
            InspectMode = Array.Exists(args, value => string.Equals(value, "--inspect", StringComparison.OrdinalIgnoreCase));
            InspectHelp = Array.Exists(args, value => string.Equals(value, "--inspect-help", StringComparison.OrdinalIgnoreCase));
            InspectActivity = Array.Exists(args, value => string.Equals(value, "--inspect-activity", StringComparison.OrdinalIgnoreCase));
            InspectCommand = Array.Exists(args, value => string.Equals(value, "--inspect-command", StringComparison.OrdinalIgnoreCase));
            if (InspectHelp || InspectActivity || InspectCommand) InspectMode = true;
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (!string.Equals(args[index], "--inspect-drop", StringComparison.OrdinalIgnoreCase)) continue;
                InspectMode = true;
                InspectDropPath = args[index + 1];
                break;
            }
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (!string.Equals(args[index], "--inspect-radial", StringComparison.OrdinalIgnoreCase)) continue;
                InspectMode = true;
                InspectRadialPath = args[index + 1];
                break;
            }
            var paths = new List<string>();
            for (var index = 0; index < args.Length; index++)
            {
                var argument = args[index];
                if (string.Equals(argument, "--inspect-drop", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(argument, "--inspect-radial", StringComparison.OrdinalIgnoreCase)) { index++; continue; }
                if (!argument.StartsWith("--", StringComparison.Ordinal) && (File.Exists(argument) || Directory.Exists(argument)))
                    paths.Add(Path.GetFullPath(argument));
            }
            InitialPaths = paths.ToArray();
            bool created;
            using (var mutex = new Mutex(true, "Local\\DropOrb.Desktop.Mvp", out created))
            {
                if (!created)
                {
                    if (!SingleInstanceBridge.Forward(InitialPaths))
                        MessageBox.Show("已有 DropOrb 正在运行，但无法把内容转交给它。请从托盘退出后重试。",
                            "DropOrb 转交失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var form = new DropOrbForm();
                using (var bridge = new SingleInstanceBridge())
                {
                    bridge.ArgumentsReceived += form.ProcessExternalArguments;
                    bridge.Start();
                    Application.Run(form);
                }
            }
        }
    }
}
