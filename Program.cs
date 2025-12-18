using Soulseek;
using System.Text;
using System.Collections.Concurrent;

namespace SimpleSeek;

struct User {
    public string Name;
    public string Pass;

    public User(string name, string pass) {
        Name = name;
        Pass = pass;
    }
}

enum EvType {
    Input,
    Response
}

abstract class Event {
    public EvType Type;

    public Event(EvType type) {
        Type = type;
    }
}

class InputEvent : Event {
    public ConsoleKeyInfo KeyInf;

    public InputEvent(ConsoleKeyInfo keyInf) : base(EvType.Input) {
        KeyInf = keyInf;
    }
}

class ResponseEvent : Event {
    public Directory Dir;

    public ResponseEvent(Directory dir) : base(EvType.Response) {
        Dir = dir;
    }
}

static class Program {
    static CancellationTokenSource src = new();
    static SoulseekClient client = new();
    static StringBuilder input = new();
    static List<Directory> files = new();
    static bool redraw;
    static bool exit;
    static int cursor;

    static User GetUser() {
        List<string?> vars = new (["SOULSEEK_USER", "SOULSEEK_PASSWORD"]);
        for (int i = 0; i < vars.Count; i++) {
            vars[i] = Environment.GetEnvironmentVariable(vars[i]);
        }

        var unset = vars.FindAll(s => s == null);
        if (unset.Count != 0) {
            Console.Write(
                $"SimpleSeek is configured through environment variables. You must export the following: {string.Join(", ", unset)}"
            );
            Environment.Exit(1);
        }

        return new(vars[0], vars[1]);
    }

    static void HandleInput(InputEvent ev) {
        switch (ev.KeyInf.Key) {
            case ConsoleKey.Enter:
                src.Cancel();
                src = new();
                files.Clear();
                string query = input.ToString();
                if (!String.IsNullOrWhiteSpace(query))
                    client.SearchAsync(new(input.ToString()), cancellationToken: src.Token);
                return;
            case ConsoleKey.Escape:
                exit = true;
                return;
            case ConsoleKey.RightArrow:
                if (cursor < input.Length)
                    ++cursor;
                break;
            case ConsoleKey.LeftArrow:
                cursor = Math.Max(0, cursor - 1);
                break;
            case ConsoleKey.PageUp:
            case ConsoleKey.UpArrow:
            case ConsoleKey.Home:
                cursor = 0;
                break;
            case ConsoleKey.PageDown:
            case ConsoleKey.DownArrow:
            case ConsoleKey.End:
                cursor = input.Length;
                break;
            case ConsoleKey.Delete:
                DeleteInputChar(cursor);
                break;
            case ConsoleKey.Backspace:
                DeleteInputChar(cursor - 1);
                goto case ConsoleKey.LeftArrow;
            default:
                char c = ev.KeyInf.KeyChar;
                if (c != '\0') {
                    ++cursor;
                    input.Append(ev.KeyInf.KeyChar);
                }
                break;
        }

        DisplayInput();
    }

    static void DeleteInputChar(int pos) {
        if ((uint)pos < input.Length)
            input.Remove(pos, 1);
    }

    static void PlaceConsoleCur() {
        int wrap = cursor / Console.BufferWidth;
        Console.SetCursorPosition(cursor - wrap * Console.BufferWidth, wrap);
    }

    static void HandleResponse(ResponseEvent ev) {
        // TODO: sorting
        files.Add(ev.Dir);
        redraw = true; // TODO: check for visibility
    }

    static void DisplayFiles()
    {
        redraw = false;
        Console.Clear();
        Console.SetCursorPosition(0, cursor / Console.BufferWidth + 1);

        int remainingLines = Console.BufferHeight - Console.CursorTop - 1;
        var sb = new StringBuilder();

        foreach (var file in files)
        {
            if (remainingLines <= 0)
                break;

            file.WriteTo(sb, 0, ref remainingLines);
        }

        Console.Write(sb);
        DisplayInput();
    }

    static void DisplayInput() {
        Console.SetCursorPosition(0, 0);
        Console.Write($"{input} "); // overwrite the extra char if we deleted one
        PlaceConsoleCur();
    }

    static void Main(string[] args) {
        User user = GetUser();

        Task conn = client.ConnectAsync(user.Name, user.Pass);

        BlockingCollection<Event> events = new();
        client.SearchResponseReceived +=
            (object? sender, SearchResponseReceivedEventArgs ev) =>
                SecondaryThreads.OnSearchResp(events, ev.Response);

        Thread inputThread = new(() => SecondaryThreads.InputThread(events));
        inputThread.Start();

        Console.SetCursorPosition(0, 0);
        Console.Clear();
        foreach (var ev in events.GetConsumingEnumerable()) {
            switch (ev.Type) {
                case EvType.Input:
                    HandleInput((InputEvent)ev);
                    break;
                case EvType.Response:
                    HandleResponse((ResponseEvent)ev);
                    break;
            }

            if (exit) Environment.Exit(0);
            else if (events.Count == 0 && redraw) DisplayFiles();
        }
    }
}

// keep static vars in Program thread safe
static class SecondaryThreads {
    public static void InputThread(BlockingCollection<Event> evs) {
        while (true) evs.Add(new InputEvent(Console.ReadKey(true)));
    }

    public static void OnSearchResp(BlockingCollection<Event> evs, SearchResponse resp) {
            evs.Add(new ResponseEvent(new(resp)));
    }
}
