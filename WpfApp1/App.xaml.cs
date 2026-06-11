using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Application = System.Windows.Application;

namespace QuickSticky
{
    public partial class App : Application
    {
        public static string NotesDir { get; private set; } = null!;

        private static Mutex _singleInstanceMutex;
        private static bool _ownsSingleInstanceMutex;

        private const string MutexName = @"Global\QuickSticky_SingleInstance_v1";
        private static string PipeName => @"QuickStickyPipe_v1";

        private readonly CancellationTokenSource _cts = new();

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            NotesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "QuickSticky",
                "Notes");

            Directory.CreateDirectory(NotesDir);

            bool createdNew;

            try
            {
                _singleInstanceMutex = new Mutex(true, MutexName, out createdNew);
            }
            catch (UnauthorizedAccessException)
            {
                _singleInstanceMutex = new Mutex(true, @"Local\QuickSticky_SingleInstance_v1", out createdNew);
            }
            catch (AbandonedMutexException)
            {
                createdNew = true;
            }

            _ownsSingleInstanceMutex = createdNew;

            var args = (e.Args ?? Array.Empty<string>()).ToArray();

            if (!createdNew)
            {
                EnsureShellNewFileExists(args);

                SendToPrimary(args);

                Shutdown();
                return;
            }

            StartPipeServer();

            bool opened = HandleArgs(args);

            if (args.Length == 0 && !opened)
            {
                var path = NoteStorage.GenerateNewPath(NotesDir);
                CreateNote(path, spawnAtCursor: true);
                //Shutdown();
            }
        }

        private bool HandleArgs(string[] args)
        {
            if (args.Length >= 2 && args[0].Equals("/open", StringComparison.OrdinalIgnoreCase))
            {
                var file = args[1].Trim('"');

                if (File.Exists(file))
                {
                    OpenNoteFile(file);
                    return true;
                }

                return false;
            }

            if (args.Length >= 2 && args[0].Equals("/new-from-shell", StringComparison.OrdinalIgnoreCase))
            {
                var shellPath = args[1].Trim('"');
                var finalPath = NoteStorage.GenerateNewPath(NotesDir);

                try
                {
                    SafeMove(shellPath, finalPath);
                }
                catch
                {
                    // ignore
                }

                CreateNote(finalPath, spawnAtCursor: true);
                return true;
            }

            if (args.Length >= 1 && args[0].Equals("/new", StringComparison.OrdinalIgnoreCase))
            {
                var path = NoteStorage.GenerateNewPath(NotesDir);
                CreateNote(path, spawnAtCursor: true);
                return true;
            }

            return LoadAllNotes() > 0;
        }
        
        private static void EnsureShellNewFileExists(string[] args)
        {
            if (args.Length < 2 ||
                !args[0].Equals("/new-from-shell", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var shellPath = args[1].Trim('"');

            try
            {
                var dir = Path.GetDirectoryName(shellPath);

                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                if (!File.Exists(shellPath))
                {
                    using (File.Create(shellPath)) { }
                }
            }
            catch
            {
                // ignore
            }
        }

        private static void SafeMove(string source, string dest)
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    File.Move(source, dest, true);
                    return;
                }
                catch
                {
                    Thread.Sleep(50);
                }
            }
        }

        private int LoadAllNotes()
        {
            int count = 0;

            foreach (var f in Directory.EnumerateFiles(NotesDir, "*.qnote"))
            {
                try
                {
                    OpenNote(NoteStorage.Load(f), f);
                    count++;
                }
                catch
                {
                    // ignore bad note files
                }
            }

            return count;
        }

        private void CreateNote(string path, bool spawnAtCursor)
        {
            var model = spawnAtCursor ? NoteModel.NewBlankAtCursor() : new NoteModel();
            OpenNote(model, path);
        }

        public void OpenNoteFile(string path)
        {
            var model = NoteStorage.Load(path);

            if (model.Removed)
                path = NoteStorage.RestoreFromBackup(path, NotesDir, model);

            OpenNote(model, path);
        }

        private void OpenNote(NoteModel model, string path)
        {
            var w = new NoteWindow(model, path)
            {
                ShowInTaskbar = false
            };

            w.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _cts.Cancel();

            if (_singleInstanceMutex != null)
            {
                if (_ownsSingleInstanceMutex)
                {
                    try
                    {
                        _singleInstanceMutex.ReleaseMutex();
                    }
                    catch (ApplicationException)
                    {
                        // Mutex was not owned anymore. Ignore.
                    }
                }

                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                _ownsSingleInstanceMutex = false;
            }

            base.OnExit(e);
        }

        private static void SendToPrimary(string[] args)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(1500);

                using var writer = new StreamWriter(client)
                {
                    AutoFlush = true
                };

                string cmd = args.Length > 0 ? args[0] : "";
                string arg = args.Length > 1 ? args[1] : "";

                writer.WriteLine($"{cmd}|{arg}");
            }
            catch
            {
                // primary instance may not be ready
            }
        }

        private void StartPipeServer()
        {
            Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                        await server.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);

                        using var reader = new StreamReader(server);
                        var line = await reader.ReadLineAsync().ConfigureAwait(false);

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        var parts = line.Split(new[] { '|' }, 2);
                        var cmd = parts[0];
                        var arg = parts.Length > 1 ? parts[1] : "";

                        Dispatcher.Invoke(() => HandleArgs(new[] { cmd, arg }));
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // keep server alive
                    }
                }
            });
        }
    }
}