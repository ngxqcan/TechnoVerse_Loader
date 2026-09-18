using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TechnoVerseLoader.Config;
using TechnoVerseLoader.Services;

namespace TechnoVerseLoader.UI
{
    public class MainForm : Form
    {
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse
        );

        private readonly AppConfig _config;
        private readonly ApiService _apiService;
        private readonly DownloadLaunchService _downloadService;

        private string _hwid = "";
        private DiscordAuthResponse? _authData;
        private CancellationTokenSource? _downloadCts;
        private HttpListener? _localHttpListener;

        private System.Windows.Forms.Timer _statusTimer = null!;
        private bool _isCheckingStatus = false;

        private System.Windows.Forms.Timer _countdownTimer = null!;
        private readonly List<(Label label, DateTime expiryDate)> _countdownItems = new();

        private Icon? _appIcon;
        private Bitmap? _appLogo;

        // Containers
        private Panel _pnlTopBar = null!;
        private Panel _pnlContent = null!;

        // View 1: Login
        private Panel _pnlLoginView = null!;
        private Label _lblBrandTitle = null!;
        private Label _lblBrandSub = null!;
        private CyberButton _btnDiscordLogin = null!;
        private Label _lblStatusMsg = null!;

        // View 2: Dashboard
        private Panel _pnlDashboardView = null!;
        private Panel _pnlUserBar = null!;
        private Label _lblUserGreeting = null!;
        private Label _lblUserIdTag = null!;
        private CyberButton _btnLogout = null!;

        private Panel _pnlProductsHeader = null!;
        private Label _lblProductsTitle = null!;
        private CyberButton _btnRefresh = null!;

        private FlowLayoutPanel _flpProducts = null!;

        public MainForm()
        {
            _config = AppConfig.Load();
            _apiService = new ApiService();
            _downloadService = new DownloadLaunchService();

            InitializeWindow();
            BuildUI();
            InitializeTimers();

            Load += OnMainFormLoad;
            FormClosing += (s, e) =>
            {
                _statusTimer.Stop();
                _statusTimer.Dispose();
                _countdownTimer.Stop();
                _countdownTimer.Dispose();
                StopLocalListener();

                if (CheeseHookLoader.IsLoaded)
                {
                    CheeseHookLoader.UnloadModule();
                }
            };
        }

