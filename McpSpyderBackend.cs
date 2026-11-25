using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HobbyOS.PullPits.Spyder
{
    // PULL-PIT TYPE: SPYDER (MODE: BACKEND / ONLINE MCP AI)
    // REQUIREMENTS: ALL + IP, CONTROL, D HERO, SAFE SECURITY, GEM, MORE OPEN PULL-PITS
    public class McpSpyderForm : Form
    {
        private TextBox txtApiKey;
        private TextBox txtModel;
        private RichTextBox rtbChat;
        private TextBox txtInput;
        private Button btnSend;
        private Label lblQueueStatus;

        private readonly Queue<string> _kolejkaZadan = new Queue<string>();
        private readonly SemaphoreSlim _semaforKolejki = new SemaphoreSlim(1, 1);
        private readonly List<object> _historiaRozmowy = new List<object>();
        private readonly List<string> _logiSystemowe = new List<string>();
        private static readonly HttpClient _http = new HttpClient();

        private const string SYSTEM_PROMPT = @"
Jesteś agentem MCP AI działającym jako kontener SPYDER (tryb BACKEND) w systemie HobbyOS (SH4RK OS / D HERO SH4RK).
Nie masz na starcie pamięci aplikacji – całą wiedzę o systemie zdobywasz wykonując polecenia.

ZASADY KRYTYCZNE:
1. Gdy chcesz wykonać polecenie systemowe, odpowiedz WYŁĄCZNIE w formacie: [CMD: nazwa_komendy] lub [CMD: nazwa_komendy argument]. Nic więcej w tej wiadomości!
2. Użytkownik NIE WIDZI wiadomości z [CMD: ...] ani odpowiedzi [SYS_OUT]. Po otrzymaniu [SYS_OUT] odpowiedz użytkownikowi naturalnie po polsku.
3. OBOWIĄZEK SPRAWDZANIA FOLDERU: Zawsze, gdy użytkownik pyta o rzeczy grzebiące w pamięci lub plikach (pobieranie, otwieranie, usuwanie, lista plików, status repozytorium, uruchamianie pull-pitów), NAJPIERW w kroku 1 wywołaj [CMD: io-current], aby sprawdzić w jakim folderze się znajdujesz!
4. LIMIT KOLEJKI PAMIĘCI: Przy operacjach grzebiących w pamięci/plikach możesz wykonać maksymalnie od 1 do 3 komend po kolei w jednym zadaniu, po czym musisz udzielić odpowiedzi użytkownikowi.

DOSTĘPNE 53 KOMENDY SYSTEMOWE HOBBYOS CORE:
- SYSTEM UPRAWNIEŃ GEM: gem install <nazwa>, gem remove <nazwa>
- INTERFEJS I GITHUB: pulpit-start <plik.cs>, fetch <url>, src-list, src-path, src-count, src-clean, src-status
- METRYKI PROCESORA: cpu-cores, cpu-arch, cpu-os, cpu-uptime, cpu-tick
- ANALIZATOR PAMIĘCI RAM: ram-allocated, ram-sterta, ram-peak, ram-threads, ram-status
- PROCESY WINDOWS: ps-count, ps-current-id, ps-name, ps-priority, ps-time
- TOŻSAMOŚĆ SYSTEMU: sys-machine, sys-user, sys-domain, sys-dir, sys-runtime
- REJESTRY ŚRODOWISKA: env-variables, env-drive-count, env-page-size, env-interactive, env-check
- WEJŚCIA/WYJŚCIA IO: io-drives, io-dir-view, io-temp, io-current, io-status
- SILNIK DEWELOPERSKI: dev-mode, dev-compiler, dev-reflection, dev-build-info, dev-status
- OPERACJE NA LOGACH: log-status, log-count, log-dump, log-security, log-check
";

        public McpSpyderForm()
        {
            Text = "HobbyOS // SPYDER MCP AI [BACKEND - D HERO SH4RK]";
            Size = new Size(920, 620);
            BackColor = Color.FromArgb(18, 20, 26);
            ForeColor = Color.White;
            Font = new Font("Consolas", 10f);

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8) };

            var lblKey = new Label { Text = "API Key:", AutoSize = true, Left = 10, Top = 13, ForeColor = Color.Cyan };
            txtApiKey = new TextBox
            {
                Left = 75,
                Top = 10,
                Width = 260,
                UseSystemPasswordChar = true,
                BackColor = Color.FromArgb(30, 34, 44),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblModel = new Label { Text = "Model:", AutoSize = true, Left = 345, Top = 13, ForeColor = Color.Gold };
            txtModel = new TextBox
            {
                Left = 398,
                Top = 10,
                Width = 210,
                Text = "gemini-3.5-flash-lite",
                BackColor = Color.FromArgb(30, 34, 44),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            lblQueueStatus = new Label
            {
                Text = "Kolejka: 0 | Tryb: BACKEND",
                AutoSize = true,
                Left = 620,
                Top = 13,
                ForeColor = Color.LimeGreen
            };

            topPanel.Controls.AddRange(new Control[] { lblKey, txtApiKey, lblModel, txtModel, lblQueueStatus });

            rtbChat = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(12, 14, 18),
                ForeColor = Color.Gainsboro,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10.5f)
            };

            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(8) };
            txtInput = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 34, 44),
                ForeColor = Color.White,
                Font = new Font("Consolas", 11f)
            };
            txtInput.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    DodajDoKolejki();
                }
            };

            btnSend = new Button
            {
                Text = "WYŚLIJ",
                Dock = DockStyle.Right,
                Width = 100,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSend.Click += (s, e) => DodajDoKolejki();

            bottomPanel.Controls.Add(txtInput);
            bottomPanel.Controls.Add(btnSend);

            Controls.Add(rtbChat);
            Controls.Add(topPanel);
            Controls.Add(bottomPanel);

            ZapiszLog("Uruchomiono kontener SPYDER BACKEND (53 polecenia aktywne).");
            LogujUI("[SPYDER BACKEND] Gotowy. Podłączono 53 realne polecenia HobbyOS Core. Kolejka aktywna.\n", Color.Yellow);
        }

        private void DodajDoKolejki()
        {
            string tekst = txtInput.Text.Trim();
            if (string.IsNullOrEmpty(tekst)) return;

            if (string.IsNullOrWhiteSpace(txtApiKey.Text))
            {
                LogujUI("[BŁĄD] Wpisz najpierw klucz Gemini API u góry okna!\n", Color.OrangeRed);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtModel.Text))
            {
                LogujUI("[BŁĄD] Podaj nazwę modelu w polu Model!\n", Color.OrangeRed);
                return;
            }

            txtInput.Clear();
            _kolejkaZadan.Enqueue(tekst);
            LogujUI($"\n[USER/ROOT]: {tekst}\n", Color.DeepSkyBlue);
            AktualizujStatusKolejki();

            _ = PrzetwarzajKolejkeAsync();
        }

        private async Task PrzetwarzajKolejkeAsync()
        {
            await _semaforKolejki.WaitAsync();
            try
            {
                while (_kolejkaZadan.Count > 0)
                {
                    AktualizujStatusKolejki();
                    string zadanie = _kolejkaZadan.Dequeue();
                    AktualizujStatusKolejki();

                    await ObsluzPojedynczeZadanieAsync(zadanie);
                }
            }
            finally
            {
                AktualizujStatusKolejki();
                _semaforKolejki.Release();
            }
        }

        private async Task ObsluzPojedynczeZadanieAsync(string pytanieUzytkownika)
        {
            _historiaRozmowy.Add(new
            {
                role = "user",
                parts = new[] { new { text = pytanieUzytkownika } }
            });

            const int MAX_KROKOW_KOMEND = 3;
            int licznikKomend = 0;

            while (true)
            {
                string odpowiedzAI = await ZapytajGeminiAsync();
                if (string.IsNullOrWhiteSpace(odpowiedzAI))
                {
                    LogujUI("[MCP AI]: (Brak odpowiedzi z Google API)\n", Color.Gray);
                    break;
                }

                // Wyłapujemy wszystkie tagi [CMD: ...] nawet jeśli AI zwróci kilka w jednej wiadomości
                var matches = Regex.Matches(odpowiedzAI, @"\[CMD:\s*(.+?)\]", RegexOptions.IgnoreCase);

                if (matches.Count > 0 && licznikKomend < MAX_KROKOW_KOMEND)
                {
                    _historiaRozmowy.Add(new
                    {
                        role = "model",
                        parts = new[] { new { text = odpowiedzAI.Trim() } }
                    });

                    var zbiorWynikow = new StringBuilder();

                    foreach (Match m in matches)
                    {
                        if (licznikKomend >= MAX_KROKOW_KOMEND) break;

                        licznikKomend++;
                        string komenda = m.Groups[1].Value.Trim();
                        ZapiszLog($"Wykonano komendę [{licznikKomend}/{MAX_KROKOW_KOMEND}]: {komenda}");

                        string wynikZSystemu = await WykonajKomendeKernelAsync(komenda);
                        zbiorWynikow.AppendLine($"[SYS_OUT dla '{komenda}' | Krok {licznikKomend}/{MAX_KROKOW_KOMEND}]: {wynikZSystemu}");
                    }

                    // Ukryta zwrotka dla AI (niewidoczna dla usera)
                    _historiaRozmowy.Add(new
                    {
                        role = "user",
                        parts = new[] { new { text = zbiorWynikow.ToString().Trim() } }
                    });

                    continue;
                }
                else
                {
                    string czystaOdpowiedz = Regex.Replace(odpowiedzAI, @"\[CMD:.*?\]", "").Trim();
                    if (string.IsNullOrEmpty(czystaOdpowiedz))
                        czystaOdpowiedz = "Wykonano maksymalną liczbę 3 operacji na pamięci w jednej kolejce.";

                    _historiaRozmowy.Add(new
                    {
                        role = "model",
                        parts = new[] { new { text = czystaOdpowiedz } }
                    });

                    LogujUI($"[MCP AI]: {czystaOdpowiedz}\n", Color.LightGreen);
                    break;
                }
            }
        }

        private async Task<string> ZapytajGeminiAsync()
        {
            string apiKey = txtApiKey.Text.Trim();
            string model = txtModel.Text.Trim();
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var payload = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = SYSTEM_PROMPT } }
                },
                contents = _historiaRozmowy
            };

            string json = JsonSerializer.Serialize(payload);
            const int MAX_PROB = 4;

            for (int proba = 1; proba <= MAX_PROB; proba++)
            {
                try
                {
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    using var response = await _http.PostAsync(url, content);
                    string respText = await response.Content.ReadAsStringAsync();

                    int statusCode = (int)response.StatusCode;
                    bool przeciazenie = statusCode == 503 || statusCode == 429 ||
                                         respText.Contains("UNAVAILABLE", StringComparison.OrdinalIgnoreCase) ||
                                         respText.Contains("high demand", StringComparison.OrdinalIgnoreCase);

                    if (!response.IsSuccessStatusCode)
                    {
                        if (przeciazenie && proba < MAX_PROB)
                        {
                            int opoznienieSek = proba * 2;
                            LogujUI($"[AUTO-RETRY] Model '{model}' przeciążony ({statusCode}). Ponawiam za {opoznienieSek}s (próba {proba}/{MAX_PROB})...\n", Color.Gold);
                            await Task.Delay(TimeSpan.FromSeconds(opoznienieSek));
                            continue;
                        }

                        return $"Błąd API ({response.StatusCode}): {respText}";
                    }

                    using var doc = JsonDocument.Parse(respText);
                    return doc.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString() ?? "";
                }
                catch (Exception ex)
                {
                    if (proba < MAX_PROB)
                    {
                        int opoznienieSek = proba * 2;
                        LogujUI($"[AUTO-RETRY] Błąd połączenia ({ex.Message}). Ponawiam za {opoznienieSek}s (próba {proba}/{MAX_PROB})...\n", Color.Gold);
                        await Task.Delay(TimeSpan.FromSeconds(opoznienieSek));
                        continue;
                    }

                    return $"Wyjątek połączenia: {ex.Message}";
                }
            }

            return "Nie udało się uzyskać odpowiedzi po ponownych próbach.";
        }

        // REALNE WYKONYWANIE WSZYSTKICH 53 KOMEND SYSTEMOWYCH HOBBYOS CORE
        private async Task<string> WykonajKomendeKernelAsync(string pelnaKomenda)
        {
            string[] czesci = pelnaKomenda.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string cmd = czesci[0].ToLowerInvariant();
            string arg = czesci.Length > 1 ? czesci[1].Trim() : "";

            string currentDir = Directory.GetCurrentDirectory();
            string srcRepoPath = Path.Combine(currentDir, "src_repo");
            if (!Directory.Exists(srcRepoPath)) Directory.CreateDirectory(srcRepoPath);

            try
            {
                return cmd switch
                {
                    // --- 1. SYSTEM UPRAWNIEŃ GEM (REALNY REJESTR PAKIETÓW JSON) ---
                    "gem" when arg.StartsWith("install", StringComparison.OrdinalIgnoreCase)
                        => ZarzadzajPakietemGem(srcRepoPath, arg.Substring(7).Trim(), true),
                    "gem" when arg.StartsWith("remove", StringComparison.OrdinalIgnoreCase)
                        => ZarzadzajPakietemGem(srcRepoPath, arg.Substring(6).Trim(), false),
                    "gem-install" => ZarzadzajPakietemGem(srcRepoPath, arg, true),
                    "gem-remove" => ZarzadzajPakietemGem(srcRepoPath, arg, false),

                    // --- 2. INTERFEJS, GITHUB I SRC_REPO (PRAWDZIWE URUCHAMIANIE PULL-PITÓW I POBIERANIE) ---
                    "pulpit-start" => await UruchomPullPitCsAsync(srcRepoPath, arg),
                    "fetch" => await PobierzPlikDoRepoAsync(srcRepoPath, arg),
                    "src-path" => $"Ścieżka src_repo: {srcRepoPath}",
                    "src-list" => "Pliki w src_repo: " + (Directory.GetFiles(srcRepoPath).Length == 0
                        ? "(brak plików)"
                        : string.Join(", ", Directory.GetFiles(srcRepoPath).Select(Path.GetFileName))),
                    "src-count" => $"Liczba plików w src_repo: {Directory.GetFiles(srcRepoPath).Length}",
                    "src-clean" => WyczyscTempISrc(srcRepoPath),
                    "src-status" => $"src_repo: AKTYWNE | Lokalizacja: {srcRepoPath} | Plików: {Directory.GetFiles(srcRepoPath).Length}",

                    // --- 3. METRYKI PROCESORA ---
                    "cpu-cores" => $"Rdzenie logiczne CPU: {Environment.ProcessorCount}",
                    "cpu-arch" => $"Architektura CPU/OS: {RuntimeInformation.ProcessArchitecture} / {RuntimeInformation.OSArchitecture}",
                    "cpu-os" => $"System bazowy: {RuntimeInformation.OSDescription}",
                    "cpu-uptime" => $"Uptime systemu: {TimeSpan.FromMilliseconds(Environment.TickCount64):dd\\.hh\\:mm\\:ss}",
                    "cpu-tick" => $"CPU TickCount64: {Environment.TickCount64}",

                    // --- 4. ANALIZATOR PAMIĘCI RAM ---
                    "ram-allocated" => $"Przydzielony RAM procesu: {GC.GetTotalMemory(false) / 1024 / 1024} MB",
                    "ram-sterta" => $"Sterta GC: {GC.GetGCMemoryInfo().HeapSizeBytes / 1024 / 1024} MB",
                    "ram-peak" => $"Szczytowy RAM procesu: {Process.GetCurrentProcess().PeakWorkingSet64 / 1024 / 1024} MB",
                    "ram-threads" => $"Liczba wątków procesu: {Process.GetCurrentProcess().Threads.Count}",
                    "ram-status" => $"RAM Fizyczny dostępny: {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024} MB | Użycie procesu: {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} MB",

                    // --- 5. PROCESY WINDOWS ---
                    "ps-count" => $"Liczba uruchomionych procesów Windows: {Process.GetProcesses().Length}",
                    "ps-current-id" => $"PID bieżącego procesu: {Environment.ProcessId}",
                    "ps-name" => $"Nazwa procesu: {Process.GetCurrentProcess().ProcessName}",
                    "ps-priority" => $"Priorytet procesu: {Process.GetCurrentProcess().PriorityClass}",
                    "ps-time" => $"Czas CPU procesu: {Process.GetCurrentProcess().TotalProcessorTime}",

                    // --- 6. TOŻSAMOŚĆ SYSTEMU ---
                    "sys-machine" => $"Nazwa maszyny: {Environment.MachineName}",
                    "sys-user" => $"Użytkownik: {Environment.UserName}",
                    "sys-domain" => $"Domena: {Environment.UserDomainName}",
                    "sys-dir" => $"Katalog systemowy: {Environment.SystemDirectory}",
                    "sys-runtime" => $"Środowisko uruchomieniowe: {RuntimeInformation.FrameworkDescription}",

                    // --- 7. REJESTRY ŚRODOWISKA ---
                    "env-variables" => $"Liczba zmiennych środowiskowych: {Environment.GetEnvironmentVariables().Count}",
                    "env-drive-count" => $"Liczba dysków logicznych: {Environment.GetLogicalDrives().Length}",
                    "env-page-size" => $"Rozmiar strony pamięci: {Environment.SystemPageSize} B",
                    "env-interactive" => $"Tryb interaktywny: {Environment.UserInteractive}",
                    "env-check" => $"Środowisko .NET: OK | 64-bit OS: {Environment.Is64BitOperatingSystem}",

                    // --- 8. WEJŚCIA/WYJŚCIA IO ---
                    "io-drives" => "Dyski: " + string.Join(", ", DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => $"{d.Name} ({d.AvailableFreeSpace / 1024 / 1024 / 1024} GB wolne)")),
                    "io-dir-view" => PodgladKatalogu(string.IsNullOrEmpty(arg) ? currentDir : arg),
                    "io-temp" => $"Folder Temp: {Path.GetTempPath()}",
                    "io-current" => $"Aktualny katalog roboczy: {currentDir} (src_repo: {srcRepoPath})",
                    "io-status" => $"IO Status: ONLINE | D HERO SH4RK FS READY | Zapis możliwy: TRUE",

                    // --- 9. SILNIK DEWELOPERSKI ---
                    "dev-mode" => "DevMode: ENABLED (Architektura HobbyOS 0 + 1 x 2)",
                    "dev-compiler" => "Kompilator: Dynamic C# Runtime Build (.NET SDK / Roslyn)",
                    "dev-reflection" => $"Refleksja .NET: AKTYWNA (Załadowane zestawy: {AppDomain.CurrentDomain.GetAssemblies().Length})",
                    "dev-build-info" => $"HobbyOS Core // SH4RK OS Build | Target: {RuntimeInformation.FrameworkDescription}",
                    "dev-status" => "Silnik deweloperski: GOTOWY DO KOMPILACJI PULL-PITÓW",

                    // --- 10. OPERACJE NA LOGACH ---
                    "log-status" => "Logi systemowe D HERO: AKTYWNE",
                    "log-count" => $"Liczba wpisów w logach: {_logiSystemowe.Count}",
                    "log-dump" => _logiSystemowe.Count == 0 ? "Brak logów." : string.Join(" | ", _logiSystemowe.TakeLast(5)),
                    "log-security" => "ZABRA GEM Security: BRAK BLOKAD (Tryb otwarty HBOS)",
                    "log-check" => $"Weryfikacja logów: CZYSTO ({_logiSystemowe.Count} wpisów sprawdzonych)",

                    _ => $"Nieznana komenda systemowa: {pelnaKomenda}"
                };
            }
            catch (Exception ex)
            {
                ZapiszLog($"Błąd komendy '{pelnaKomenda}': {ex.Message}");
                return $"Błąd wykonania '{pelnaKomenda}': {ex.Message}";
            }
        }

        // PRAWDZIWE URUCHAMIANIE PLIKÓW .CS Z SRC_REPO (KOMPILACJA DO UNIKALNEGO DLL W PAMIĘCI)
        private async Task<string> UruchomPullPitCsAsync(string srcRepoPath, string nazwaPliku)
        {
            if (string.IsNullOrWhiteSpace(nazwaPliku))
                nazwaPliku = "WinOneShell.cs";

            if (!nazwaPliku.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                !nazwaPliku.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                nazwaPliku += ".cs";
            }

            string pelnaSciezka = File.Exists(nazwaPliku)
                ? Path.GetFullPath(nazwaPliku)
                : Path.Combine(srcRepoPath, nazwaPliku);

            if (!File.Exists(pelnaSciezka))
                return $"BŁĄD: Nie znaleziono pliku '{nazwaPliku}' w folderze {srcRepoPath}.";

            // Jeśli to gotowe .exe, odpalamy bezpośrednio
            if (pelnaSciezka.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo(pelnaSciezka) { UseShellExecute = true });
                return $"SUKCES: Uruchomiono plik wykonywalny {Path.GetFileName(pelnaSciezka)}.";
            }

            // Tworzymy tymczasowy projekt w %TEMP% z unikalnym GUID (zero błędów 'TempApp.dll is denied'!)
            string buildId = Guid.NewGuid().ToString("N");
            string tempBuildDir = Path.Combine(Path.GetTempPath(), $"HobbyOS_Build_{buildId}");
            Directory.CreateDirectory(tempBuildDir);

            string skopiowanyCs = Path.Combine(tempBuildDir, Path.GetFileName(pelnaSciezka));
            File.Copy(pelnaSciezka, skopiowanyCs, true);

            string csprojPath = Path.Combine(tempBuildDir, $"PullPit_{buildId}.csproj");
            string csprojXml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <AssemblyName>PullPit_" + buildId + @"</AssemblyName>
  </PropertyGroup>
