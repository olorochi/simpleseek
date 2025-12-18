namespace Simpleseek;

struct Line {
    public string Text;

    public Line(string text) {
        Text = text;
    }

    public void Write() {
        int w = Console.WindowWidth;
        Console.Write(
            Text.Length < w ? Text.PadRight(w) : Text[..w]
        );
    }
}

static class DirBrowser {
    static List<Directory> Files = new(256);
    static List<Line> Lines = new(Console.WindowHeight * 2);
    static int LineOffset; // first shown line
    static int FileOffset; // belongs to what file
    static int ShownFiles;
    // static int Selected;
    static bool redraw;
    const int Top = 1;
    const int Left = 0;

    static int Height {get => Console.WindowHeight - Top;}

    public static void Add(Directory dir) {
        // TODO: sorting
        Files.Add(dir);
        if (Lines.Count < Height) BuildLines();
        // if (pos <= FileOffset)
        //     if (Files.Count != 1) ++LineOffset;
        //     else BuildLines();
        // else if (Lines.Count < Height || pos - FileOffset <= ShownFiles) BuildLines();
    }

    public static void Clear() {
        Files.Clear();
        Lines.Clear();
        Console.SetCursorPosition(Left, Top);
        Console.Write(new string(' ', Console.WindowWidth * Height));
        Program.PlaceConsoleCur();
    }

    public static void SelUp() {
        throw new NotImplementedException();
        // --LineOffset; // placeholder implementation. Should affect Selected instead.
        // int last = Height + LineOffset;
        // if (Lines[last].Text.Length == 0) Lines.RemoveRange(last, Lines.Count - last);
        // if (--LineOffset > 0) return;

        // redraw = true;
        // Directory dir = Files[--FileOffset];
        // List<Line> lines = new(dir.ChildCount);
        // dir.BuildLines(lines);
        // Lines.InsertRange(0, lines);
        // BuildLines();
    }

    public static void SelDown() {
        throw new NotImplementedException();
    }

    public static File GetSel() {
        throw new NotImplementedException();
    }

    public static void Display() {
        if (!redraw) return;
        redraw = false;
        Console.SetCursorPosition(Left, Top);
        int last = Math.Min(Height + LineOffset, Lines.Count);
        for (int i = LineOffset; i < last; ++i) 
            Lines[i].Write();

        Program.PlaceConsoleCur();
    }

    static void BuildLines() { // could reuse some prebuilt lines with more state or arguments
        redraw = true;
        Lines.Clear();
        ShownFiles = FileOffset;
        do Files[ShownFiles].BuildLines(Lines);
        while (Lines.Count - LineOffset < Height && ++ShownFiles < Files.Count);
        ShownFiles -= FileOffset;
    }
}