        private void InitializeWindow()
        {
            Text = "TechnoVerse";
            Size = new Size(560, 460);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.BgDark;
            DoubleBuffered = true;

            LoadAppIcon();

            Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 14, 14));
            MouseDown += OnWindowMouseDown;
        }

        private void LoadAppIcon()
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using var stream = asm.GetManifestResourceStream("TechnoVerseLoader.icon.ico");
                if (stream != null)
                {
                    _appIcon = new Icon(stream);
                }
            }
            catch { }

            if (_appIcon == null)
            {
                try
                {
                    string localIco = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                    if (File.Exists(localIco))
                    {
                        _appIcon = new Icon(localIco);
                    }
                }
                catch { }
            }

            if (_appIcon != null)
            {
                Icon = _appIcon;
                _appLogo = _appIcon.ToBitmap();
            }
        }

        private void InitializeTimers()
        {
            _statusTimer = new System.Windows.Forms.Timer { Interval = 10000 };
            _statusTimer.Tick += async (s, e) => await CheckBackendStatusAsync();

            _countdownTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _countdownTimer.Tick += (s, e) => UpdateCountdownDisplay();
        }

        private void BuildUI()
        {
            // Title bar (Fixed 560x42)
            _pnlTopBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(560, 42),
                BackColor = Color.FromArgb(14, 11, 26)
            };
            _pnlTopBar.MouseDown += OnWindowMouseDown;

            if (_appLogo != null)
            {
                var picTopLogo = new PictureBox
                {
                    Size = new Size(20, 20),
                    Location = new Point(14, 11),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = _appLogo,
                    BackColor = Color.Transparent
                };
                picTopLogo.MouseDown += OnWindowMouseDown;
                _pnlTopBar.Controls.Add(picTopLogo);
            }

            var lblBrand = new Label
            {
                Text = "TechnoVerse",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.CosmicViolet,
                AutoSize = true,
                Location = new Point(_appLogo != null ? 38 : 16, 11)
            };
            lblBrand.MouseDown += OnWindowMouseDown;

            var btnMin = new Button
            {
                Text = "—",
                Size = new Size(34, 28),
                Location = new Point(484, 7),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Theme.TextGray,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnMin.FlatAppearance.BorderSize = 0;
            btnMin.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 35, 75);
            btnMin.Click += (s, e) => WindowState = FormWindowState.Minimized;

            var btnClose = new Button
            {
                Text = "✕",
                Size = new Size(34, 28),
                Location = new Point(520, 7),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Theme.TextGray,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 38, 38);
            btnClose.Click += (s, e) => Application.Exit();

            _pnlTopBar.Controls.AddRange(new Control[] { lblBrand, btnMin, btnClose });

            // Content Panel (Fixed 560x458 below TopBar)
            _pnlContent = new Panel
            {
                Location = new Point(0, 42),
                Size = new Size(560, 418),
                BackColor = Color.Transparent
            };
            _pnlContent.MouseDown += OnWindowMouseDown;

            Controls.Add(_pnlContent);
            Controls.Add(_pnlTopBar);

            BuildLoginView();
            BuildDashboardView();

            ShowView(_pnlLoginView);
        }

        // ==========================================
        // 1. LOGIN VIEW (Clean, Concise, No Emojis)
        // ==========================================
        private void BuildLoginView()
        {
            _pnlLoginView = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };
            _pnlLoginView.MouseDown += OnWindowMouseDown;

            if (_appLogo != null)
            {
                var picLoginLogo = new PictureBox
                {
                    Size = new Size(48, 48),
                    Location = new Point((560 - 48) / 2, 28),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = _appLogo,
                    BackColor = Color.Transparent
                };
                picLoginLogo.MouseDown += OnWindowMouseDown;
                _pnlLoginView.Controls.Add(picLoginLogo);
            }

            _lblBrandTitle = new Label
            {
                Text = "TechnoVerse",
                Font = new Font("Segoe UI", 30F, FontStyle.Bold),
                ForeColor = Theme.TextWhite,
                AutoSize = false,
                Size = new Size(560, 60),
                Location = new Point(0, _appLogo != null ? 82 : 80),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _lblBrandTitle.MouseDown += OnWindowMouseDown;

            _lblBrandSub = new Label
            {
                Text = "Sign in with Discord to continue",
                Font = Theme.SubFont,
                ForeColor = Theme.TextGray,
                AutoSize = false,
                Size = new Size(560, 25),
                Location = new Point(0, 145),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _lblBrandSub.MouseDown += OnWindowMouseDown;

            _btnDiscordLogin = new CyberButton
            {
                Text = "Login with Discord",
                Size = new Size(320, 48),
                Location = new Point(120, 205),
                NormalColor = Theme.Primary,
                HoverColor = Theme.PrimaryHover,
                PressedColor = Theme.PrimaryActive,
                BorderRadius = 8,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.TextWhite
            };
            _btnDiscordLogin.Click += async (s, e) => await StartBrowserDiscordLoginAsync();

            _lblStatusMsg = new Label
            {
                Text = "",
                Font = Theme.SmallFont,
                ForeColor = Theme.TextMuted,
                AutoSize = false,
                Size = new Size(500, 30),
                Location = new Point(30, 275),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _lblStatusMsg.MouseDown += OnWindowMouseDown;

            _pnlLoginView.Controls.AddRange(new Control[] {
                _lblBrandTitle, _lblBrandSub, _btnDiscordLogin, _lblStatusMsg
            });

            _pnlContent.Controls.Add(_pnlLoginView);
        }

        // ==========================================
        // 2. DASHBOARD VIEW (Clean, Concise, No Emojis)
        // ==========================================
        private void BuildDashboardView()
        {
            _pnlDashboardView = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(560, 418),
                BackColor = Color.Transparent,
                Visible = false
            };
            _pnlDashboardView.MouseDown += OnWindowMouseDown;

            // User Bar (Prominent bar right below TopBar)
            _pnlUserBar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(560, 48),
                BackColor = Color.FromArgb(22, 17, 42)
            };
            _pnlUserBar.MouseDown += OnWindowMouseDown;

            _lblUserGreeting = new Label
            {
                Text = "Account: User",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.TextWhite,
                AutoSize = true,
                Location = new Point(16, 7)
            };
            _lblUserGreeting.MouseDown += OnWindowMouseDown;

            _lblUserIdTag = new Label
            {
                Text = "ID: ",
                Font = Theme.SmallFont,
                ForeColor = Theme.TextMuted,
                AutoSize = true,
                Location = new Point(17, 27)
            };
            _lblUserIdTag.MouseDown += OnWindowMouseDown;

            _btnLogout = new CyberButton
            {
                Text = "Logout",
                Size = new Size(80, 26),
                Location = new Point(464, 11),
                NormalColor = Color.FromArgb(38, 22, 38),
                HoverColor = Color.FromArgb(220, 38, 38),
                PressedColor = Color.FromArgb(180, 20, 30),
                BorderLineColor = Color.FromArgb(90, 42, 60),
                BorderRadius = 5,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(244, 130, 150)
            };
            _btnLogout.Click += (s, e) =>
            {
                if (CheeseHookLoader.IsLoaded)
                {
                    CheeseHookLoader.UnloadModule();
                }

                _authData = null;
                _config.SavedDiscordId = "";
                _config.SavedUsername = "";
                _config.Save();
                _lblStatusMsg.Text = "Logged out.";
                _lblStatusMsg.ForeColor = Theme.TextMuted;
                ShowView(_pnlLoginView);
            };

            _pnlUserBar.Controls.AddRange(new Control[] { _lblUserGreeting, _lblUserIdTag, _btnLogout });
            _pnlDashboardView.Controls.Add(_pnlUserBar);

            // Products Header (below UserBar)
            _pnlProductsHeader = new Panel
            {
                Size = new Size(528, 28),
                Location = new Point(16, 56),
                BackColor = Color.Transparent
            };

            _lblProductsTitle = new Label
            {
                Text = "Your Products",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Theme.TextGray,
                AutoSize = true,
                Location = new Point(0, 5)
            };

            _btnRefresh = new CyberButton
            {
                Text = "Refresh",
                Size = new Size(80, 26),
                Location = new Point(448, 1),
                NormalColor = Color.FromArgb(32, 25, 52),
                HoverColor = Theme.Primary,
                PressedColor = Color.FromArgb(109, 40, 217),
                BorderLineColor = Color.FromArgb(70, 52, 105),
                BorderRadius = 5,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(205, 195, 240)
            };
            _btnRefresh.Click += async (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(_config.SavedDiscordId))
                {
                    _btnRefresh.Enabled = false;
                    _btnRefresh.Text = "...";
                    await DoDiscordAuthAsync(_config.SavedDiscordId, true);
                    _btnRefresh.Enabled = true;
                    _btnRefresh.Text = "Refresh";
                }
            };

            _pnlProductsHeader.Controls.AddRange(new Control[] { _lblProductsTitle, _btnRefresh });
            _pnlDashboardView.Controls.Add(_pnlProductsHeader);

            // FlowLayoutPanel products
            _flpProducts = new FlowLayoutPanel
            {
                Size = new Size(528, 360),
                Location = new Point(16, 88),
                AutoScroll = true,
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            _pnlDashboardView.Controls.Add(_flpProducts);

            _pnlContent.Controls.Add(_pnlDashboardView);
        }

        private async void OnMainFormLoad(object? sender, EventArgs e)
        {
            _hwid = HwidService.GetHwid();

            // Tu dong don sach cac file DLL cu con sot lai tu phien truoc
            _ = Task.Run(() =>
            {
                try
                {
                    string cacheDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "TechnoVerse", ".cache");
                    if (Directory.Exists(cacheDir))
                    {
                        foreach (var f in Directory.GetFiles(cacheDir, "*.dll"))
                        {
                            try { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); } catch { }
                        }
                    }
                }
                catch { }
            });

            _countdownTimer.Start();
            await CheckBackendStatusAsync();
            _statusTimer.Start();

            if (!string.IsNullOrWhiteSpace(_config.SavedDiscordId))
            {
                await DoDiscordAuthAsync(_config.SavedDiscordId, true);
            }
        }

        private async Task CheckBackendStatusAsync()
        {
            if (_isCheckingStatus) return;
            _isCheckingStatus = true;

            try
            {
                var (online, pingMs, pingMsg) = await _apiService.PingServerAsync(_config.ServerUrl);

                if (!online)
                {
                    if (_pnlLoginView.Visible)
                    {
                        _lblStatusMsg.Text = "Cannot connect to server. Retrying in 10s...";
                        _lblStatusMsg.ForeColor = Theme.Red;
                    }
                    return;
                }

                if (_pnlDashboardView.Visible && !string.IsNullOrWhiteSpace(_config.SavedDiscordId))
                {
                    if (_downloadCts != null) return;

                    var res = await _apiService.AuthenticateWithDiscordAsync(_config.ServerUrl, _config.SavedDiscordId, _hwid);
                    if (res.Success && res.Products != null)
                    {
                        if (HasProductsChanged(_authData?.Products, res.Products))
                        {
                            _authData = res;
                            DisplayUserProducts(res);
                        }
                    }
                }
            }
            catch
            {
                // Silently handle error during background status ping
            }
            finally
            {
                _isCheckingStatus = false;
            }
        }

        private bool HasProductsChanged(List<PurchasedProductItem>? oldList, List<PurchasedProductItem> newList)
        {
            if (oldList == null) return true;
            if (oldList.Count != newList.Count) return true;

            for (int i = 0; i < newList.Count; i++)
            {
                var n = newList[i];
                var o = oldList.Find(x => x.Key == n.Key);
                if (o == null) return true;
                if (o.Status != n.Status || o.CanActivate != n.CanActivate) return true;
                if (o.Subscription?.ExpiresAt != n.Subscription?.ExpiresAt) return true;
                if ((o.Payload == null) != (n.Payload == null)) return true;
                if (o.Payload?.Sha256 != n.Payload?.Sha256) return true;
                if (o.Payload?.FileName != n.Payload?.FileName) return true;
                if ((o.PayloadOption2 == null) != (n.PayloadOption2 == null)) return true;
                if (o.PayloadOption2?.Sha256 != n.PayloadOption2?.Sha256) return true;
                if (o.PayloadOption2?.FileName != n.PayloadOption2?.FileName) return true;
            }
            return false;
        }

        private void UpdateCountdownDisplay()
        {
            if (_countdownItems.Count == 0) return;

            var now = DateTime.UtcNow;
            foreach (var (lbl, exp) in _countdownItems)
            {
                UpdateSingleCountdown(lbl, exp, now);
            }
        }

        private static void UpdateSingleCountdown(Label lbl, DateTime exp, DateTime? nowUtc = null)
        {
            var now = nowUtc ?? DateTime.UtcNow;
            var remaining = exp - now;

            if (remaining.TotalSeconds <= 0)
            {
                lbl.Text = "Expired";
                lbl.ForeColor = Theme.Red;
            }
            else
            {
                int days = (int)remaining.TotalDays;
                int hours = remaining.Hours;
                int mins = remaining.Minutes;
                int secs = remaining.Seconds;

                lbl.Text = $"Active • Remaining: {days}d {hours:D2}:{mins:D2}:{secs:D2}";
                lbl.ForeColor = Theme.Green;
            }
        }

        private async Task StartBrowserDiscordLoginAsync()
        {
            _btnDiscordLogin.Enabled = false;
            _lblStatusMsg.Text = "Opening browser for authentication...";
            _lblStatusMsg.ForeColor = Theme.Cyan;

            const int port = 39182;
            StopLocalListener();

            try
            {
                _localHttpListener = new HttpListener();
                _localHttpListener.Prefixes.Add($"http://127.0.0.1:{port}/callback/");
                _localHttpListener.Start();

                string oauthUrl = $"https://technoverse-backend-production.up.railway.app/api/loader/discord/oauth?port={port}";
                Process.Start(new ProcessStartInfo(oauthUrl) { UseShellExecute = true });

                _lblStatusMsg.Text = "Please click 'Authorize' in your browser...";

                var context = await _localHttpListener.GetContextAsync();
                var req = context.Request;
                string? discordId = req.QueryString["discordId"];

                byte[] responseBytes = System.Text.Encoding.UTF8.GetBytes(
                    "<!DOCTYPE html><html><head><meta charset='utf-8'><title>TechnoVerse</title>" +
                    "<style>body{background:#090d16;color:#f3f4f6;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;margin:0;}" +
                    ".card{background:#111827;border:1px solid #1f2937;border-radius:10px;padding:32px 40px;text-align:center;box-shadow:0 12px 30px rgba(0,0,0,0.4);max-width:380px;}" +
                    "h2{margin:0 0 10px 0;font-size:18px;font-weight:700;color:#f9fafb;letter-spacing:0.5px;}" +
                    "p{margin:0;font-size:13px;color:#9ca3af;line-height:1.5;}</style></head>" +
                    "<body><div class='card'><h2>Xác thực thành công</h2><p>Bạn có thể đóng tab này và quay lại ứng dụng.</p></div></body></html>"
                );
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                context.Response.OutputStream.Close();

                StopLocalListener();

                if (!string.IsNullOrWhiteSpace(discordId))
                {
                    Invoke((MethodInvoker)(async () => await DoDiscordAuthAsync(discordId)));
                }
                else
                {
                    _lblStatusMsg.Text = "Authentication failed.";
                    _lblStatusMsg.ForeColor = Theme.Red;
                }
            }
            catch (Exception ex)
            {
                _lblStatusMsg.Text = "Error: " + ex.Message;
                _lblStatusMsg.ForeColor = Theme.Red;
            }
            finally
            {
                _btnDiscordLogin.Enabled = true;
            }
        }

        private void StopLocalListener()
        {
            try
            {
                if (_localHttpListener != null && _localHttpListener.IsListening)
                {
                    _localHttpListener.Stop();
                    _localHttpListener.Close();
                }
            }
            catch { }
            _localHttpListener = null;
        }

        private async Task DoDiscordAuthAsync(string discordId, bool isAutoLogin = false)
        {
            _btnDiscordLogin.Enabled = false;

            if (!isAutoLogin)
            {
                _lblStatusMsg.Text = "Synchronizing data...";
                _lblStatusMsg.ForeColor = Theme.CosmicViolet;
            }

            var res = await _apiService.AuthenticateWithDiscordAsync(_config.ServerUrl, discordId, _hwid);
            _btnDiscordLogin.Enabled = true;

            if (!res.Success)
            {
                _lblStatusMsg.Text = res.Error ?? "Authentication failed.";
                _lblStatusMsg.ForeColor = Theme.Red;
                return;
            }

            _config.SavedDiscordId = discordId;
            _config.SavedUsername = res.User?.Username ?? "";
            _config.Save();

            _authData = res;
            DisplayUserProducts(res);
            ShowView(_pnlDashboardView);
        }

        private void DisplayUserProducts(DiscordAuthResponse data)
        {
            string userName = data.User?.Username ?? "User";
            string userId = data.User?.Id ?? _config.SavedDiscordId;
            _lblUserGreeting.Text = $"Account: {userName}";
            _lblUserIdTag.Text = $"ID: {userId}";

            foreach (Control c in _flpProducts.Controls)
            {
                c.Dispose();
            }
            _flpProducts.Controls.Clear();
            _countdownItems.Clear();

            if (data.Products == null || data.Products.Count == 0)
            {
                var pnlEmpty = new CardPanel
                {
                    Size = new Size(508, 100),
                    BackgroundColor = Theme.Surface,
                    BorderColor = Theme.Border,
                    BorderRadius = 8,
                    Margin = new Padding(0, 10, 0, 0)
                };

                var lblEmpty = new Label
                {
                    Text = "No products found.\nPlease open a ticket on Discord to purchase.",
                    Font = Theme.SubFont,
                    ForeColor = Theme.TextGray,
                    AutoSize = false,
                    Size = new Size(480, 60),
                    Location = new Point(14, 20),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                pnlEmpty.Controls.Add(lblEmpty);
                _flpProducts.Controls.Add(pnlEmpty);

                // Empty
                return;
            }

            // Loaded

            foreach (var item in data.Products)
            {
                var p = item.Product;
                if (p == null) continue;

                bool isUnactivated = item.CanActivate || item.Status == "unactivated";

                bool isInternalVip = string.Equals(p.Key, "internal-vip", StringComparison.OrdinalIgnoreCase);
                bool showOptionSelector = isInternalVip && !isUnactivated && item.Status != "hwid_mismatch";

                var card = new CardPanel
                {
                    Size = new Size(508, showOptionSelector ? 90 : 72),
                    BackgroundColor = isUnactivated ? Color.FromArgb(30, 22, 52) : Color.FromArgb(22, 17, 40),
                    BorderColor = isUnactivated ? Theme.BorderActive : Theme.Border,
                    BorderRadius = 8,
                    Margin = new Padding(0, 0, 0, 8)
                };

                // Chỉ hiện tên sản phẩm thuần túy, KHÔNG CÓ EMOJI
                string prodTitle = p.Name.Trim();
                var lblTitle = new Label
                {
                    Text = prodTitle,
                    Font = Theme.HeaderFont,
                    ForeColor = Theme.TextWhite,
                    AutoSize = true,
                    Location = new Point(14, 8)
                };

                string subText = item.Subscription?.IsLifetime == true
                    ? "Lifetime"
                    : (item.Subscription?.PricingLabel ?? "License");

                string maskedKey = MaskLicenseKey(item.Key);
                var lblSub = new Label
                {
                    Text = $"Plan: {subText}   •   Key: {maskedKey}",
                    Font = Theme.SmallFont,
                    ForeColor = Theme.TextGray,
                    AutoSize = true,
                    Location = new Point(14, 28)
                };

                var lblStatusTag = new Label
                {
                    Font = new Font("Segoe UI", 8.2F, FontStyle.Bold),
                    AutoSize = true,
                    Location = new Point(14, 46)
                };

                var curItem = item;

                if (isUnactivated)
                {
                    lblStatusTag.Text = "Chưa kích hoạt (Bấm Activate)";
                    lblStatusTag.ForeColor = Theme.Yellow;

                    var btnAction = new CyberButton
                    {
                        Size = new Size(130, 42),
                        Location = new Point(362, 15),
                        BorderRadius = 7,
                        Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                        Text = "Activate",
                        NormalColor = Theme.BorderActive,
                        HoverColor = Color.FromArgb(147, 51, 234),
                        PressedColor = Color.FromArgb(126, 34, 206)
                    };

                    btnAction.Click += async (s, e) =>
                    {
                        var confirm = MessageBox.Show(
                            $"Bạn có chắc chắn muốn kích hoạt bản quyền {p.Name} ({subText}) cho máy tính này không?\nThời hạn sử dụng sẽ bắt đầu tính ngay sau khi kích hoạt.",
                            "Xác nhận kích hoạt thiết bị",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question
                        );

                        if (confirm == DialogResult.Yes)
                        {
                            btnAction.Enabled = false;
                            btnAction.Text = "Activating...";

                            var (success, msg) = await _apiService.ActivateKeyAsync(
                                _config.ServerUrl, curItem.Key, _config.SavedDiscordId, _hwid
                            );

                            if (success)
                            {
                                MessageBox.Show(msg, "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                await DoDiscordAuthAsync(_config.SavedDiscordId, true);
                            }
                            else
                            {
                                MessageBox.Show(msg, "Lỗi kích hoạt", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                btnAction.Enabled = true;
                                btnAction.Text = "Activate";
                            }
                        }
                    };

                    card.Controls.AddRange(new Control[] { lblTitle, lblSub, lblStatusTag, btnAction });
                }
                else if (item.Status == "hwid_mismatch")
                {
                    lblStatusTag.Text = "Lệch thiết bị (HWID)";
                    lblStatusTag.ForeColor = Theme.Red;

                    var btnAction = new CyberButton
                    {
                        Size = new Size(130, 42),
                        Location = new Point(362, 15),
                        BorderRadius = 7,
                        Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                        Text = "Reset trên Bot",
                        NormalColor = Color.FromArgb(45, 20, 30),
                        HoverColor = Theme.Red,
                        BorderLineColor = Theme.Red
                    };

                    btnAction.Click += (s, e) =>
                    {
                        var confirm = MessageBox.Show(
                            $"Khóa {p.Name} này đang liên kết với một máy tính khác!\n\n" +
                            "Bạn có muốn mở kênh #reset-hwid trên Discord để xóa liên kết và chuyển bản quyền sang máy tính này không?",
                            "Lệch thiết bị (HWID Mismatch)",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning
                        );

                        if (confirm == DialogResult.Yes)
                        {
                            try
                            {
                                string resetUrl = "https://discord.com/channels/1424360310076211274/1547868356684681226";
                                Process.Start(new ProcessStartInfo(resetUrl) { UseShellExecute = true });
                            }
                            catch
                            {
                                Process.Start(new ProcessStartInfo("https://discord.gg/technoverse") { UseShellExecute = true });
                            }
                        }
                    };

                    card.Controls.AddRange(new Control[] { lblTitle, lblSub, lblStatusTag, btnAction });
                }
                else
                {
                    if (item.Subscription?.IsLifetime == true || string.IsNullOrWhiteSpace(item.Subscription?.ExpiresAt))
                    {
                        lblStatusTag.Text = "Active • Lifetime";
                        lblStatusTag.ForeColor = Theme.Green;
                    }
                    else if (DateTime.TryParse(item.Subscription?.ExpiresAt, out var dtExp))
                    {
                        var utcExp = dtExp.ToUniversalTime();
                        _countdownItems.Add((lblStatusTag, utcExp));
                        UpdateSingleCountdown(lblStatusTag, utcExp);
                    }
                    else
                    {
                        lblStatusTag.Text = "Active";
                        lblStatusTag.ForeColor = Theme.Green;
                    }

                    int selectedOption = 1;

                    // Giữ nguyên nút Launch truyền thống ở bên phải
                    var btnAction = new CyberButton
                    {
                        Size = new Size(130, 42),
                        Location = new Point(362, showOptionSelector ? 24 : 15),
                        BorderRadius = 7,
                        Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                        Text = "Launch",
                        NormalColor = Theme.Primary,
                        HoverColor = Theme.PrimaryHover,
                        PressedColor = Theme.PrimaryActive
                    };
                    btnAction.Click += async (s, e) => await HandleLoadPurchasedItemAsync(curItem, btnAction, selectedOption);

                    var cardControls = new List<Control> { lblTitle, lblSub, lblStatusTag, btnAction };

                    // Ô option nhỏ ở góc trái phía dưới
                    if (showOptionSelector)
                    {
                        var cmbOption = new CyberDropdown
                        {
                            Location = new Point(14, 62),
                            Size = new Size(100, 24)
                        };

                        cmbOption.Items.Add("Option 1");
                        cmbOption.Items.Add("Option 2");
                        cmbOption.Items.Add("Option 3");

                        // Tự động load config option đã chọn trước đó
                        int savedOpt = _config.GetProductOption(p.Key, 1);
                        selectedOption = savedOpt;
                        cmbOption.SelectedIndex = Math.Clamp(savedOpt - 1, 0, 2);

                        cmbOption.SelectedIndexChanged += (s, e) =>
                        {
                            selectedOption = cmbOption.SelectedIndex + 1;
                            _config.SetProductOption(p.Key, selectedOption);
                        };

                        cardControls.Add(cmbOption);
                    }

                    card.Controls.AddRange(cardControls.ToArray());
                }

                _flpProducts.Controls.Add(card);
            }
        }

        private static string MaskLicenseKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "N/A";
            string clean = key.Trim();
            if (clean.Length <= 8) return clean;
            return clean.Substring(0, 8) + "-****-" + clean.Substring(clean.Length - 4);
        }

        private static string TruncateName(string name, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            if (name.Length <= maxLen) return name;
            return name.Substring(0, maxLen - 2) + "..";
        }

        private async Task HandleLoadPurchasedItemAsync(PurchasedProductItem item, CyberButton btnLoad, int optionIndex = 1)
        {
            var targetPayload = (optionIndex == 3) ? item.PayloadOption3 : ((optionIndex == 2) ? item.PayloadOption2 : item.Payload);
            string defaultBtnText = "Launch";

            if (targetPayload == null || string.IsNullOrWhiteSpace(targetPayload.DownloadUrl))
            {
                btnLoad.Enabled = false;
                btnLoad.Text = "Checking...";
                try
                {
                    var refreshed = await _apiService.AuthenticateWithDiscordAsync(_config.ServerUrl, _config.SavedDiscordId, _hwid);
                    if (refreshed.Success && refreshed.Products != null)
                    {
                        var match = refreshed.Products.Find(x => x.Key == item.Key);
                        if (match != null)
                        {
                            item = match;
                            _authData = refreshed;
                            targetPayload = (optionIndex == 3) ? item.PayloadOption3 : ((optionIndex == 2) ? item.PayloadOption2 : item.Payload);
                        }
                    }
                }
                catch { }
            }

            if (targetPayload == null || string.IsNullOrWhiteSpace(targetPayload.DownloadUrl))
            {
                btnLoad.Enabled = true;
                btnLoad.Text = defaultBtnText;
                MessageBox.Show(
                    $"No download file available on server for '{item.Product?.Name}' (Option {optionIndex}).\nPlease ensure a payload file is uploaded on the admin panel.",
                    "Notice",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                return;
            }

            bool isReRun = btnLoad.Text.Contains("Running", StringComparison.OrdinalIgnoreCase) ||
                           btnLoad.Text.Contains("Launched", StringComparison.OrdinalIgnoreCase) ||
                           btnLoad.Text.Contains("Restart", StringComparison.OrdinalIgnoreCase) ||
                           btnLoad.Text.Contains("Injected", StringComparison.OrdinalIgnoreCase);

            string prodKey = item.Product?.Key ?? item.Key;
            string downloadKey = prodKey + (optionIndex > 1 ? $"_opt{optionIndex}" : "");
            bool isEmulator = string.Equals(prodKey, "emulator-restart", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(prodKey, "emulator-no-restart", StringComparison.OrdinalIgnoreCase);

            btnLoad.Enabled = false;
            btnLoad.Text = isReRun ? "Restarting..." : "Loading...";

            if (isEmulator)
            {
                // Luôn luôn tắt toàn bộ tiến trình Emulator cũ trước khi tải hoặc cập nhật file
                DownloadLaunchService.KillEmulatorProcesses();
            }

            _downloadCts = new CancellationTokenSource();

            var progress = new Progress<DownloadProgressArgs>(args =>
            {
                if (!isReRun)
                {
                    btnLoad.Text = args.Percentage > 0 ? $"{args.Percentage}%" : "Loading...";
                }
            });

            try
            {
                string expectedSha = targetPayload.Sha256 ?? "";
                string downloadedPath = await _downloadService.DownloadPayloadAsync(
                    targetPayload.DownloadUrl,
                    downloadKey,
                    targetPayload.FileName,
                    expectedSha,
                    progress,
                    _downloadCts.Token,
                    targetPayload.Password
                );

                string ext = Path.GetExtension(downloadedPath).ToLowerInvariant();

                if (ext == ".dll")
                {
                    string cacheDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "TechnoVerse", ".cache");
                    if (!Directory.Exists(cacheDir))
                    {
                        Directory.CreateDirectory(cacheDir);
                    }

                    // Don dep cac file dll cu neu khong bi khoa
                    try
                    {
                        foreach (var oldDll in Directory.GetFiles(cacheDir, "*.dll"))
                        {
                            try
                            {
                                File.SetAttributes(oldDll, FileAttributes.Normal);
                                File.Delete(oldDll);
                            }
                            catch { }
                        }
                    }
                    catch { }

                    // Tao file dll voi ten ngau nhien de khong bao gio bi trung hoac loi Access Denied do file cu bi lock
                    string cheesePath = Path.Combine(cacheDir, $"cv_{Guid.NewGuid():N}.dll");
                    File.Copy(downloadedPath, cheesePath, true);

                    try { File.Delete(downloadedPath); } catch { }

                    btnLoad.Text = "Injecting...";
                    await Task.Delay(300);

                    if (!CheeseHookLoader.IsTargetRunning())
                    {
                        try { File.Delete(cheesePath); } catch { }
                        MessageBox.Show(
                            "VALORANT is not running. Please start the game first.",
                            "Hook Injection",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning
                        );
                        return;
                    }

                    var result = CheeseHookLoader.LoadModule(cheesePath);
                    if (!result.Success)
                    {
                        try { File.Delete(cheesePath); } catch { }
                        MessageBox.Show(result.Message, "Hook Injection Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Tu dong theo doi game: Khi VALORANT tat thi tu dong unload hook va xoa sach file DLL khoi o dia
                    CheeseHookLoader.WatchAndCleanOnExit(cheesePath);

                    btnLoad.Text = "Injected";
                    btnLoad.NormalColor = Theme.Green;
                    btnLoad.HoverColor = Theme.Green;

                    // Tiêu thụ key 1 lần
                    bool isOneTime = IsOneTimePlan(item.Subscription?.PricingLabel);
                    if (isOneTime)
                    {
                        _ = _apiService.ConsumeKeyAsync(_config.ServerUrl, item.Key, _config.SavedDiscordId, _hwid);
                        if (_authData?.Products != null)
                        {
                            _authData.Products.RemoveAll(x => x.Key == item.Key);
                            DisplayUserProducts(_authData);
                        }
                    }
                }
                else
                {
                    btnLoad.Text = isReRun ? "Restarting..." : "Launching...";
                    await Task.Delay(400);

                    _downloadService.LaunchPayload(downloadedPath, prodKey);

                    // Tiêu thụ key 1 lần
                    bool isOneTime = IsOneTimePlan(item.Subscription?.PricingLabel);
                    if (isOneTime)
                    {
                        _ = _apiService.ConsumeKeyAsync(_config.ServerUrl, item.Key, _config.SavedDiscordId, _hwid);
                        if (_authData?.Products != null)
                        {
                            _authData.Products.RemoveAll(x => x.Key == item.Key);
                            DisplayUserProducts(_authData);
                        }
                    }

                    btnLoad.Text = "Running";
                    btnLoad.NormalColor = Theme.Green;
                    btnLoad.HoverColor = Theme.Green;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to launch: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnLoad.Text = defaultBtnText;
                btnLoad.NormalColor = Theme.Primary;
                btnLoad.HoverColor = Theme.PrimaryHover;
            }
            finally
            {
                btnLoad.Enabled = true;
                _downloadCts = null;
            }
        }

        private void ShowView(Panel target)
        {
            _pnlLoginView.Visible = (target == _pnlLoginView);
            _pnlDashboardView.Visible = (target == _pnlDashboardView);
        }

        private void OnWindowMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private static bool IsOneTimePlan(string? label)
        {
            if (string.IsNullOrWhiteSpace(label)) return false;
            string l = label.ToLowerInvariant();
            return l.Contains("1 lần") || l.Contains("1 lan") || l.Contains("mot lan") ||
                   l.Contains("one time") || l.Contains("one-time") || l.Contains("onetime") || l.Contains("1 time");
        }
    }
}
