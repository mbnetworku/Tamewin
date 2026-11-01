using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Tamewin
{
    static class Program
    {
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int processId);

        const string Help =
@"Tamewin - Windows privacy settings, tweaks and app remover, with undo.

Run with no arguments to open the window.

Command line:
  Tamewin.exe --list                  show every option, its id and status
  Tamewin.exe --apply  <ids|recommended>   apply options (comma-separated ids)
  Tamewin.exe --revert <ids|all>      put options back the way they were
  Tamewin.exe --apps                  scan apps: installed / not installed
  Tamewin.exe --startmenu [glossy|glass]   open the Windows 7-style launcher

Testing options:
  --test-root <key>   redirect every registry change to HKCU\<key> (nothing real is touched)
  --backup <file>     use this backup file instead of %LOCALAPPDATA%\Tamewin\backup.tsv
  --theme light|dark  force a theme instead of following Windows
  --screenshot <file.png> <privacy|features|apps|log>   save a picture of a page and exit";

        [STAThread]
        static int Main(string[] args)
        {
            string testRoot = null, backup = null, command = null, arg = null, theme = null, shotPage = null;
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i].ToLowerInvariant();
                string Next() => i + 1 < args.Length ? args[++i] : null;
                if (a == "--test-root") testRoot = Next();
                else if (a == "--backup") backup = Next();
                else if (a == "--theme") theme = Next();
                else if (a == "--apply" || a == "--revert") { command = a; arg = Next(); }
                else if (a == "--screenshot") { command = a; arg = Next(); shotPage = Next(); }
                else if (a == "--startmenu") { command = a; arg = Next(); shotPage = Next(); }
                else command = a;
            }
            var engine = new Engine(testRoot, backup);
            Theme.Init(theme == "dark" ? true : theme == "light" ? (bool?)false : null);

            if (command == null || command == "--screenshot" || command == "--startmenu")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                if (command == "--screenshot") return Screenshot(engine, arg, shotPage ?? "privacy");
                if (command == "--startmenu")
                {
                    var s = StartSettings.Load();
                    if (arg == "glossy") s.Style = StartStyle.Glossy;
                    else if (arg == "glass" || arg == "liquidglass") s.Style = StartStyle.LiquidGlass;
                    Application.Run(new StartMenuForm(s) { KeepOpen = shotPage == "pin" });
                    return 0;
                }
                Application.Run(new MainForm(engine));
                return 0;
            }

            if (!Console.IsOutputRedirected) AttachConsole(-1);
            Console.WriteLine();
            try { return RunCommand(engine, command, arg); }
            catch (Exception ex) { Console.WriteLine("ERROR: " + ex.Message); return 2; }
        }

        static int RunCommand(Engine engine, string command, string arg)
        {
            switch (command)
            {
                case "--list":
                    foreach (var t in Catalog.All)
                        Console.WriteLine("{0,-24} {1,-12} {2,-6} {3}", t.Id, engine.State(t), t.Admin ? "admin" : "", t.Title);
                    return 0;

                case "--apply":
                case "--revert":
                    var apply = command == "--apply";
                    var tweaks = Pick(arg, apply);
                    if (tweaks == null) return 2;
                    bool ok = true;
                    foreach (var t in tweaks)
                        ok &= apply ? engine.Apply(t, Console.WriteLine) : engine.Revert(t, Console.WriteLine);
                    return ok ? 0 : 1;

                case "--apps":
                    foreach (var r in AppScanner.Scan().OrderBy(r => r.Level).ThenBy(r => !r.Installed).ThenBy(r => r.Name))
                        Console.WriteLine("{0,-9} {1,-14} {2}", r.Level, r.Installed ? "Installed" : "Not installed", r.Name);
                    return 0;

                default:
                    Console.WriteLine(Help);
                    return command == "--help" || command == "-h" || command == "/?" ? 0 : 2;
            }
        }

        /// <summary>Draws a page off-screen and saves it as a PNG (for docs and design checks).</summary>
        static int Screenshot(Engine engine, string file, string page)
        {
            using (var form = new MainForm(engine, showTestLabel: false) { StartPosition = FormStartPosition.Manual, ShowInTaskbar = false })
            {
                form.Location = new System.Drawing.Point(-30000, -30000);
                form.Show();
                form.ShowPage(page);
                Application.DoEvents();
                using (var bmp = new System.Drawing.Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                    bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
                }
                form.Close();
            }
            return 0;
        }

        static Tweak[] Pick(string arg, bool apply)
        {
            if (string.IsNullOrEmpty(arg)) { Console.WriteLine("Give option ids, e.g. --apply ad_id,tips"); return null; }
            if (apply && arg == "recommended") return Catalog.All.Where(t => !t.OptIn).ToArray();
            if (!apply && arg == "all") return Catalog.All.ToArray();
            var ids = arg.Split(',').Select(s => s.Trim()).ToArray();
            var unknown = ids.Where(id => Catalog.Find(id) == null).ToArray();
            if (unknown.Length > 0) { Console.WriteLine("Unknown option id(s): " + string.Join(", ", unknown) + ". Use --list."); return null; }
            return ids.Select(Catalog.Find).ToArray();
        }
    }
}
