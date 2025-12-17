using Soulseek;

namespace SimpleSeek;

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

    public virtual string ToString(int level) => $"{String.Concat(Enumerable.Repeat('\t', level))}{name}\n";
    public override string ToString() => name;
}

class Directory : File {
    public List<File> children;

    public Directory(string name, int cap = 1) : base(name) {
        kind = File.Kind.Directory;
        children = new(cap);
    }

    public Directory(SearchResponse resp) : base(resp.Username) {
        kind = File.Kind.Directory;
        children = new();
        CreateTree(resp.Files, this);
    }

    public override string ToString(int level) {
        StringWriter writer = new();
        writer.Write(base.ToString(level++));
        foreach (var child in children)
            writer.Write(child.ToString(level));

        writer.Close();
        string ret = writer.ToString();
        return ret;
    }
    public override string ToString() => ToString(0);

    private static void FinalizeDir(List<Directory> loc, int depth) {
        for (int d = depth; d < loc.Count - 1; ++d) {
            Directory current = loc[d];
            if (current.children.Count != 1) {
                current.children.TrimExcess();
                continue;
            };

            Directory child = (Directory)current.children[0];
            // string += forces more heap allocations than necessary. We could use a StringBuilder for the name (but we're heavily network bound anyways)
            current.name += child.name;
            current.children = child.children;
            loc[d + 1] = current;
        }

        loc[^1].children.TrimExcess();
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
            loc.Last().children.Add(dir);
            loc.Add(dir);
            start = c;
        }
        loc.Last().children.Add(new(name.Substring(start, name.Length - start)));

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
                loc.Last().children.Add(dir);
                loc.Add(dir);
                start = c;
                c = NextDir(name, start);
            }
            loc.Last().children.Add(new(name.Substring(start, name.Length - start)));
        }

        FinalizeDir(loc, 1);
    }
}
