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

    public bool IsBlank() => Text.Length == 0;
}

static class DirBrowser {
    static List<Root> Files = new(256);
    static List<Line> Lines = new(Console.WindowHeight * 2);
    static int LineOffset; // first shown line
    static int FileOffset; // belongs to what file
    static int ShownFiles;
    static int Selected;
    static bool redraw;
    const int Top = 0;
    const int Left = 0;

    static int Height {get => Console.WindowHeight - 1;}
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
        Console.SetCursorPosition(0, 0);
        Console.Write(new string(' ', Console.WindowWidth * Height));
    }

    public static void Up() {
        Selected = Math.Min(Height - 1, Selected + 1);
        if (FileOffset == 0 && LineOffset == 0) return;
        redraw = true;
        --LineOffset;
        if (Lines[LastLine].IsBlank()) RemoveLast();
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
        Selected = Math.Max(0, Selected - 1);
        if (LastFile == Files.Count - 1 && LastLine == Lines.Count - 1 || Lines.Count < Bottom) return;
        redraw = true;

        if (Lines[LineOffset].IsBlank()) RemoveFirst();
        else ++LineOffset;

        if (Lines[Bottom - 1].IsBlank()) LineAppend();
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
        if (Lines.Count == 0) return;
        do if (--Selected < 0) Up();
        while (Lines[LineOffset + Selected].IsBlank());
        redraw = true;
    }

    public static void SelDown() {
        if (Lines.Count == 0) return;
        do if (++Selected == Height) Down();
        while (Lines[LineOffset + Selected].IsBlank());
        redraw = true;
    }

    public static (string, string) GetSel() {
        int parent = FileOffset;
        int child = 0;
        for (int i = LineOffset; i < LineOffset + Selected; ++i)
            if (Lines[i].IsBlank()) {
                ++parent;
                child = 0;
            } else ++child;

        return Files[parent].PathAt(child);
    }

    public static void Display() {
        if (!redraw) return; // TODO: redraw should be an int that indicates from which display line to start drawing
        redraw = false;
        Console.SetCursorPosition(0, 0);

        int last = Math.Min(Height + LineOffset, Lines.Count);
        for (int i = LineOffset; i < last; ++i) {
            if (i - LineOffset == Selected) {
                Console.BackgroundColor = ConsoleColor.White;
                Console.ForegroundColor = ConsoleColor.Black;
                Lines[i].Write();
                Console.ResetColor();
            } else Lines[i].Write();
        }
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
        int n = dir.Score();
        int low = 0;
        int high = Files.Count;

        while (low < high) {
            int mid = low + ((high - low) >> 1);
            if (Files[mid].Score() > n) low = mid + 1;
            else high = mid;
        }

        return low;
    }
}

static class Statusbar {
    public static string Mes {get; set;} = "";
    static string Status = "";
    static int id;

    public static void Update(IReadOnlyCollection<Soulseek.Transfer> transfers) {
        long total = 0;
        long progress = 0;

        foreach (var t in transfers) {
            total += t.Size;
            progress += t.BytesTransferred;
        }

        total /= 1024 * 1024 * 8;
        progress /= 1024 * 1024 * 8;
        Status = $"{progress}/{total}MiB";

        Console.SetCursorPosition(0, Console.WindowHeight - 1);
        Console.Write($"{Mes.PadRight(Console.WindowWidth - Status.Length)}{Status}");
    }
}
