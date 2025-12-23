using System.Runtime.InteropServices;
using Soulseek;
using static Simpleseek.Program;

namespace Simpleseek;

struct FileIterator {
    public Directory Dir;
    public int I; 

    public File Current => Dir.Children[I];
    public bool Next() => ++I < Dir.Children.Count;

    public FileIterator(Directory dir, int i) {
        Dir = dir;
        I = i;
    }
}

struct DirIterator {
    List<FileIterator> Loc;

    public File Current {get => Loc.Last().Current;}
    public int Depth {get => Loc.Count;}

    public DirIterator() {
        Loc = new(8);
    }

    public void ChangeRoot(Directory root) {
        Loc.Clear();
        Loc.Add(new(root, 0));
    }

    public bool Next() {
        if (Current.kind == File.Kind.Directory) {
            Loc.Add(new ((Directory)Current, 0));
            return true;
        }

        int remove = 0;
        var span = CollectionsMarshal.AsSpan(Loc);
        while (remove < Loc.Count - 1 && !span[^(remove + 1)].Next()) ++remove;

        if (remove > 0) {
            Loc.RemoveRange(Loc.Count - remove, remove);
            return Loc.Count != 1;
        }

        return true;
    }
}

class File {
    public enum Kind : byte {
        Regular,
        Directory
    }

    public string name;
    public Kind kind;

    public File(string name) {
        this.name = name;
    }

    public void BuildLine(List<Line> lines, int level = 0) {
        lines.Add(new($"{Repeat("    ", level)}{ToString()}"));
    }

    public override string ToString() => name;
}

class Directory : File {
    // avoids unnecessary heap allocations
    static DirIterator s_Writer = new();
    static DirIterator s_Counter = new();

    public List<File> Children;

    public Directory(string name, int cap = 1) : base(name) {
        kind = File.Kind.Directory;
        Children = new(cap);
    }

    public void BuildLines(List<Line> lines) {
        s_Writer.ChangeRoot(this);
        lines.Add(new(ToString()));
        while (s_Writer.Next())
            s_Writer.Current.BuildLine(lines, s_Writer.Depth - 1);
        lines.Add(new(string.Empty));
    }

    public int CountChildren() {
        s_Counter.ChangeRoot(this);
        int i = 1;
        while (s_Counter.Next()) ++i;
        return i;
    }

    public int CountFiles() {
        s_Counter.ChangeRoot(this);
        int i = 0;
        do if (s_Counter.Current.kind == File.Kind.Regular) ++i;
        while (s_Counter.Next());
        return i;
    }

    private static void FinalizeDir(List<Directory> loc, int depth) {
        for (int d = depth; d < loc.Count - 1; ++d) {
            Directory current = loc[d];
            if (current.Children.Count != 1) {
                current.Children.TrimExcess();
                continue;
            };

            Directory child = (Directory)current.Children[0];
            // string+= forces more heap allocations than necessary. We could use a StringBuilder for the name (but we're heavily network bound anyways)
            current.name += child.name;
            current.Children = child.Children;
            loc[d + 1] = current;
        }

        loc[^1].Children.TrimExcess();
        loc.RemoveRange(depth, loc.Count - depth);
    }

    // returns 1 past the next '\\' or the end
    public static int NextDir(ReadOnlySpan<char> s, int i) {
        do if (s[i++] == '\\') return i;
        while (i < s.Length);
        return s.Length;
    }

    public static void CreateTree(IReadOnlyCollection<Soulseek.File> files, Directory root) {
        List<Directory> loc = new(8) {root};

        int start = 0;
        string name = files.ElementAt(0).Filename;
        for (int c = NextDir(name, 0); c < name.Length; c = NextDir(name, c)) {
            Directory dir = new(name.Substring(start, c - start));
            loc.Last().Children.Add(dir);
            loc.Add(dir);
            start = c;
        }
        loc.Last().Children.Add(new(name.Substring(start, name.Length - start)));

        for (int i = 1; i < files.Count; ++i) {
            name = files.ElementAt(i).Filename;
            int c = 0;
            for (int depth = 1; depth < loc.Count; ++depth) {
                start = c;
                c = NextDir(name, c);

                if (!loc[depth].name.StartsWith(name.Substring(start, c - start))) {
                    FinalizeDir(loc, depth);
                    goto newloc;
                }
            }

            start = c;
            c = NextDir(name, c);

            newloc:
            while (c < name.Length) {
                Directory dir = new(name.Substring(start, c - start));
                loc.Last().Children.Add(dir);
                loc.Add(dir);
                start = c;
                c = NextDir(name, start);
            }
            loc.Last().Children.Add(new(name.Substring(start, name.Length - start)));
        }

        FinalizeDir(loc, 1);
    }
}

class Root : Directory {
    public int Speed {get;}

    public override string ToString() => $"{name} - {Speed}kbps";

    public Root(SearchResponse resp) : base(resp.Username) {
        Speed = resp.UploadSpeed / 1024;
        CreateTree(resp.Files, this);
    }
}
