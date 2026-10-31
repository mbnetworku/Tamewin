using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using Microsoft.Win32;

namespace Fastyourpc
{
    public enum TweakState { NotApplied, Partly, Applied }

    /// <summary>Applies and reverts tweaks. Before a tweak is first applied, the original value of each of its
    /// registry values is saved to the backup file, so revert can put back exactly what was there.</summary>
    public sealed class Engine
    {
        readonly string testRoot;   // when set, every key is redirected to HKCU\<testRoot>\CU|LM\... (for testing)
        public readonly string BackupPath;

        public Engine(string testRoot = null, string backupPath = null)
        {
            this.testRoot = testRoot;
            BackupPath = backupPath ?? Path.Combine(DataFolder, "backup.tsv");
        }

        public static string DataFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fastyourpc");

        public bool TestMode => testRoot != null;

        public static readonly bool IsAdmin =
            new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        public bool CanRun(Tweak t) => !t.Admin || IsAdmin || TestMode;

        // ---------- registry helpers ----------

        RegistryKey Root(Hive hive) =>
            RegistryKey.OpenBaseKey(TestMode || hive == Hive.CU ? RegistryHive.CurrentUser : RegistryHive.LocalMachine,
                                    RegistryView.Registry64);

        string MapKey(Hive hive, string key) => TestMode ? testRoot + "\\" + hive + "\\" + key : key;

        bool KeyExists(Hive hive, string key)
        {
            using (var root = Root(hive))
            using (var k = root.OpenSubKey(MapKey(hive, key)))
                return k != null;
        }

        /// <summary>Returns the value, or null when the value (or its key) doesn't exist.</summary>
        object Read(Hive hive, string key, string name, out RegistryValueKind kind)
        {
            kind = RegistryValueKind.Unknown;
            using (var root = Root(hive))
            using (var k = root.OpenSubKey(MapKey(hive, key)))
            {
                var v = k?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (v != null) kind = k.GetValueKind(name);
                return v;
            }
        }

        void Write(Hive hive, string key, string name, object value, RegistryValueKind kind)
        {
            using (var root = Root(hive))
            using (var k = root.CreateSubKey(MapKey(hive, key), true))
            {
                if (value == null) k.DeleteValue(name, false);
                else k.SetValue(name, value, kind);
            }
        }

        static RegistryValueKind KindOf(object value) => value is int ? RegistryValueKind.DWord : RegistryValueKind.String;

        bool Matches(RegOp op)
        {
            var v = Read(op.Hive, op.Key, op.Name, out _);
            if (op.Value == null) return v == null;
            if (v == null) return false;
            if (op.Value is int i) return v is int vi && vi == i;
            return v is string s && s == (string)op.Value;
        }

        // ---------- public API ----------

        public TweakState State(Tweak t)
        {
            if (t.Ops.All(Matches)) return TweakState.Applied;
            // "Delete this value" ops match whenever the value is absent, so only values it sets count as partly applied.
            return t.Ops.Any(op => op.Value != null && Matches(op)) ? TweakState.Partly : TweakState.NotApplied;
        }

        public bool HasBackup(Tweak t) => Backup.Load(BackupPath).Any(e => e.TweakId == t.Id);

        public bool Apply(Tweak t, Action<string> log)
        {
            if (!CanRun(t)) { log("SKIPPED   " + t.Title + "  (needs Administrator)"); return false; }
            try
            {
                foreach (var c in t.Conflicts.Select(Catalog.Find).Where(c => c != null && State(c) != TweakState.NotApplied))
                    Revert(c, log);

                var backup = Backup.Load(BackupPath);
                if (!backup.Any(e => e.TweakId == t.Id))
                {
                    backup.AddRange(t.Ops.Select(op => Snapshot(t.Id, op)));
                    Backup.Save(BackupPath, backup);
                }
                foreach (var op in t.Ops) Write(op.Hive, op.Key, op.Name, op.Value, KindOf(op.Value));
                log("APPLIED   " + t.Title);
                return true;
            }
            catch (Exception ex) { log("FAILED    " + t.Title + "  (" + ex.Message + ")"); return false; }
        }

