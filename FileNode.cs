using System.Runtime.InteropServices;
using Soulseek;

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

    public virtual void BuildLine(List<Line> lines, int level = 0)
    {
        lines.Add(new Line(
            $"{String.Concat(Enumerable.Repeat("    ", level))}{name}"
        ));
    }
}

class Directory : File {
    public List<File> Children;

    public Directory(string name, int cap = 1) : base(name) {
        kind = File.Kind.Directory;
        Children = new(cap);
    }

    public Directory(SearchResponse resp) : base(resp.Username) {
        kind = File.Kind.Directory;
        Children = new();
        CreateTree(resp.Files, this);
    }

    public void BuildLines(List<Line> lines, DirIterator it)
    {
        it.ChangeRoot(this);
        BuildLine(lines, 0);
        while (it.Next()) it.Current.BuildLine(lines, it.Depth);
        lines.Add(new Line(string.Empty));
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
