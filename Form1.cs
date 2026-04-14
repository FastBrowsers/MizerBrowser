using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Mizer
{
    // =====================================================================
    //  LOCALIZATION — все строки в одном месте (твой вариант)
    // =====================================================================
    internal static class Lang
    {
        public const string MenuThemeShop = "Theme Store";
        public const string MenuOpenThemeFile = "Open theme file (.mizer)";
        public const string MenuWhite = "White";
        public const string MenuBlack = "Black";
        public const string MenuCustomBg = "Custom background from PC";
        public const string MenuRemoveBg = "Remove custom background";
        public const string MenuHistory = "History";
        public const string DlgOpenThemeTitle = "Select a MizeR theme file";
        public const string DlgOpenThemeFilter = "Mizer Theme Files|*.mizer";
        public const string DlgOpenBgFilter = "Images|*.jpg;*.jpeg;*.png;*.webp";
        public const string DlgThemeApplied = "Theme applied successfully! ✨";
        public const string DlgThemeError = "Theme format error: ";
        public const string DlgBgError = "Could not download theme background: ";
        public const string DlgLoadError = "Load error: ";
        public const string DlgFileError = "Error: ";
        public const string DlgShortcutName = "Shortcut name:";
        public const string DlgShortcutUrl = "URL:";
        public const string DlgMizerTitle = "MizeR";
        public const string DefaultShortcutName = "Google";
        public const string DefaultShortcutUrl = "https://";
        public const string TabHome = "MizeR";
        public const string TabHistory = "History";
        public const string TabPage = "Page";
        public const string HomeSearchPlaceholder = "Search in Google...";
        public const string HomeAddShortcut = "+ ADD SHORTCUT";
        public const string HistoryTitle = "MizeR History";
        public const string HistoryEmpty = "Nothing here yet...";
        public const string HistoryBtnHome = "\u2190 Home";
        public const string HistoryBtnClear = "Clear all";
        public const string HistoryToday = "Today";
        public const string SessionHome = "MIZER_HOME";
    }

    public partial class Form1 : Form
    {
        private Panel sidebarPanel, titleBar, tabsPanel, contentContainer, navPanel;
        private FlowLayoutPanel tabsContainer;
        private List<WebViewTab> allTabs = new List<WebViewTab>();
        private WebViewTab activeTab;
        private Button btnAddTab, btnExit, btnMax, btnMin, btnPalette, btnBack, btnForward, btnReload, btnHistory;

        public string DataPath => Path.Combine(Application.StartupPath, "MizerData");
        public string SessionFile => Path.Combine(DataPath, "session.txt");
        public string HistoryFile => Path.Combine(DataPath, "history.txt");
        public string ShortcutsFile => Path.Combine(DataPath, "shortcuts.txt");
        public string CustomBgPath => Path.Combine(DataPath, "bg.jpg");

        public Color CurrentThemeColor = Color.FromArgb(30, 30, 30);
        public string CurrentRGB = "30,30,30";
        public bool IsWhiteTheme = false;
        public bool HasCustomBg { get; set; }
        public string CustomBgTextColor { get; set; } = "white";

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        public Form1()
        {
            // 1. Инициализация (важно для работы кнопок и панелей)
            // InitializeComponent();

            // 2. Установка кастомной иконки
            try
            {
                // Пытаемся найти icon.ico в папке с программой
                string iconPath = Path.Combine(Application.StartupPath, "icon.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
            }
            catch { /* Если иконки нет, будет стандартная */ }

            // 3. Базовые настройки окна
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(1200, 750);
            this.BackColor = Color.FromArgb(245, 245, 245);
            this.StartPosition = FormStartPosition.CenterScreen;

            // 4. Работа с папками и файлами
            if (!Directory.Exists(DataPath))
                Directory.CreateDirectory(DataPath);

            if (!File.Exists(ShortcutsFile))
                File.WriteAllText(ShortcutsFile, "Google|https://google.com\nYouTube|https://youtube.com");

            if (!File.Exists(HistoryFile))
                File.WriteAllText(HistoryFile, "");

            HasCustomBg = File.Exists(CustomBgPath);
            if (HasCustomBg) AnalyzeBgBrightness();

            // 5. Запуск интерфейса и сессии
            SetupLayout();
            LoadSavedSession();

            // 6. Подписка на события
            this.SizeChanged += (s, e) => { UpdateRegion(); RecalculateTabWidths(); };
            UpdateRegion();
            this.FormClosing += (s, e) => SaveFullSession();

            this.AllowDrop = true;
            this.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            this.DragDrop += (s, e) =>
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && files[0].EndsWith(".mizer"))
                    ImportThemeFromFile(files[0]);
            };
        }

// ------------------------------------------------------------------
//  Theme — file dialog
// ------------------------------------------------------------------
private void OpenThemeFileDialog()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = Lang.DlgOpenThemeFilter;
                ofd.Title = Lang.DlgOpenThemeTitle;
                if (ofd.ShowDialog() == DialogResult.OK)
                    ImportThemeFromFile(ofd.FileName);
            }
        }

        // ------------------------------------------------------------------
        //  Theme — import from file
        // ------------------------------------------------------------------
        public void ImportThemeFromFile(string filePath)
        {
            try
            {
                string content = File.ReadAllText(filePath).Trim();

                if (content.StartsWith("{"))
                {
                    string imageUrl = GetJsonValue(content, "backgroundImage");
                    string bgColor = GetJsonValue(content, "backgroundColor");

                    if (!string.IsNullOrEmpty(imageUrl))
                        DownloadAndApplyBackground(imageUrl);

                    Color themeColor = ColorTranslator.FromHtml(bgColor);
                    string rgbString = $"{themeColor.R},{themeColor.G},{themeColor.B}";
                    ApplyGlobalTheme(themeColor, rgbString, false);
                }
                else
                {
                    string[] parts = content.Split('|');
                    if (parts.Length >= 2)
                    {
                        string rgb = parts[0];
                        bool whiteText = bool.Parse(parts[1]);
                        string[] c = rgb.Split(',');
                        Color themeColor = Color.FromArgb(int.Parse(c[0]), int.Parse(c[1]), int.Parse(c[2]));

                        if (parts.Length >= 3 && !string.IsNullOrEmpty(parts[2]))
                            DownloadAndApplyBackground(parts[2]);
                        else
                        {
                            if (File.Exists(CustomBgPath)) File.Delete(CustomBgPath);
                            HasCustomBg = false;
                        }
                        ApplyGlobalTheme(themeColor, rgb, whiteText);
                    }
                }

                MessageBox.Show(Lang.DlgThemeApplied, Lang.DlgMizerTitle);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Lang.DlgThemeError + ex.Message);
            }
        }

        private string GetJsonValue(string json, string key)
        {
            try
            {
                int keyIndex = json.IndexOf("\"" + key + "\"");
                if (keyIndex == -1) return "";
                int valStart = json.IndexOf(":", keyIndex) + 1;
                int quoteStart = json.IndexOf("\"", valStart) + 1;
                int quoteEnd = json.IndexOf("\"", quoteStart);
                return json.Substring(quoteStart, quoteEnd - quoteStart);
            }
            catch { return ""; }
        }

        private void DownloadAndApplyBackground(string url)
        {
            try
            {
                using (System.Net.WebClient client = new System.Net.WebClient())
                {
                    client.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) MizerBrowser/1.0");
                    client.DownloadFile(url, CustomBgPath);
                    HasCustomBg = true;
                    AnalyzeBgBrightness();
                    if (activeTab != null) activeTab.LoadHome();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Lang.DlgBgError + ex.Message);
            }
        }

        public async void ImportThemeFromUrl(string url)
        {
            try
            {
                using (System.Net.Http.HttpClient client = new System.Net.Http.HttpClient())
                {
                    string content = await client.GetStringAsync(url);
                    string tempPath = Path.Combine(DataPath, "temp_theme.mizer");
                    File.WriteAllText(tempPath, content);
                    ImportThemeFromFile(tempPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Lang.DlgLoadError + ex.Message);
            }
        }

        private void AnalyzeBgBrightness()
        {
            try
            {
                using (Bitmap bmp = new Bitmap(CustomBgPath))
                {
                    Color c = bmp.GetPixel(bmp.Width / 2, bmp.Height / 2);
                    double brightness = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255;
                    CustomBgTextColor = brightness > 0.5 ? "black" : "white";
                }
            }
            catch { CustomBgTextColor = "white"; }
        }

        // ------------------------------------------------------------------
        //  Layout
        // ------------------------------------------------------------------
        private void SetupLayout()
        {
            titleBar = new Panel();
            titleBar.Height = 45;
            titleBar.Dock = DockStyle.Top;
            titleBar.BackColor = Color.White;
            titleBar.MouseDown += (s, e) => { ReleaseCapture(); SendMessage(Handle, 0xA1, 0x2, 0); };

            btnExit = CreateTitleBtn("✕", Color.Red, (s, e) => { SaveFullSession(); Application.Exit(); }, DockStyle.Right);
            btnMax = CreateTitleBtn("🗖", Color.Black, (s, e) => { this.WindowState = this.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; }, DockStyle.Right);
            btnMin = CreateTitleBtn("—", Color.Black, (s, e) => { this.WindowState = FormWindowState.Minimized; }, DockStyle.Right);
            btnPalette = CreateTitleBtn("🎨", Color.DeepSkyBlue, (s, e) => { ShowThemeMenu(); }, DockStyle.Right);
            btnHistory = CreateTitleBtn("📜", Color.SaddleBrown, (s, e) => { ShowHistoryInTab(); }, DockStyle.Right);

            navPanel = new Panel();
            navPanel.Width = 150;
            navPanel.Dock = DockStyle.Left;

            btnBack = CreateTitleBtn("←", Color.Black, (s, e) => { if (activeTab?.WebView.CanGoBack == true) activeTab.WebView.GoBack(); }, DockStyle.Left);
            btnForward = CreateTitleBtn("→", Color.Black, (s, e) => { if (activeTab?.WebView.CanGoForward == true) activeTab.WebView.GoForward(); }, DockStyle.Left);
            btnReload = CreateTitleBtn("↻", Color.FromArgb(0, 150, 255), (s, e) => { activeTab?.WebView.Reload(); }, DockStyle.Left);

            navPanel.Controls.Add(btnReload);
            navPanel.Controls.Add(btnForward);
            navPanel.Controls.Add(btnBack);

            titleBar.Controls.Add(navPanel);
            titleBar.Controls.Add(btnHistory);
            titleBar.Controls.Add(btnPalette);
            titleBar.Controls.Add(btnMin);
            titleBar.Controls.Add(btnMax);
            titleBar.Controls.Add(btnExit);

            sidebarPanel = new Panel();
            sidebarPanel.Width = 70;
            sidebarPanel.Dock = DockStyle.Left;
            sidebarPanel.BackColor = Color.White;
            sidebarPanel.Padding = new Padding(5, 50, 5, 0);
            sidebarPanel.Controls.Add(CreateSidebarBtn("GH", "https://github.com", Color.Black));
            sidebarPanel.Controls.Add(CreateSidebarBtn("YT", "https://youtube.com", Color.Red));

            tabsPanel = new Panel();
            tabsPanel.Height = 50;
            tabsPanel.Dock = DockStyle.Top;
            tabsPanel.BackColor = Color.FromArgb(230, 230, 230);

            tabsContainer = new FlowLayoutPanel();
            tabsContainer.Dock = DockStyle.Fill;
            tabsContainer.WrapContents = false;
            tabsContainer.AutoScroll = false;
            tabsContainer.Padding = new Padding(10, 5, 0, 0);

            btnAddTab = new Button();
            btnAddTab.Text = "+";
            btnAddTab.Size = new Size(40, 40);
            btnAddTab.Dock = DockStyle.Right;
            btnAddTab.FlatStyle = FlatStyle.Flat;
            btnAddTab.Font = new Font("Arial", 16);
            btnAddTab.FlatAppearance.BorderSize = 0;
            btnAddTab.Click += (s, e) => AddNewTab();

            tabsPanel.Controls.Add(tabsContainer);
            tabsPanel.Controls.Add(btnAddTab);

            contentContainer = new Panel();
            contentContainer.Dock = DockStyle.Fill;
            contentContainer.BackColor = Color.White;

            this.Controls.Add(contentContainer);
            this.Controls.Add(tabsPanel);
            this.Controls.Add(sidebarPanel);
            this.Controls.Add(titleBar);
        }

        public void RecalculateTabWidths()
        {
            if (allTabs.Count == 0) return;
            int availableWidth = tabsContainer.Width - 20;
            int targetWidth = Math.Min(180, Math.Max(50, availableWidth / allTabs.Count));
            foreach (var tab in allTabs)
            {
                tab.HeaderPanel.Width = targetWidth;
                tab.TitleLabel.Width = targetWidth - 40;
                tab.TitleLabel.Visible = targetWidth > 60;
            }
        }

        // ------------------------------------------------------------------
        //  Session
        // ------------------------------------------------------------------
        private void SaveFullSession()
        {
            try
            {
                var lines = allTabs.Select(t =>
                {
                    string u = t.WebView.Source?.ToString();
                    string title = t.TitleLabel.Text;
                    if (string.IsNullOrEmpty(u) || u.StartsWith("data:") || u == "about:blank")
                        u = Lang.SessionHome;
                    return $"{u}|MIZER|{title}";
                }).ToList();
                File.WriteAllLines(SessionFile, lines);
            }
            catch { }
        }

        private async void LoadSavedSession()
        {
            if (File.Exists(SessionFile))
            {
                var lines = File.ReadAllLines(SessionFile).Where(l => l.Contains("|MIZER|")).ToList();
                if (lines.Count > 0)
                {
                    foreach (var line in lines)
                    {
                        var parts = line.Split(new[] { "|MIZER|" }, StringSplitOptions.None);
                        AddNewTab(parts[0] == Lang.SessionHome ? null : parts[0], parts[1]);
                        await Task.Delay(150);
                    }
                    return;
                }
            }
            AddNewTab();
        }

        // ------------------------------------------------------------------
        //  History page
        // ------------------------------------------------------------------
        public void ShowHistoryInTab()
        {
            if (activeTab == null) return;

            string historyContentHtml = "";
            if (File.Exists(HistoryFile))
            {
                var lines = File.ReadAllLines(HistoryFile).ToList();
                string lastDate = "";
                for (int i = lines.Count - 1; i >= 0; i--)
                {
                    var parts = lines[i].Split('|');
                    if (parts.Length >= 4)
                    {
                        string dateOnly = parts[0];
                        string timeOnly = parts[1];
                        string title = parts[2];
                        string url = parts[3];

                        if (dateOnly != lastDate)
                        {
                            string displayDate = (dateOnly == DateTime.Now.ToString("dd.MM.yyyy"))
                                ? Lang.HistoryToday : dateOnly;
                            historyContentHtml += $"<div class='date-divider'>{displayDate}</div>";
                            lastDate = dateOnly;
                        }

                        historyContentHtml +=
                            $@"<div class='history-item'>
                                <div class='content' onclick=""window.location.href='{url}'"">
                                    <div style='display:flex;justify-content:space-between;align-items:center;'>
                                        <span class='title'>{title}</span>
                                        <span class='time'>{timeOnly}</span>
                                    </div>
                                    <div class='url'>{url}</div>
                                </div>
                                <button class='del-btn' onclick=""window.chrome.webview.postMessage('delete_history|{i}')"">✕</button>
                            </div>";
                    }
                }
            }

            string historyPage = $@"<html><head><style>
                body {{ background: rgb({CurrentRGB}); color: {(IsWhiteTheme ? "black" : "white")}; font-family: 'Segoe UI'; padding: 40px; margin: 0; transition: 0.3s; }}
                .container {{ max-width: 900px; margin: 0 auto; }}
                .date-divider {{ margin: 30px 0 15px 5px; font-size: 18px; font-weight: bold; color: #f22; border-bottom: 2px solid rgba(255,34,34,0.3); padding-bottom: 5px; }}
                .history-item {{ background: {(IsWhiteTheme ? "rgba(0,0,0,0.05)" : "rgba(255,255,255,0.07)")}; margin-bottom: 5px; border-radius: 8px; display: flex; align-items: center; transition: 0.2s; }}
                .history-item:hover {{ background: {(IsWhiteTheme ? "rgba(0,0,0,0.08)" : "rgba(255,255,255,0.12)")}; transform: scale(1.01); }}
                .content {{ flex-grow: 1; padding: 10px 20px; cursor: pointer; overflow: hidden; }}
                .title {{ font-weight: bold; font-size: 14px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 70%; }}
                .time {{ font-size: 12px; opacity: 0.5; }}
                .url {{ font-size: 12px; opacity: 0.4; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; margin-top: 3px; }}
                .del-btn {{ background: transparent; color: #666; border: none; padding: 15px; cursor: pointer; font-size: 14px; }}
                .del-btn:hover {{ color: #f22; }}
                .btn-top {{ background: #f22; color: white; border: none; padding: 10px 20px; border-radius: 8px; cursor: pointer; font-weight: bold; margin-right: 10px; }}
            </style></head><body>
            <div class='container'>
                <div style='margin-bottom:40px;'>
                    <button class='btn-top' onclick='window.chrome.webview.postMessage(""go_home"")'>{Lang.HistoryBtnHome}</button>
                    <button class='btn-top' style='background:#444' onclick='window.chrome.webview.postMessage(""clear_history_all"")'>{Lang.HistoryBtnClear}</button>
                </div>
                <h1>📜 {Lang.HistoryTitle}</h1>
                {(string.IsNullOrEmpty(historyContentHtml)
                    ? $"<p style='opacity:0.5'>{Lang.HistoryEmpty}</p>"
                    : historyContentHtml)}
            </div></body></html>";

            activeTab.WebView.NavigateToString(historyPage);
            activeTab.TitleLabel.Text = Lang.TabHistory;
        }

        // ------------------------------------------------------------------
        //  Tabs
        // ------------------------------------------------------------------
        public void AddNewTab(string url = null, string savedTitle = null)
        {
            var tab = new WebViewTab(this, url, savedTitle ?? Lang.TabHome);
            allTabs.Add(tab);
            contentContainer.Controls.Add(tab.WebView);
            tabsContainer.Controls.Add(tab.HeaderPanel);
            SwitchToTab(tab);
            RecalculateTabWidths();
        }

        public void SwitchToTab(WebViewTab target)
        {
            foreach (var t in allTabs)
            {
                t.WebView.Visible = false;
                t.HeaderPanel.BackColor = Color.White;
                t.TitleLabel.ForeColor = Color.Black;
                t.CloseBtn.ForeColor = Color.Black;
            }
            target.WebView.Visible = true;
            target.HeaderPanel.BackColor = CurrentThemeColor;
            if (!IsWhiteTheme)
            {
                target.TitleLabel.ForeColor = Color.White;
                target.CloseBtn.ForeColor = Color.White;
            }
            activeTab = target;
        }

        public void CloseTabAnimated(WebViewTab tab)
        {
            if (allTabs.Count <= 1) return;
            allTabs.Remove(tab);
            tabsContainer.Controls.Remove(tab.HeaderPanel);
            contentContainer.Controls.Remove(tab.WebView);
            tab.WebView.Dispose();
            SwitchToTab(allTabs.Last());
            RecalculateTabWidths();
        }

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------
        private Button CreateTitleBtn(string t, Color c, EventHandler ev, DockStyle d)
        {
            Button b = new Button();
            b.Text = t; b.Size = new Size(40, 45); b.Dock = d; b.FlatStyle = FlatStyle.Flat;
            b.ForeColor = c; b.Font = new Font("Arial", 12, FontStyle.Bold);
            b.FlatAppearance.BorderSize = 0; b.Click += ev; return b;
        }

        private Button CreateSidebarBtn(string t, string u, Color c)
        {
            Button b = new Button();
            b.Text = t; b.Size = new Size(60, 50); b.Dock = DockStyle.Top; b.FlatStyle = FlatStyle.Flat;
            b.ForeColor = c; b.Font = new Font("Arial", 12, FontStyle.Bold);
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => { if (activeTab != null) activeTab.WebView.Source = new Uri(u); };
            return b;
        }

        // ------------------------------------------------------------------
        //  Theme menu
        // ------------------------------------------------------------------
        private void ShowThemeMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            var shopItem = new ToolStripMenuItem("🛒 " + Lang.MenuThemeShop);
            shopItem.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            shopItem.Click += (s, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            { FileName = "https://sites.google.com/view/mizerthemes/", UseShellExecute = true });
            menu.Items.Add(shopItem);

            var openFileItem = new ToolStripMenuItem("📂 " + Lang.MenuOpenThemeFile);
            openFileItem.Click += (s, e) => OpenThemeFileDialog();
            menu.Items.Add(openFileItem);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("⚪ " + Lang.MenuWhite, null, (s, e) => ApplyGlobalTheme(Color.FromArgb(240, 240, 240), "245,245,245", true));
            menu.Items.Add("⚫ " + Lang.MenuBlack, null, (s, e) => ApplyGlobalTheme(Color.FromArgb(30, 30, 30), "30,30,30", false));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("🖼 " + Lang.MenuCustomBg, null, (s, e) => SelectCustomBg());

            if (HasCustomBg)
                menu.Items.Add("❌ " + Lang.MenuRemoveBg, null, (s, e) =>
                {
                    File.Delete(CustomBgPath); HasCustomBg = false;
                    if (activeTab != null) activeTab.LoadHome();
                });

            menu.Show(btnPalette, new Point(0, btnPalette.Height));
        }

        private void SelectCustomBg()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = Lang.DlgOpenBgFilter;
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.Copy(ofd.FileName, CustomBgPath, true);
                        HasCustomBg = true; AnalyzeBgBrightness();
                        if (activeTab != null) activeTab.LoadHome();
                    }
                    catch (Exception ex) { MessageBox.Show(Lang.DlgFileError + ex.Message); }
                }
            }
        }

        private void ApplyGlobalTheme(Color c, string rgb, bool isWhite)
        {
            CurrentThemeColor = c; CurrentRGB = rgb; IsWhiteTheme = isWhite;
            foreach (var tab in allTabs) tab.UpdateThemeUI(rgb, isWhite);
            if (activeTab != null) SwitchToTab(activeTab);
        }

        public void UpdateRegion()
        {
            Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 30, 30));
        }

        // ==================================================================
        //  WebViewTab
        // ==================================================================
        public class WebViewTab
        {
            public Panel HeaderPanel;
            public Label TitleLabel;
            public Button CloseBtn;
            public WebView2 WebView;

            private Form1 parent;
            private string startUrl;
            private string lastLoggedUrl = "";

            public WebViewTab(Form1 mainForm, string url = null, string initialTitle = null)
            {
                parent = mainForm;
                startUrl = url;

                WebView = new WebView2();
                WebView.Dock = DockStyle.Fill;
                WebView.Visible = false;

                HeaderPanel = new Panel();
                HeaderPanel.Size = new Size(180, 40);
                HeaderPanel.Margin = new Padding(3, 5, 3, 0);
                HeaderPanel.BackColor = Color.White;
                HeaderPanel.BorderStyle = BorderStyle.FixedSingle;

                TitleLabel = new Label();
                TitleLabel.Text = initialTitle ?? Lang.TabHome;
                TitleLabel.AutoSize = false;
                TitleLabel.Size = new Size(140, 40);
                TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
                TitleLabel.Padding = new Padding(10, 0, 0, 0);
                TitleLabel.Font = new Font("Segoe UI", 9, FontStyle.Bold);
                TitleLabel.Cursor = Cursors.Hand;
                TitleLabel.Click += (s, e) => parent.SwitchToTab(this);

                CloseBtn = new Button();
                CloseBtn.Text = "×";
                CloseBtn.Size = new Size(30, 40);
                CloseBtn.Dock = DockStyle.Right;
                CloseBtn.FlatStyle = FlatStyle.Flat;
                CloseBtn.Font = new Font("Arial", 12);
                CloseBtn.FlatAppearance.BorderSize = 0;
                CloseBtn.Click += (s, e) => parent.CloseTabAnimated(this);

                HeaderPanel.Controls.Add(TitleLabel);
                HeaderPanel.Controls.Add(CloseBtn);

                Init();
            }

            public void UpdateThemeUI(string rgb, bool isWhite)
            {
                if (WebView?.CoreWebView2 != null)
                {
                    string js = $"if(window.applyTheme) applyTheme('{rgb}', {isWhite.ToString().ToLower()})";
                    WebView.CoreWebView2.ExecuteScriptAsync(js);
                }
            }

            private async void Init()
            {
                var env = await CoreWebView2Environment.CreateAsync(null, parent.DataPath);
                await WebView.EnsureCoreWebView2Async(env);

                WebView.CoreWebView2.NavigationStarting += (s, e) =>
                {
                    if (e.Uri.ToString().ToLower().EndsWith(".mizer"))
                    {
                        e.Cancel = true;
                        parent.ImportThemeFromUrl(e.Uri.ToString());
                    }
                };

                WebView.CoreWebView2.ContainsFullScreenElementChanged += (s, e) =>
                {
                    bool isFull = WebView.CoreWebView2.ContainsFullScreenElement;
                    parent.sidebarPanel.Visible = !isFull;
                    parent.titleBar.Visible = !isFull;
                    parent.tabsPanel.Visible = !isFull;
                };

                if (!string.IsNullOrEmpty(startUrl) && startUrl != Lang.SessionHome)
                    WebView.CoreWebView2.Navigate(startUrl);
                else
                    LoadHome();

                WebView.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    string url = WebView.Source.ToString();
                    string title = WebView.CoreWebView2.DocumentTitle;

                    if (string.IsNullOrEmpty(url) || url.StartsWith("data:") ||
                        url.Contains("about:blank") || url == lastLoggedUrl) return;

                    if (string.IsNullOrEmpty(title) || title == url) title = Lang.TabPage;
                    TitleLabel.Text = title.Length > 15 ? title.Substring(0, 15) + ".." : title;

                    string log = $"{DateTime.Now:dd.MM.yyyy}|{DateTime.Now:HH:mm}|{title}|{url}";
                    File.AppendAllLines(parent.HistoryFile, new[] { log });
                    lastLoggedUrl = url;
                };

                WebView.WebMessageReceived += (s, e) =>
                {
                    string msg = e.TryGetWebMessageAsString();

                    if (msg == "go_home")
                        LoadHome();
                    else if (msg == "create_shortcut")
                    {
                        string name = Interaction.InputBox(Lang.DlgShortcutName, Lang.DlgMizerTitle, Lang.DefaultShortcutName);
                        string url = Interaction.InputBox(Lang.DlgShortcutUrl, Lang.DlgMizerTitle, Lang.DefaultShortcutUrl);
                        if (!string.IsNullOrEmpty(name) && url.Contains("://"))
                        {
                            File.AppendAllLines(parent.ShortcutsFile, new[] { $"{name}|{url}" });
                            LoadHome();
                        }
                    }
                    else if (msg.StartsWith("delete_shortcut|"))
                    {
                        int index = int.Parse(msg.Split('|')[1]);
                        var lines = File.ReadAllLines(parent.ShortcutsFile).ToList();
                        if (index >= 0 && index < lines.Count)
                        {
                            lines.RemoveAt(index);
                            File.WriteAllLines(parent.ShortcutsFile, lines);
                            LoadHome();
                        }
                    }
                    else if (msg == "clear_history_all")
                    {
                        File.WriteAllText(parent.HistoryFile, "");
                        parent.ShowHistoryInTab();
                    }
                    else if (msg.StartsWith("delete_history|"))
                    {
                        try
                        {
                            int index = int.Parse(msg.Split('|')[1]);
                            var lines = File.ReadAllLines(parent.HistoryFile).ToList();
                            if (index >= 0 && index < lines.Count)
                            {
                                lines.RemoveAt(index);
                                File.WriteAllLines(parent.HistoryFile, lines);
                            }
                            parent.ShowHistoryInTab();
                        }
                        catch { }
                    }
                };
            }

            // ------------------------------------------------------------------
            //  Home page
            // ------------------------------------------------------------------
            public void LoadHome()
            {
                TitleLabel.Text = Lang.TabHome;
                string shortcutsHtml = "";

                if (File.Exists(parent.ShortcutsFile))
                {
                    var lines = File.ReadAllLines(parent.ShortcutsFile);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var p = lines[i].Split('|');
                        if (p.Length == 2)
                        {
                            shortcutsHtml += $@"
                                <div class='shortcut'>
                                    <div class='del-btn-home' onclick=""window.chrome.webview.postMessage('delete_shortcut|{i}')"">×</div>
                                    <div class='icon-click' onclick=""window.location.href='{p[1]}'"">
                                        <div class='icon'>{p[0][0].ToString().ToUpper()}</div>
                                        <small>{p[0]}</small>
                                    </div>
                                </div>";
                        }
                    }
                }

                string bgStyle = $"background: rgb({parent.CurrentRGB});";
                if (parent.HasCustomBg && File.Exists(parent.CustomBgPath))
                {
                    try
                    {
                        byte[] imgBytes = File.ReadAllBytes(parent.CustomBgPath);
                        string base64 = Convert.ToBase64String(imgBytes);
                        string ext = Path.GetExtension(parent.CustomBgPath).ToLower().TrimStart('.');
                        if (ext == "jpg") ext = "jpeg";
                        bgStyle = $"background: url('data:image/{ext};base64,{base64}'); background-size: cover; background-position: center;";
                    }
                    catch { }
                }

                string textCol = parent.HasCustomBg
                    ? parent.CustomBgTextColor
                    : (parent.IsWhiteTheme ? "black" : "white");

                string html = $@"<html><head><style>
                    body {{ {bgStyle} font-family: 'Segoe UI'; display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100vh; margin: 0; color: {textCol}; overflow: hidden; transition: 0.3s; }}
                    .logo {{ font-size: 100px; font-weight: bold; color: #f22; text-shadow: 0 0 20px rgba(255,0,0,0.6); font-family: 'Consolas'; user-select: none; transition: 0.2s; cursor: pointer; }}
                    .search-box {{ width: 500px; height: 45px; border-radius: 20px; border: 2px solid #555; padding: 0 20px; font-size: 18px; outline: none; margin: 20px 0; background: rgba(255,255,255,0.95); color: black; }}
                    .grid {{ display: flex; gap: 20px; flex-wrap: wrap; justify-content: center; max-width: 650px; }}
                    .shortcut {{ position: relative; text-align: center; width: 90px; }}
                    .icon-click {{ cursor: pointer; transition: 0.2s; }}
                    .icon-click:hover {{ transform: translateY(-5px); }}
                    .icon {{ width: 60px; height: 60px; border: 2px solid #444; border-radius: 12px; display: flex; align-items: center; justify-content: center; font-size: 25px; background: white; color: black; margin: 0 auto 5px; }}
                    .del-btn-home {{ position: absolute; top: -5px; right: 10px; background: #f22; color: white; border-radius: 50%; width: 20px; height: 20px; font-size: 14px; line-height: 18px; cursor: pointer; display: none; z-index: 10; font-weight: bold; }}
                    .shortcut:hover .del-btn-home {{ display: block; }}
                    .add-btn {{ margin-top: 30px; padding: 10px 20px; border: 1px dashed #f22; border-radius: 10px; cursor: pointer; opacity: 0.7; font-weight: bold; transition: 0.2s; background: rgba(0,0,0,0.1); }}
                    .add-btn:hover {{ opacity: 1; background: rgba(255,0,0,0.1); }}
                </style>
                <script>
                    function applyTheme(rgb, white) {{
                        if (!{parent.HasCustomBg.ToString().ToLower()}) {{
                            document.body.style.background = 'rgb(' + rgb + ')';
                            document.body.style.color = white ? 'black' : 'white';
                        }}
                    }}
                </script>
                </head><body>
                    <div class='logo'
                         onmousedown='this.style.color=""#0078d7""; this.style.textShadow=""0 0 20px rgba(0,120,215,0.6)""; this.style.transform=""scale(1.2)""'
                         onmouseup='this.style.color=""#f22""; this.style.textShadow=""0 0 20px rgba(255,0,0,0.6)""; this.style.transform=""scale(1)""'
                         onmouseleave='this.style.color=""#f22""; this.style.textShadow=""0 0 20px rgba(255,0,0,0.6)""; this.style.transform=""scale(1)""'>MIZER</div>
                    <input type='text' class='search-box' placeholder='{Lang.HomeSearchPlaceholder}' onkeydown='if(event.key==""Enter"") window.location.href=""https://google.com/search?q=""+this.value'>
                    <div class='grid'>{shortcutsHtml}</div>
                    <div class='add-btn' onclick='window.chrome.webview.postMessage(""create_shortcut"")'>{Lang.HomeAddShortcut}</div>
                </body></html>";

                WebView.NavigateToString(html);
            }
        }
    }
}