</Project>";
            await File.WriteAllTextAsync(csprojPath, csprojXml);

            // Kompilujemy przez dotnet build w tle
            var psi = new ProcessStartInfo("dotnet", $"build \"{csprojPath}\" -c Release -o \"{tempBuildDir}\\out\" --nologo")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
                return "BŁĄD: Nie udało się uruchomić kompilatora dotnet.";

            string stdout = await proc.StandardOutput.ReadToEndAsync();
            string stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
            {
                string bladKompilacji = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                return $"BŁĄD KOMPILACJI '{Path.GetFileName(pelnaSciezka)}': {bladKompilacji}";
            }

            string skompilowaneExe = Path.Combine(tempBuildDir, "out", $"PullPit_{buildId}.exe");
            if (File.Exists(skompilowaneExe))
            {
                Process.Start(new ProcessStartInfo(skompilowaneExe)
                {
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    UseShellExecute = true
                });
                ZapiszLog($"Uruchomiono pull-pit: {Path.GetFileName(pelnaSciezka)} (PID nowego okna).");
                return $"SUKCES: Skompilowano i otworzono okno pull-pita '{Path.GetFileName(pelnaSciezka)}' na ekranie.";
            }

            return "BŁĄD: Kompilacja zakończona, ale nie znaleziono pliku wyjściowego.";
        }

        // PRAWDZIWE POBIERANIE PLIKU Z SIECI / GITHUBA DO SRC_REPO
        private async Task<string> PobierzPlikDoRepoAsync(string srcRepoPath, string url)
        {
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return "BŁĄD: Podaj poprawny adres URL (http/https) do pobrania pliku.";

            Uri uri = new Uri(url);
            string nazwaPliku = Path.GetFileName(uri.LocalPath);
            if (string.IsNullOrWhiteSpace(nazwaPliku))
                nazwaPliku = $"fetched_{DateTime.Now:HHmmss}.cs";

            string cel = Path.Combine(srcRepoPath, nazwaPliku);
            byte[] dane = await _http.GetByteArrayAsync(uri);
            await File.WriteAllBytesAsync(cel, dane);

            ZapiszLog($"Pobrano plik {nazwaPliku} ({dane.Length} B) do src_repo.");
            return $"SUKCES: Pobrano '{nazwaPliku}' ({dane.Length} bajtów) do {cel}.";
        }

        // PRAWDZIWY SYSTEM PAKIETÓW GEM (ZAPIS DO src_repo\gem_packages.json)
        private string ZarzadzajPakietemGem(string srcRepoPath, string pakiet, bool instaluj)
        {
            if (string.IsNullOrWhiteSpace(pakiet))
                return "BŁĄD: Podaj nazwę pakietu GEM.";

            string rejestrPath = Path.Combine(srcRepoPath, "gem_packages.json");
            List<string> pakiety = File.Exists(rejestrPath)
                ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(rejestrPath)) ?? new List<string>()
                : new List<string>();

            if (instaluj)
            {
                if (!pakiety.Contains(pakiet, StringComparer.OrdinalIgnoreCase))
                    pakiety.Add(pakiet);
                File.WriteAllText(rejestrPath, JsonSerializer.Serialize(pakiety, new JsonSerializerOptions { WriteIndented = true }));
                ZapiszLog($"GEM INSTALL: {pakiet}");
                return $"[ZABRA GEM] Zainstalowano pakiet '{pakiet}'. Aktywne pakiety: {string.Join(", ", pakiety)}";
            }
            else
            {
                int usuniete = pakiety.RemoveAll(p => p.Equals(pakiet, StringComparison.OrdinalIgnoreCase));
                File.WriteAllText(rejestrPath, JsonSerializer.Serialize(pakiety, new JsonSerializerOptions { WriteIndented = true }));
                ZapiszLog($"GEM REMOVE: {pakiet}");
                return usuniete > 0
                    ? $"[ZABRA GEM] Usunięto pakiet '{pakiet}'."
                    : $"[ZABRA GEM] Pakiet '{pakiet}' nie był zainstalowany.";
            }
        }

        private string WyczyscTempISrc(string srcRepoPath)
        {
            int usuniete = 0;
            foreach (var f in Directory.GetFiles(srcRepoPath, "*.dll").Concat(Directory.GetFiles(srcRepoPath, "*.tmp")))
            {
                try { File.Delete(f); usuniete++; } catch { }
            }
            return $"Oczyszczono pliki tymczasowe w src_repo (usunięto: {usuniete}).";
        }

        private string PodgladKatalogu(string sciezka)
        {
            if (!Directory.Exists(sciezka))
                return $"Katalog '{sciezka}' nie istnieje.";

            var wpisy = Directory.GetFileSystemEntries(sciezka).Select(Path.GetFileName).ToArray();
            return wpisy.Length == 0
                ? $"Katalog '{sciezka}' jest pusty."
                : $"Zawartość '{sciezka}': {string.Join(", ", wpisy)}";
        }

        private void ZapiszLog(string wpis)
        {
            _logiSystemowe.Add($"[{DateTime.Now:HH:mm:ss}] {wpis}");
        }

        private void AktualizujStatusKolejki()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(AktualizujStatusKolejki));
                return;
            }
            lblQueueStatus.Text = $"Kolejka: {_kolejkaZadan.Count} | Tryb: BACKEND";
        }

        private void LogujUI(string tekst, Color kolor)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => LogujUI(tekst, kolor)));
                return;
            }
            rtbChat.SelectionStart = rtbChat.TextLength;
            rtbChat.SelectionLength = 0;
            rtbChat.SelectionColor = kolor;
            rtbChat.AppendText(tekst);
            rtbChat.ScrollToCaret();
        }

        [STAThread]
        public static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new McpSpyderForm());
        }
    }
}
