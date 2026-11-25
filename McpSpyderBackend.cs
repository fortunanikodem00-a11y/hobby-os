using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
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
        private RichTextBox rtbChat;
        private TextBox txtInput;
        private Button btnSend;
        private Label lblQueueStatus;

        private readonly Queue<string> _kolejkaZadan = new Queue<string>();
        private readonly SemaphoreSlim _semaforKolejki = new SemaphoreSlim(1, 1);
        private readonly List<object> _historiaRozmowy = new List<object>();
        private static readonly HttpClient _http = new HttpClient();

        // Prompt uczący Gemini 53 komend, sprawdzania folderu i limitu 1-3 kroków
        private const string SYSTEM_PROMPT = @"
Jesteś agentem MCP AI działającym jako kontener SPYDER (tryb BACKEND) w systemie HobbyOS (SH4RK OS / D HERO SH4RK).
Nie masz na starcie pamięci aplikacji – całą wiedzę o systemie zdobywasz wykonując polecenia.

ZASADY KRYTYCZNE:
1. Gdy chcesz wykonać polecenie systemowe, odpowiedz WYŁĄCZNIE w formacie: [CMD: nazwa_komendy] lub [CMD: nazwa_komendy argument]. Nic więcej w tej wiadomości!
2. Użytkownik NIE WIDZI wiadomości z [CMD: ...] ani odpowiedzi [SYS_OUT]. Po otrzymaniu [SYS_OUT] odpowiedz użytkownikowi naturalnie po polsku.
3. OBOWIĄZEK SPRAWDZANIA FOLDERU: Zawsze, gdy użytkownik pyta o rzeczy grzebiące w pamięci lub plikach (pobieranie, otwieranie, usuwanie, lista plików, status repozytorium, uruchamianie pull-pitów), NAJPIERW w kroku 1 wywołaj [CMD: io-current], aby sprawdzić w jakim folderze się znajdujesz!
4. LIMIT KOLEJKI PAMIĘCI: Przy operacjach grzebiących w pamięci/plikach możesz wykonać maksymalnie od 1 do 3 komend po kolei w jednym zadaniu, po czym musisz udzielić odpowiedzi użytkownikowi.

