namespace Simpleseek;

struct Line {
    public string Text;

    public Line(string text) {
        Text = text;
    }

    public void Write() {
        int w = Console.WindowWidth;
        Console.Write(Text.Length < w ? Text.PadRight(w) : Text[..w]);
    }

    public bool IsSep() => Text.Length == 0;
}

static class DirBrowser {
    static List<Root> Files = new(256);
    static List<Line> Lines = new(Console.WindowHeight * 2);
    static int LineOffset; // first shown line
    static int FileOffset; // belongs to what file
    static int ShownFiles;
    // static int Selected;
    static bool redraw;
    const int Top = 1;
    const int Left = 0;

    static int Height {get => Console.WindowHeight - Top;}
    static int Bottom {get => Height + LineOffset;}
    static int LastLine {get => Math.Min(Bottom, Lines.Count - 1);}
    static int LastFile {get => FileOffset + ShownFiles;}

    public static void Add(Root dir) {
        int pos = BinarySearchInsert(dir);
        Files.Insert(pos, dir);

        if (pos <= FileOffset)
            if (Lines.Count < Height) {
                FileOffset = pos;
                BuildLines();
            } else ++FileOffset;
        else if (Lines.Count < Height || pos - FileOffset <= ShownFiles) BuildLines();
    }

    public static void Clear() {
        Files.Clear();
        Lines.Clear();
        FileOffset = 0;
        LineOffset = 0;
        ShownFiles = 0;
        Console.SetCursorPosition(Left, Top);
        Console.Write(new string(' ', Console.WindowWidth * Height));
        Program.PlaceConsoleCur();
    }

    public static void Up() {
        if (FileOffset == 0 && LineOffset == 0) return;
        redraw = true;
        --LineOffset;
        if (Lines[LastLine].IsSep()) RemoveLast();
        if (LineOffset == -1) LinePrepend();
    }

    static void LinePrepend() {
        --FileOffset;
        LineOffset += LineInsert(FileOffset, 0);
    }

    static int LineInsert(int dir, int pos) {
        var d = Files[dir];
        int count = d.CountChildren();

        List<Line> l = new(count);
        d.BuildLines(l);
        Lines.InsertRange(pos, l);
        ++ShownFiles;
        return count;
    }

    public static void Down() {
        if (LastFile == Files.Count - 1 && LastLine == Lines.Count - 1 || Lines.Count < Bottom) return;
        redraw = true;

        if (Lines[LineOffset].IsSep()) RemoveFirst();
        else ++LineOffset;

        if (Lines[Bottom - 1].IsSep()) LineAppend();
    }

    static void LineAppend() {
        Files[LastFile].BuildLines(Lines);
        ++ShownFiles;
    }

    static void RemoveLast() {
        int past = LastLine + 1;
        Lines.RemoveRange(past, Lines.Count - past);
        --ShownFiles;
    }
    static void RemoveFirst() {
        Lines.RemoveRange(0, LineOffset + 1);
        LineOffset = 0;
        ++FileOffset;
        --ShownFiles;
    }

    public static void SelUp() {
        throw new NotImplementedException();
    }

    public static void SelDown() {
        throw new NotImplementedException();
    }

    public static File GetSel() {
        throw new NotImplementedException();
    }

    public static void Display() {
        if (!redraw) return; // TODO: redraw should be an int that indicates from which display line to start drawing
        redraw = false;
        Console.SetCursorPosition(Left, Top);
        int last = Math.Min(Height + LineOffset, Lines.Count);
        for (int i = LineOffset; i < last; ++i) 
            Lines[i].Write();

        Program.PlaceConsoleCur();
    }

    static void BuildLines() { // TODO: remove
        redraw = true;
        Lines.Clear();
        ShownFiles = FileOffset;
        do Files[ShownFiles].BuildLines(Lines);
        while (Lines.Count - LineOffset < Height && ++ShownFiles < Files.Count);
        ShownFiles -= FileOffset;
    }

    static int BinarySearchInsert(Root dir) {
        int n = dir.Speed;
        int low = 0;
        int high = Files.Count;

        while (low < high) {
            int mid = low + ((high - low) >> 1);
            if (Files[mid].Speed > n) low = mid + 1;
            else high = mid;
        }

        return low;
    }
}
