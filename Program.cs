using Soulseek;
using System.Text;
using System.Collections.Concurrent;

namespace SimpleSeek;

struct User(string name, string pass) {
    public string Name = name;
    public string Pass = pass;
}

enum EvType {
    Input,
    Response
}

abstract class Event(EvType type) {
    public EvType Type = type;
}

class InputEvent(ConsoleKeyInfo key) : Event(EvType.Input) {
    public ConsoleKeyInfo Key = key;
}

class ResponseEvent(Directory dir) : Event(EvType.Response) {
    public Directory Dir = dir;
}

static class Program {
    static StringBuilder input = new();
    static bool redraw;

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

    static void HandleInput() {

    }

    static void HandleResponse() {

    }

    static void DisplayFiles() {

    }

    static void Main(string[] args) {
        User user = GetUser();

        SoulseekClient client = new();
        Task conn = client.ConnectAsync(user.Name, user.Pass);

        List<Directory> files = new();
        BlockingCollection<Event> events = new();
        client.SearchResponseReceived += (object? sender, SearchResponseReceivedEventArgs ev) => {
            events.Add(new ResponseEvent(new(ev.Response)));
        };

        Thread inputThread = new(() => {
            while (true) events.Add(new InputEvent(Console.ReadKey(true)));
        });

        foreach (var ev in events.GetConsumingEnumerable()) {
            switch (ev.Type) {
                case EvType.Input:
                    HandleInput();
                    break;
                case EvType.Response:
                    HandleResponse();
                    break;
            }

            if (events.Count == 0 && redraw) DisplayFiles();
        }
    }
}