DOSTĘPNE 53 KOMENDY SYSTEMOWE HOBBYOS CORE:
- SYSTEM UPRAWNIEŃ GEM: gem install, gem remove
- INTERFEJS I GITHUB: pulpit-start, fetch, src-list, src-path, src-count, src-clean, src-status
- METRYKI PROCESORA: cpu-cores, cpu-arch, cpu-os, cpu-uptime, cpu-tick
- ANALIZATOR PAMIĘCI RAM: ram-allocated, ram-sterta, ram-peak, ram-threads, ram-status
- PROCESY WINDOWS: ps-count, ps-current-id, ps-name, ps-priority, ps-time
- TOŻSAMOŚĆ SYSTEMU: sys-machine, sys-user, sys-domain, sys-dir, sys-runtime
- REJESTRY ŚRODOWISKA: env-variables, env-drive-count, env-page-size, env-interactive, env-check
- WEJŚCIA/WYJŚCIA IO (w tym sprawdzanie folderu): io-drives, io-dir-view, io-temp, io-current, io-status
- SILNIK DEWELOPERSKI: dev-mode, dev-compiler, dev-reflection, dev-build-info, dev-status
- OPERACJE NA LOGACH: log-status, log-count, log-dump, log-security, log-check
";

        public McpSpyderForm()
        {
            Text = "HobbyOS // SPYDER MCP AI [BACKEND - D HERO SH4RK]";
            Size = new Size(820, 600);
            BackColor = Color.FromArgb(18, 20, 26);
            ForeColor = Color.White;
            Font = new Font("Consolas", 10f);

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(8) };
            var lblKey = new Label { Text = "Gemini API Key:", AutoSize = true, Left = 10, Top = 12, ForeColor = Color.Cyan };
            txtApiKey = new TextBox
            {
                Left = 140,
                Top = 9,
                Width = 420,
                UseSystemPasswordChar = true,
                BackColor = Color.FromArgb(30, 34, 44),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            lblQueueStatus = new Label
            {
                Text = "Kolejka: 0 | Tryb: BACKEND",
                AutoSize = true,
                Left = 575,
                Top = 12,
                ForeColor = Color.LimeGreen
            };
            topPanel.Controls.AddRange(new Control[] { lblKey, txtApiKey, lblQueueStatus });

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

            LogujUI("[SPYDER BACKEND] Gotowy. Podłączono 53 polecenia HobbyOS Core. Kolejka aktywna.\n", Color.Yellow);
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

            // Maksymalnie 3 kroki komend w ramach jednego zadania (limit 1-3 dla operacji na pamięci/plikach)
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

                // Sprawdzamy, czy AI zwróciło ukrytą komendę [CMD: ...]
                var match = Regex.Match(odpowiedzAI.Trim(), @"^\[CMD:\s*(.+?)\]$", RegexOptions.IgnoreCase);

                if (match.Success && licznikKomend < MAX_KROKOW_KOMEND)
                {
                    licznikKomend++;
                    string komenda = match.Groups[1].Value.Trim();

                    // Zapisujemy wywołanie komendy w historii (niewidoczne dla użytkownika)
                    _historiaRozmowy.Add(new
                    {
                        role = "model",
                        parts = new[] { new { text = odpowiedzAI.Trim() } }
                    });

                    // Backend wykonuje komendę na jądrze D HERO SH4RK
                    string wynikZSystemu = WykonajKomendeKernel(komenda);

                    // Fałszywa / ukryta odpowiedź zwrotna dla AI (użytkownik jej nie widzi)
                    _historiaRozmowy.Add(new
                    {
                        role = "user",
                        parts = new[] { new { text = $"[SYS_OUT dla '{komenda}' | Krok {licznikKomend}/{MAX_KROKOW_KOMEND}]: {wynikZSystemu}" } }
                    });

                    // Pętla idzie dalej – AI analizuje wynik i decyduje czy potrzebuje kolejnej komendy (max 3) czy odpowiada userowi
                    continue;
                }
                else
                {
                    // Jeśli AI chciało zrobić >3 komend, wymuszamy podsumowanie
                    string czystaOdpowiedz = Regex.Replace(odpowiedzAI, @"\[CMD:.*?\]", "").Trim();
                    if (string.IsNullOrEmpty(czystaOdpowiedz))
                        czystaOdpowiedz = "Osiągnięto limit 3 operacji na pamięci w jednej kolejce. Zadanie zatrzymane.";

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
            try
            {
                string apiKey = txtApiKey.Text.Trim();
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

                var payload = new
                {
                    system_instruction = new
                    {
                        parts = new[] { new { text = SYSTEM_PROMPT } }
                    },
                    contents = _historiaRozmowy
                };

                string json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await _http.PostAsync(url, content);
                string respText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return $"Błąd API ({response.StatusCode}): {respText}";

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
                return $"Wyjątek połączenia: {ex.Message}";
            }
        }

        // TUTAJ PODPINASZ SWOJE METODY Z JĄDRA D HERO SH4RK (53 KOMENDY)
        private string WykonajKomendeKernel(string pelnaKomenda)
        {
            string[] czesci = pelnaKomenda.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string cmd = czesci[0].ToLowerInvariant();
            string arg = czesci.Length > 1 ? czesci[1] : "";
            string srcRepoPath = Path.Combine(Directory.GetCurrentDirectory(), "src_repo");

            return cmd switch
            {
                // --- WEJŚCIA/WYJŚCIA IO (w tym sprawdzanie aktualnego folderu) ---
                "io-current" => $"Aktualny katalog roboczy: {Directory.GetCurrentDirectory()}",
                "io-dir-view" => Directory.Exists(string.IsNullOrEmpty(arg) ? Directory.GetCurrentDirectory() : arg)
                    ? "Zawartość: " + string.Join(", ", Directory.GetFileSystemEntries(string.IsNullOrEmpty(arg) ? Directory.GetCurrentDirectory() : arg).Select(Path.GetFileName))
                    : "Katalog nie istnieje.",
                "io-drives" => "Dyski: " + string.Join(", ", DriveInfo.GetDrives().Select(d => $"{d.Name} ({d.DriveType})")),
                "io-temp" => $"Folder Temp: {Path.GetTempPath()}",
                "io-status" => "IO Status: ONLINE (D HERO SH4RK FS READY)",

                // --- ANALIZATOR PAMIĘCI RAM ---
                "ram-allocated" => $"Przydzielony RAM procesu: {GC.GetTotalMemory(false) / 1024 / 1024} MB",
                "ram-sterta" => $"Sterta GC: {GC.GetGCMemoryInfo().HeapSizeBytes / 1024 / 1024} MB",
                "ram-peak" => $"Szczytowy RAM procesu: {Process.GetCurrentProcess().PeakWorkingSet64 / 1024 / 1024} MB",
                "ram-threads" => $"Liczba wątków procesu: {Process.GetCurrentProcess().Threads.Count}",
                "ram-status" => $"RAM Fizyczny dostępny: {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024} MB | Użycie procesu: {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} MB",

                // --- METRYKI PROCESORA ---
                "cpu-cores" => $"Rdzenie logiczne CPU: {Environment.ProcessorCount}",
                "cpu-arch" => $"Architektura: {RuntimeInformation.OSArchitecture}",
                "cpu-os" => $"System bazowy: {RuntimeInformation.OSDescription}",
                "cpu-uptime" => $"Uptime systemu: {TimeSpan.FromMilliseconds(Environment.TickCount64):dd\\.hh\\:mm\\:ss}",
                "cpu-tick" => $"CPU TickCount64: {Environment.TickCount64}",

                // --- INTERFEJS, GITHUB I SRC_REPO ---
                "src-path" => $"Ścieżka src_repo: {srcRepoPath}",
                "src-list" => Directory.Exists(srcRepoPath)
                    ? "Pliki w src_repo: " + string.Join(", ", Directory.GetFiles(srcRepoPath).Select(Path.GetFileName))
                    : "Brak folderu src_repo w bieżącym katalogu.",
                "src-count" => Directory.Exists(srcRepoPath)
                    ? $"Liczba plików w src_repo: {Directory.GetFiles(srcRepoPath).Length}"
                    : "0 (brak folderu src_repo)",
                "src-clean" => $"[SYGNAŁ D HERO] Oczyszczono pamięć podręczną src_repo ({arg})",
                "src-status" => Directory.Exists(srcRepoPath) ? "src_repo: AKTYWNE" : "src_repo: NIEUTWORZONE",
                "pulpit-start" => $"[SYGNAŁ D HERO] Uruchamianie pull-pita: {(string.IsNullOrEmpty(arg) ? "WinOneShell" : arg)}",
                "fetch" => $"[SYGNAŁ D HERO] Pobieranie zasobu z sieci/GitHub: {arg}",

                // --- SYSTEM UPRAWNIEŃ GEM ---
                "gem" when arg.StartsWith("install") => $"[ZABRA GEM] Zainstalowano pakiet: {arg.Replace("install", "").Trim()}",
                "gem" when arg.StartsWith("remove") => $"[ZABRA GEM] Usunięto pakiet: {arg.Replace("remove", "").Trim()}",
                "gem-install" => $"[ZABRA GEM] Zainstalowano pakiet: {arg}",
                "gem-remove" => $"[ZABRA GEM] Usunięto pakiet: {arg}",

                // --- PROCESY WINDOWS ---
                "ps-count" => $"Liczba procesów: {Process.GetProcesses().Length}",
                "ps-current-id" => $"PID Spydera: {Environment.ProcessId}",
                "ps-name" => $"Nazwa procesu: {Process.GetCurrentProcess().ProcessName}",
                "ps-priority" => $"Priorytet: {Process.GetCurrentProcess().PriorityClass}",
                "ps-time" => $"Czas CPU procesu: {Process.GetCurrentProcess().TotalProcessorTime}",

                // --- TOŻSAMOŚĆ SYSTEMU ---
                "sys-machine" => $"Maszyna: {Environment.MachineName}",
                "sys-user" => $"Użytkownik: {Environment.UserName}",
                "sys-domain" => $"Domena: {Environment.UserDomainName}",
                "sys-dir" => $"Katalog systemowy: {Environment.SystemDirectory}",
                "sys-runtime" => $"Runtime: {RuntimeInformation.FrameworkDescription}",

                // --- REJESTRY ŚRODOWISKA ---
                "env-variables" => $"Liczba zmiennych środowiskowych: {Environment.GetEnvironmentVariables().Count}",
                "env-drive-count" => $"Liczba dysków: {Environment.GetLogicalDrives().Length}",
                "env-page-size" => $"Rozmiar strony pamięci: {Environment.SystemPageSize} B",
                "env-interactive" => $"Tryb interaktywny: {Environment.UserInteractive}",
                "env-check" => "Środowisko .NET: OK (HobbyOS Compatible)",

                // --- SILNIK DEWELOPERSKI I LOGI ---
                "dev-mode" => "DevMode: ENABLED (0 + 1 x 2)",
                "dev-compiler" => "Kompilator: Roslyn C# Runtime Compiler",
                "dev-reflection" => "Refleksja .NET: AKTYWNA",
                "dev-build-info" => "HobbyOS Core // SH4RK OS Build",
                "dev-status" => "Silnik deweloperski: GOTOWY",
                "log-status" => "Logi systemowe: AKTYWNE",
                "log-count" => $"Wpisy w historii sesji: {_historiaRozmowy.Count}",
                "log-dump" => "Zrzut logów wykonany do bufora D HERO.",
                "log-security" => "ZABRA GEM Security: BRAK BLOKAD (Tryb otwarty HBOS)",
                "log-check" => "Weryfikacja logów: CZYSTO",

                _ => $"Nieznana komenda systemowa: {pelnaKomenda}"
            };
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