        public bool Revert(Tweak t, Action<string> log)
        {
            if (!CanRun(t)) { log("SKIPPED   " + t.Title + "  (needs Administrator)"); return false; }
            try
            {
                var backup = Backup.Load(BackupPath);
                var entries = backup.Where(e => e.TweakId == t.Id).ToList();
                if (entries.Count > 0)
                {
                    foreach (var e in entries)
                        Write(e.Hive, e.Key, e.Name, e.Existed ? e.GetValue() : null, e.Kind);
                    foreach (var e in entries.Where(e => e.CreatedKey != ""))
                        DeleteEmptyKeys(e.Hive, e.Key, e.CreatedKey);
                    backup.RemoveAll(e => e.TweakId == t.Id);
                    Backup.Save(BackupPath, backup);
                    log("RESTORED  " + t.Title);
                    return true;
                }
                if (State(t) == TweakState.NotApplied) { log("UNCHANGED " + t.Title + "  (wasn't applied)"); return true; }

                // Applied by something else (no backup): go back to Windows' default.
                foreach (var op in t.Ops.Where(o => o.Value != null))
                    Write(op.Hive, op.Key, op.Name, op.Default, KindOf(op.Default ?? op.Value));
                log("DEFAULT   " + t.Title + "  (no backup found, reset to Windows default)");
                return true;
            }
            catch (Exception ex) { log("FAILED    " + t.Title + "  (" + ex.Message + ")"); return false; }
        }

        // ---------- backup ----------

        Backup.Entry Snapshot(string tweakId, RegOp op)
        {
            var v = Read(op.Hive, op.Key, op.Name, out var kind);
            return new Backup.Entry
            {
                TweakId = tweakId, Hive = op.Hive, Key = op.Key, Name = op.Name,
                Existed = v != null, Kind = v != null ? kind : KindOf(op.Value),
                Value = v == null ? "" : Backup.Encode(v, kind),
                CreatedKey = TopmostMissingKey(op.Hive, op.Key),
            };
        }

        /// <summary>The highest part of the key path that doesn't exist yet ("" if the key exists),
        /// so revert can remove keys this app created (only if they're empty).</summary>
        string TopmostMissingKey(Hive hive, string key)
        {
            var parts = key.Split('\\');
            for (int i = 1; i <= parts.Length; i++)
            {
                var prefix = string.Join("\\", parts.Take(i));
                if (!KeyExists(hive, prefix)) return prefix;
            }
            return "";
        }

        void DeleteEmptyKeys(Hive hive, string key, string stopAt)
        {
            if (!key.StartsWith(stopAt, StringComparison.OrdinalIgnoreCase)) return;
            using (var root = Root(hive))
            {
                for (var cur = key; cur.Length >= stopAt.Length; cur = cur.Substring(0, Math.Max(cur.LastIndexOf('\\'), 0)))
                {
                    using (var k = root.OpenSubKey(MapKey(hive, cur)))
                        if (k == null || k.SubKeyCount > 0 || k.ValueCount > 0) break;
                    root.DeleteSubKey(MapKey(hive, cur), false);
                    if (cur.Length == stopAt.Length) break;
                }
            }
        }
    }

    /// <summary>Backup file: one tab-separated line per registry value, fields URL-encoded.</summary>
    public static class Backup
    {
        public sealed class Entry
        {
            public string TweakId, Key, Name, Value, CreatedKey;
            public Hive Hive;
            public bool Existed;
            public RegistryValueKind Kind;

            public object GetValue()
            {
                switch (Kind)
                {
                    case RegistryValueKind.DWord: return int.Parse(Value);
                    case RegistryValueKind.QWord: return long.Parse(Value);
                    case RegistryValueKind.Binary: return Convert.FromBase64String(Value);
                    case RegistryValueKind.MultiString: return Value.Split('\0');
                    default: return Value;
                }
            }
        }

        public static string Encode(object v, RegistryValueKind kind)
        {
            switch (kind)
            {
                case RegistryValueKind.Binary: return Convert.ToBase64String((byte[])v);
                case RegistryValueKind.MultiString: return string.Join("\0", (string[])v);
                default: return Convert.ToString(v);
            }
        }

        public static List<Entry> Load(string path)
        {
            if (!File.Exists(path)) return new List<Entry>();
            return File.ReadAllLines(path)
                .Where(l => l.Length > 0 && !l.StartsWith("#"))
                .Select(l => l.Split('\t').Select(Uri.UnescapeDataString).ToArray())
                .Where(f => f.Length == 8)
                .Select(f => new Entry
                {
                    TweakId = f[0], Hive = (Hive)Enum.Parse(typeof(Hive), f[1]), Key = f[2], Name = f[3],
                    Existed = f[4] == "1", Kind = (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), f[5]),
                    Value = f[6], CreatedKey = f[7],
                })
                .ToList();
        }

        public static void Save(string path, List<Entry> entries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var lines = new List<string> { "# Fastyourpc backup: original values, used by Revert. Don't edit." };
            lines.AddRange(entries.Select(e => string.Join("\t", new[]
            {
                e.TweakId, e.Hive.ToString(), e.Key, e.Name, e.Existed ? "1" : "0", e.Kind.ToString(), e.Value, e.CreatedKey,
            }.Select(Uri.EscapeDataString))));
            File.WriteAllLines(path, lines);
        }
    }
}
