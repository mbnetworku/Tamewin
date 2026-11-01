using System;
using System.Linq;

namespace Tamewin
{
    public enum Hive { CU, LM }

    /// <summary>One registry value a tweak sets. Value is an int (REG_DWORD), a string (REG_SZ), or null (value is deleted).</summary>
    public sealed class RegOp
    {
        public Hive Hive;
        public string Key;
        public string Name;     // "" = the key's (Default) value
        public object Value;
        public object Default;  // used on revert only when there is no backup; null = delete the value

        public RegOp(Hive hive, string key, string name, object value, object def = null)
        {
            Hive = hive; Key = key; Name = name; Value = value; Default = def;
        }
    }

    /// <summary>One option the user can tick. Applying sets all its RegOps; reverting puts back what was there before.</summary>
    public sealed class Tweak
    {
        public string Id;
        public string Group;
        public string Title;
        public string Info;
        public RegOp[] Ops;
        public string[] Conflicts = new string[0];  // tweaks reverted first when this one is applied
        public string Note;                          // what's needed for it to take effect
        public string Warning;                       // shown before applying
        public bool OptIn;                           // not included in "recommended" / --apply all

        /// <summary>HKLM and any ...\Policies\... key can only be written by an Administrator.</summary>
        public bool Admin => Ops.Any(o => o.Hive == Hive.LM ||
                                          o.Key.StartsWith(@"Software\Policies\", StringComparison.OrdinalIgnoreCase));

        public Tweak Needs(string note) { Note = note; return this; }
        public Tweak Warn(string warning) { Warning = warning; return this; }
        public Tweak ConflictsWith(params string[] ids) { Conflicts = ids; return this; }
        public Tweak Optional() { OptIn = true; return this; }
    }
}
