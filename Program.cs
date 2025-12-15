using Soulseek;

namespace SimpleSeek;

struct UserInfo {
    public string user;
    public string pwd;
}

static class Program {
    static UserInfo userInfo;

    public static int IndexOfFrom(ReadOnlySpan<char> s, int i, char c = '\\') {
        do if (s[i++] == c) return i;
        while (i < s.Length);
        return s.Length;
    }

    static SearchResponse[]? FromCache(string file) {
        if (!System.IO.File.Exists(file))
            return null;

        BinaryReader reader = new(System.IO.File.Open(file, FileMode.Open));
        SearchResponse[] resps = new SearchResponse[reader.ReadInt32()];
        for (int i = 0; i < resps.Length; ++i) {
            string user = reader.ReadString();
            Soulseek.File[] files = new Soulseek.File[reader.ReadInt32()];
            for (int f = 0; f < files.Length; ++f)
                files[f] = new Soulseek.File(0, reader.ReadString(), 0, "");

            resps[i] = new(user, 0, false, 0, 0, files);
        }

        reader.Close();
        return resps;
    }

    static void CacheResps(SearchResponse[] resps, int size, string file) {
        BinaryWriter writer = new(System.IO.File.Open(file, FileMode.OpenOrCreate));
        writer.Write(size);
        for (int i = 0; i < size; ++i) {
            var r = resps[i];
            writer.Write(r.Username);
            writer.Write(r.Files.Count);
            foreach(var f in r.Files) {
                writer.Write(f.Filename);
            }
        }

        writer.Close();
    }

    static int Main(string[] args) {
        string cache = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/simpleseek"; // for reproducible tests
        SearchResponse[]? resps = FromCache(cache);
        if (resps == null) {
            userInfo.user = Environment.GetEnvironmentVariable("SOULSEEK_USER");
            userInfo.pwd = Environment.GetEnvironmentVariable("SOULSEEK_PASSWORD");

            SoulseekClient client = new();
            Task conn = client.ConnectAsync(userInfo.user, userInfo.pwd);
            conn.Wait();

            resps = new SearchResponse[256];
            Task search = client.SearchAsync(new("Evoken"), options: new(searchTimeout: 2000, responseLimit: resps.Length));

            int i = -1;
            client.SearchResponseReceived += (object? sender, SearchResponseReceivedEventArgs ev) => {
                Interlocked.Increment(ref i);
                resps[i] = (ev.Response);
            };
            search.Wait();

            try {
                CacheResps(resps, i, cache);
            } catch (Exception e) {
                Console.WriteLine($"failed to cache responses: {e.Message}");
                System.IO.File.Delete(cache);
            }
        }

        List<File> files = new(resps.Length);
        foreach (var r in resps)
            try {
                if (r.FileCount > 0) files.Add(new Directory(r));
            } catch(Exception e) {
                Console.WriteLine($"Threw while processing response from {r.Username}. {e.Message}:\n {e.StackTrace}");
            }
        foreach (var f in files)
            Console.WriteLine(f);
        return 0;
    }
}
