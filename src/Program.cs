using System;
using System.Threading;
using System.Windows.Forms;

namespace DropOrb
{
    internal static class Program
    {
        public static bool InspectMode { get; private set; }
        public static string InspectDropPath { get; private set; }
        public static bool InspectHelp { get; private set; }
        public static bool InspectActivity { get; private set; }
        public static string InspectRadialPath { get; private set; }

        [STAThread]
        private static void Main(string[] args)
        {
            args = args ?? new string[0];
            InspectMode = Array.Exists(args, value => string.Equals(value, "--inspect", StringComparison.OrdinalIgnoreCase));
            InspectHelp = Array.Exists(args, value => string.Equals(value, "--inspect-help", StringComparison.OrdinalIgnoreCase));
            InspectActivity = Array.Exists(args, value => string.Equals(value, "--inspect-activity", StringComparison.OrdinalIgnoreCase));
            if (InspectHelp || InspectActivity) InspectMode = true;
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
            bool created;
            using (var mutex = new Mutex(true, "Local\\DropOrb.Desktop.Mvp", out created))
            {
                if (!created) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new DropOrbForm());
            }
        }
    }
}
