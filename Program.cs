using Soulseek;
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
    static SoulseekClient Client = new(9999);
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
            case ConsoleKey.Enter: // Placeholder key. Should be enter but depend on current focus.
                Download();
                break;
            case ConsoleKey.Escape:
                Exit(0);
                break;
            case ConsoleKey.PageUp:
                DirBrowser.Up();
                break;
            case ConsoleKey.PageDown:
                DirBrowser.Down();
                break;
            case ConsoleKey.UpArrow:
                DirBrowser.SelUp();
                break;
            case ConsoleKey.DownArrow:
                DirBrowser.SelDown();
                break;
            case ConsoleKey.LeftArrow:
                Cursor = Math.Max(0, Cursor - 1);
                break;
            case ConsoleKey.Home:
                Cursor = 0;
                break;
        }
    }

    static int token = 0;
    static async void Download() {
        (string user, string path) = DirBrowser.GetSel();
        string local = path.Replace('\\', '/');
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(local));

        try {
            await Client.DownloadAsync(
                    user,
                    path,
                    local,
                    startOffset: 0,
                    token: ++token
                    );
        } catch (Exception e) {
            Statusbar.Mes = $"{e.Message} ({local})";
        }
    }

    static void HandleResponse(ResponseEvent ev) {
        DirBrowser.Add(ev.Dir);
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

        Conn.Wait();
        string query = "Mizmor";
        if (String.IsNullOrWhiteSpace(query))
            Console.WriteLine("No query provided.");
        else
            Client.SearchAsync(new(query));

        foreach (var ev in events.GetConsumingEnumerable()) {
            switch (ev.Type) {
                case EvType.Input:
                    HandleInput((InputEvent)ev);
                    break;
                case EvType.Response:
                    HandleResponse((ResponseEvent)ev);
                    break;
                case EvType.Status:
                    Statusbar.Update(Client.Downloads);
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
