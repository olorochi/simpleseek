using Soulseek;
using System.Text;
using System.Collections.Concurrent;

namespace Simpleseek;

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
    static CancellationTokenSource Src = new();
    static SoulseekClient Client = new();
    static StringBuilder Input = new();
    static Task Conn;
    static bool Exit;
    static int Cursor;

    static User GetUser() {
        List<string?> vars = new (["SOULSEEK_USER", "SOULSEEK_PASSWORD"]);
        for (int i = 0; i < vars.Count; i++)
            vars[i] = Environment.GetEnvironmentVariable(vars[i]);


        var unset = vars.FindAll(s => s == null);
        if (unset.Count != 0) {
            Console.Write(
                $"Simpleseek is configured through environment variables. You must export the following: {string.Join(", ", unset)}"
            );
            Environment.Exit(1);
        }

        return new(vars[0], vars[1]);
    }

    static void HandleInput(InputEvent ev) {
        switch (ev.KeyInf.Key) {
            case ConsoleKey.Enter:
                Search();
                return;
            case ConsoleKey.Escape:
                Exit = true;
                return;
            case ConsoleKey.RightArrow:
                if (Cursor < Input.Length)
                    ++Cursor;
                break;
            case ConsoleKey.LeftArrow:
                Cursor = Math.Max(0, Cursor - 1);
                break;
            case ConsoleKey.PageUp:
            case ConsoleKey.Home:
                Cursor = 0;
                break;
            case ConsoleKey.PageDown:
            case ConsoleKey.End:
                Cursor = Input.Length;
                break;
            case ConsoleKey.UpArrow:
                DirBrowser.SelUp();
                break;
            case ConsoleKey.DownArrow:
                DirBrowser.SelDown();
                break;
            case ConsoleKey.Delete:
                DeleteInputChar(Cursor);
                break;
            case ConsoleKey.Backspace:
                DeleteInputChar(Cursor - 1);
                goto case ConsoleKey.LeftArrow;
            default:
                char c = ev.KeyInf.KeyChar;
                if (c != '\0') {
                    ++Cursor;
                    Input.Append(ev.KeyInf.KeyChar);
                }
                break;
        }

        DisplayInput();
    }

    static void Search() {
        string query = Input.ToString();
        if (!String.IsNullOrWhiteSpace(query)) {
            Src.Cancel();
            Src = new();
            DirBrowser.Clear();
            Conn.Wait();
            Client.SearchAsync(new(Input.ToString()), cancellationToken: Src.Token);
        }
    }

    static void DeleteInputChar(int pos) {
        if ((uint)pos < Input.Length)
            Input.Remove(pos, 1);
    }

    public static void PlaceConsoleCur() {
        int wrap = Cursor / Console.BufferWidth;
        Console.SetCursorPosition(Cursor - wrap * Console.BufferWidth, wrap);
    }

    static void HandleResponse(ResponseEvent ev) {
        DirBrowser.Add(ev.Dir);
    }

    static void DisplayInput() {
        Console.SetCursorPosition(0, 0);
        Console.Write($"{Input} "); // overwrite the extra char if we deleted one
        PlaceConsoleCur();
    }

    static void Main(string[] args) {
        User user = GetUser();
        Conn = Client.ConnectAsync(user.Name, user.Pass);

        BlockingCollection<Event> events = new();
        Client.SearchResponseReceived +=
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

            if (Exit) Environment.Exit(0);
            else if (events.Count == 0) DirBrowser.Display();
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
