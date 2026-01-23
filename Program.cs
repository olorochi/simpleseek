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
    Response,
    Status
}

abstract class Event {
    public EvType Type;

    public Event(EvType type) {
        Type = type;
    }
}

class StatusEvent : Event {
    public StatusEvent() : base(EvType.Status) { }
}

class InputEvent : Event {
    public ConsoleKeyInfo KeyInf;

    public InputEvent(ConsoleKeyInfo keyInf) : base(EvType.Input) {
        KeyInf = keyInf;
    }
}

class ResponseEvent : Event {
    public Root Dir;

    public ResponseEvent(Root dir) : base(EvType.Response) {
        Dir = dir;
    }
}

static class Program {
    static CancellationTokenSource Src = new();
    static SoulseekClient Client = new();
    static StringBuilder Input = new();
    static Task Conn;
    static int Cursor;

    public static string Repeat(string s, int n) => String.Concat(Enumerable.Repeat(s, n));

    static User GetUser() {
        bool warn = true;

        List<string> vars = new (["SOULSEEK_USER", "SOULSEEK_PASSWORD"]);
        List<string?> set = new(vars.Count);
        for (int i = 0; i < vars.Count; i++) {
            set.Add(Environment.GetEnvironmentVariable(vars[i]));
            if (set[i] == null) {
                if (warn) {
                    Console.WriteLine("Simpleseek is configured through environment variables. You must export the following: ");
                    warn = false;
                }

                Console.WriteLine(vars[i]);
            }
        }

        if (!warn) Exit(1); // if we have warned the user
        return new(set[0], set[1]);
    }

    static void HandleInput(InputEvent ev) {
        switch (ev.KeyInf.Key) {
            case ConsoleKey.F1: // Placeholder key. Should be enter but depend on current focus.
                Download();
                return;
            case ConsoleKey.Enter:
                Search();
                return;
            case ConsoleKey.Escape:
                Exit(0);
                return;
            case ConsoleKey.PageUp:
                DirBrowser.Up();
                return;
            case ConsoleKey.PageDown:
                DirBrowser.Down();
                return;
            case ConsoleKey.UpArrow:
                DirBrowser.SelUp();
                return;
            case ConsoleKey.DownArrow:
                DirBrowser.SelDown();
                return;
            case ConsoleKey.RightArrow:
                if (Cursor < Input.Length)
                    ++Cursor;
                break;
            case ConsoleKey.LeftArrow:
                Cursor = Math.Max(0, Cursor - 1);
                break;
            case ConsoleKey.Home:
                Cursor = 0;
                break;
            case ConsoleKey.End:
                Cursor = Input.Length;
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

    static async void Download() {
        string full = DirBrowser.GetSel();
        string user = full.Substring(0, full.IndexOf('\0'));
        string path = full.Substring(user.Length);

        try {
            await Client.DownloadAsync(
                    user,
                    path,
                    path,
                    options: new(

                        ));
        } catch (Exception e) {
            throw e;
        }
    }

    static void Search() {
        string query = Input.ToString();
        if (!String.IsNullOrWhiteSpace(query)) {
            Src.Cancel();
            Src = new();
            DirBrowser.Clear();
            DirBrowser.EnsureConn(Conn);

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

    public static void Exit(int code) {
        Environment.Exit(code);
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

        Thread statusThread = new(() => SecondaryThreads.StatusThread(events));
        statusThread.Start();

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
                case EvType.Status:
                    Statusbar.UpdateStatus(Client.Downloads);
                    Statusbar.Display();
                    break;
            }

            if (events.Count == 0) DirBrowser.Display();
        }
    }
}

// keep static vars in Program thread safe
static class SecondaryThreads {
    public static void InputThread(BlockingCollection<Event> evs) {
        while (true) evs.Add(new InputEvent(Console.ReadKey(true)));
    }

    public static void StatusThread(BlockingCollection<Event> evs) {
        while (true) {
            evs.Add(new StatusEvent());
            Thread.Sleep(1000);
        }
    }

    public static void OnSearchResp(BlockingCollection<Event> evs, SearchResponse resp) {
        evs.Add(new ResponseEvent(new(resp)));
    }
}
