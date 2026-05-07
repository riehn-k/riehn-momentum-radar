using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static class NativeProgram
{
    [STAThread]
    private static void Main()
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768;

        if (IsEnvironmentFlagEnabled("RIEHN_MOMENTUM_RADAR_NATIVE_TEST", "AKTIENMANAGER_NATIVE_TEST"))
        {
            NativeTest.Run();
            return;
        }

        if (IsEnvironmentFlagEnabled("RIEHN_MOMENTUM_RADAR_NOTIFICATION_TEST", "AKTIENMANAGER_NOTIFICATION_TEST"))
        {
            NativeTest.RunNotificationTest();
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MomentumRadarForm());
    }

    private static bool IsEnvironmentFlagEnabled(string primaryName, string legacyName)
    {
        return Environment.GetEnvironmentVariable(primaryName) == "1"
            || Environment.GetEnvironmentVariable(legacyName) == "1";
    }
}

internal sealed class MomentumRadarForm : Form
{
    private static readonly Color PageBack = Color.FromArgb(243, 246, 250);
    private static readonly Color CardBack = Color.White;
    private static readonly Color CardBackSoft = Color.FromArgb(235, 250, 247);
    private static readonly Color TextMain = Color.FromArgb(25, 32, 43);
    private static readonly Color TextMuted = Color.FromArgb(92, 103, 119);
    private static readonly Color Line = Color.FromArgb(220, 228, 238);
    private static readonly Color Shadow = Color.FromArgb(226, 232, 240);
    private static readonly Color Teal = Color.FromArgb(0, 137, 123);
    private static readonly Color Gold = Color.FromArgb(186, 133, 34);
    private static readonly Color Green = Color.FromArgb(22, 128, 82);

    private readonly Button refreshButton;
    private readonly CheckBox notificationsToggle;
    private readonly Label referenceDateLabel;
    private readonly MaterialDatePicker referenceDatePicker;
    private readonly MaterialLanguageDropdown languageSelector;
    private readonly Label statusLabel;
    private readonly Label titleLabel;
    private readonly DashboardView dashboardView;
    private readonly Panel notificationOverlay;
    private readonly Panel notificationDialog;
    private readonly Label notificationTitle;
    private readonly Label notificationMessage;
    private readonly Button notificationOkButton;
    private readonly NotifyIcon notifyIcon;
    private readonly Timer refreshTimer;
    private List<string> lastTopFive;
    private bool isLoading;
    private bool hasRenderedLiveData;
    private bool changingLanguage;
    private bool scheduledRefreshPending;
    private string languageCode;
    private DateTime lastScheduledRefreshMinute = DateTime.MinValue;

    public MomentumRadarForm()
    {
        languageCode = StateStore.LoadLanguage();
        Text = "Riehn Momentum Radar";
        Width = 1360;
        Height = 1060;
        MinimumSize = new Size(1180, 960);
        BackColor = PageBack;
        ForeColor = TextMain;
        Font = new Font("Segoe UI", 10f);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        lastTopFive = StateStore.LoadTopFive();

        var root = new TableLayoutPanel();
        root.Dock = DockStyle.Fill;
        root.Padding = new Padding(30, 26, 30, 28);
        root.RowCount = 3;
        root.ColumnCount = 1;
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var header = new TableLayoutPanel();
        header.Dock = DockStyle.Fill;
        header.ColumnCount = 2;
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        root.Controls.Add(header, 0, 0);

        titleLabel = new Label();
        titleLabel.Text = "Riehn Momentum Radar";
        titleLabel.Dock = DockStyle.Fill;
        titleLabel.Font = new Font("Segoe UI Semibold", 23.5f, FontStyle.Bold);
        titleLabel.ForeColor = TextMain;
        titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        header.Controls.Add(titleLabel, 0, 0);

        statusLabel = new Label();
        statusLabel.Text = T("Ready");
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.TextAlign = ContentAlignment.MiddleRight;
        statusLabel.ForeColor = TextMuted;
        statusLabel.Font = new Font("Segoe UI", 9.8f);
        statusLabel.AutoEllipsis = true;
        header.Controls.Add(statusLabel, 1, 0);

        var toolbar = new TableLayoutPanel();
        toolbar.Dock = DockStyle.Fill;
        toolbar.Padding = new Padding(0, 9, 0, 9);
        toolbar.ColumnCount = 2;
        toolbar.RowCount = 1;
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176));
        root.Controls.Add(toolbar, 0, 1);

        var leftTools = new FlowLayoutPanel();
        leftTools.Dock = DockStyle.Fill;
        leftTools.FlowDirection = FlowDirection.LeftToRight;
        leftTools.WrapContents = false;
        leftTools.Margin = new Padding(0);
        toolbar.Controls.Add(leftTools, 0, 0);

        refreshButton = new MaterialButton();
        refreshButton.Text = T("Refresh");
        refreshButton.Width = 144;
        refreshButton.Height = 40;
        refreshButton.BackColor = Teal;
        refreshButton.ForeColor = Color.White;
        refreshButton.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        refreshButton.Click += delegate { RefreshData(); };
        leftTools.Controls.Add(refreshButton);

        notificationsToggle = new MaterialToggle();
        notificationsToggle.Text = T("Notifications");
        notificationsToggle.Checked = StateStore.LoadNotificationsEnabled();
        notificationsToggle.AutoSize = false;
        notificationsToggle.Width = 190;
        notificationsToggle.Height = 40;
        notificationsToggle.ForeColor = TextMain;
        notificationsToggle.Font = new Font("Segoe UI", 10f);
        notificationsToggle.CheckedChanged += delegate
        {
            StateStore.SaveNotificationsEnabled(notificationsToggle.Checked);
        };
        leftTools.Controls.Add(notificationsToggle);

        referenceDateLabel = new Label();
        referenceDateLabel.Text = T("ReferenceDate");
        referenceDateLabel.AutoSize = false;
        referenceDateLabel.Width = 72;
        referenceDateLabel.Height = 40;
        referenceDateLabel.Margin = new Padding(18, 0, 0, 0);
        referenceDateLabel.ForeColor = TextMuted;
        referenceDateLabel.TextAlign = ContentAlignment.MiddleLeft;
        referenceDateLabel.Font = new Font("Segoe UI", 9.5f);
        leftTools.Controls.Add(referenceDateLabel);

        referenceDatePicker = new MaterialDatePicker();
        referenceDatePicker.Value = new DateTime(2026, 1, 1);
        referenceDatePicker.MaxDate = DateTime.Today;
        referenceDatePicker.Width = 142;
        referenceDatePicker.Height = 40;
        referenceDatePicker.Margin = new Padding(0, 5, 0, 0);
        referenceDatePicker.Font = new Font("Segoe UI", 10f);
        referenceDatePicker.ValueChanged += delegate
        {
            if (!isLoading)
            {
                RefreshData();
            }
        };
        leftTools.Controls.Add(referenceDatePicker);

        languageSelector = new MaterialLanguageDropdown();
        languageSelector.Width = 158;
        languageSelector.Height = 40;
        languageSelector.Margin = new Padding(0, 3, 0, 0);
        languageSelector.Font = new Font("Segoe UI", 10f);
        languageSelector.Items.Add(new LanguageChoice("de", "Deutsch"));
        languageSelector.Items.Add(new LanguageChoice("en", "English"));
        languageSelector.Items.Add(new LanguageChoice("es", "Español"));
        languageSelector.SelectedIndexChanged += delegate
        {
            if (changingLanguage || languageSelector.SelectedItem == null)
            {
                return;
            }

            languageCode = ((LanguageChoice)languageSelector.SelectedItem).Code;
            StateStore.SaveLanguage(languageCode);
            ApplyLanguage();
        };
        toolbar.Controls.Add(languageSelector, 1, 0);

        dashboardView = new DashboardView();
        dashboardView.LanguageCode = languageCode;
        dashboardView.PortfolioSymbols = StateStore.LoadPortfolioSymbols();
        dashboardView.PortfolioChanged += delegate
        {
            StateStore.SavePortfolioSymbols(dashboardView.PortfolioSymbols);
        };
        dashboardView.Dock = DockStyle.Fill;
        dashboardView.Margin = new Padding(0);
        root.Controls.Add(dashboardView, 0, 2);

        notificationOverlay = new Panel();
        notificationOverlay.Visible = false;
        notificationOverlay.Dock = DockStyle.Fill;
        notificationOverlay.BackColor = Color.FromArgb(232, 238, 246);
        Controls.Add(notificationOverlay);
        notificationOverlay.BringToFront();

        notificationDialog = new Panel();
        notificationDialog.Width = 500;
        notificationDialog.Height = 240;
        notificationDialog.BackColor = Color.White;
        notificationDialog.Padding = new Padding(38, 30, 38, 26);
        notificationDialog.Paint += delegate(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle card = new Rectangle(0, 0, notificationDialog.Width - 1, notificationDialog.Height - 1);
            using (GraphicsPath path = RoundedPath(card, 10))
            using (Pen pen = new Pen(Color.FromArgb(216, 224, 235)))
            {
                e.Graphics.DrawPath(pen, path);
            }
        };
        notificationOverlay.Controls.Add(notificationDialog);
        notificationOverlay.Resize += delegate { CenterNotificationDialog(); };

        notificationTitle = new Label();
        notificationTitle.Text = T("NotificationTitle");
        notificationTitle.Dock = DockStyle.Top;
        notificationTitle.Height = 42;
        notificationTitle.Font = new Font("Segoe UI Semibold", 15.5f, FontStyle.Bold);
        notificationTitle.ForeColor = Teal;
        notificationTitle.TextAlign = ContentAlignment.MiddleCenter;
        notificationDialog.Controls.Add(notificationTitle);

        notificationOkButton = new MaterialButton();
        notificationOkButton.Text = "OK";
        notificationOkButton.Width = 136;
        notificationOkButton.Height = 44;
        notificationOkButton.BackColor = Teal;
        notificationOkButton.ForeColor = Color.White;
        notificationOkButton.Font = new Font("Segoe UI", 10.8f, FontStyle.Bold);
        notificationOkButton.Click += delegate { notificationOverlay.Visible = false; };
        notificationDialog.Controls.Add(notificationOkButton);

        notificationMessage = new Label();
        notificationMessage.Dock = DockStyle.Fill;
        notificationMessage.Padding = new Padding(8, 14, 8, 66);
        notificationMessage.ForeColor = TextMain;
        notificationMessage.Font = new Font("Segoe UI", 11.8f);
        notificationMessage.TextAlign = ContentAlignment.MiddleCenter;
        notificationDialog.Controls.Add(notificationMessage);
        notificationOkButton.BringToFront();
        CenterNotificationDialog();

        notifyIcon = new NotifyIcon();
        notifyIcon.Icon = Icon;
        notifyIcon.Text = "Riehn Momentum Radar";
        notifyIcon.Visible = true;

        refreshTimer = new Timer();
        refreshTimer.Interval = 60 * 1000;
        refreshTimer.Tick += delegate
        {
            if (ShouldRunScheduledRefresh(DateTime.Now))
            {
                if (isLoading)
                {
                    scheduledRefreshPending = true;
                }
                else
                {
                    RefreshData();
                }
            }
        };
        refreshTimer.Start();

        Shown += delegate
        {
            ApplyLanguage();
            MarketResult cached = StateStore.LoadCachedResult();
            if (cached != null)
            {
                Render(cached, false);
                statusLabel.Text = T("CachedUpdating");
            }
            else
            {
                RenderLoadingCards();
            }

            RefreshData();
        };
        FormClosed += delegate { notifyIcon.Dispose(); };
    }

    private string T(string key)
    {
        return LocalizedText.Get(languageCode, key);
    }

    private void ApplyLanguage()
    {
        changingLanguage = true;
        for (int i = 0; i < languageSelector.Items.Count; i++)
        {
            var choice = (LanguageChoice)languageSelector.Items[i];
            if (choice.Code == languageCode)
            {
                languageSelector.SelectedIndex = i;
                break;
            }
        }
        changingLanguage = false;

        refreshButton.Text = T("Refresh");
        notificationsToggle.Text = T("Notifications");
        referenceDateLabel.Text = T("ReferenceDate");
        dashboardView.LanguageCode = languageCode;
        dashboardView.Invalidate();

        if (isLoading)
        {
            statusLabel.Text = T("LoadingStatus");
        }
        else if (hasRenderedLiveData)
        {
            statusLabel.Text = GetNextRefreshText(DateTime.Now, languageCode);
        }
        else if (dashboardView.Result != null)
        {
            statusLabel.Text = T("Cached");
        }
        else
        {
            statusLabel.Text = T("Ready");
        }
    }

    private Label Metric(TableLayoutPanel metrics, string title, string value)
    {
        var panel = new Panel();
        panel.Dock = DockStyle.Fill;
        panel.Margin = new Padding(0, 0, 12, 12);
        panel.Padding = new Padding(14, 10, 14, 10);
        panel.BackColor = CardBack;

        var layout = new TableLayoutPanel();
        layout.Dock = DockStyle.Fill;
        layout.RowCount = 2;
        layout.ColumnCount = 1;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(layout);

        var titleLabel = new Label();
        titleLabel.Text = title;
        titleLabel.Dock = DockStyle.Fill;
        titleLabel.ForeColor = TextMuted;
        titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(titleLabel, 0, 0);

        var valueLabel = new Label();
        valueLabel.Text = value;
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        valueLabel.ForeColor = TextMain;
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(valueLabel, 0, 1);

        metrics.Controls.Add(panel);
        return valueLabel;
    }

    private void RefreshData()
    {
        if (isLoading)
        {
            return;
        }

        isLoading = true;
        refreshButton.Enabled = false;
        statusLabel.Text = T("LoadingStatus");
        if (!hasRenderedLiveData && dashboardView.Result == null)
        {
            RenderLoadingCards();
        }

        DateTime referenceDate = referenceDatePicker.Value.Date;
        Task.Factory.StartNew(function: () => MarketData.GetTopMovers(180, referenceDate)).ContinueWith(task =>
        {
            BeginInvoke((Action)(() =>
            {
                isLoading = false;
                refreshButton.Enabled = true;

                if (task.IsFaulted)
                {
                    statusLabel.Text = task.Exception.GetBaseException().Message;
                }
                else
                {
                    Render(task.Result, true);
                }

                RunPendingScheduledRefresh();
            }));
        });
    }

    private void RunPendingScheduledRefresh()
    {
        if (!scheduledRefreshPending || isLoading)
        {
            return;
        }

        scheduledRefreshPending = false;
        RefreshData();
    }

    private void Render(MarketResult result, bool isLiveData)
    {
        List<StockPerformance> topFive = result.Top10.Take(5).ToList();
        List<StockPerformance> lowerFive = result.Top10.Skip(5).Take(5).ToList();

        if (isLiveData)
        {
            NotifyIfChanged(topFive);
            StateStore.SaveCachedResult(result);
            hasRenderedLiveData = true;
        }

        StateStore.SaveTopFive(topFive.Select(s => s.Symbol).ToList());
        lastTopFive = topFive.Select(s => s.Symbol).ToList();

        dashboardView.Result = result;
        dashboardView.IsLoading = false;
        statusLabel.Text = isLiveData ? GetNextRefreshText(DateTime.Now, languageCode) : T("Cached");
    }

    private void RenderLoadingCards()
    {
        dashboardView.IsLoading = true;
    }

    private Control CreateTopCard(StockPerformance stock, bool first)
    {
        var panel = new Panel();
        panel.Dock = DockStyle.Fill;
        panel.Margin = new Padding(0, 0, 10, 12);
        panel.Padding = new Padding(16);
        panel.BackColor = first ? CardBackSoft : CardBack;

        var layout = new TableLayoutPanel();
        layout.Dock = DockStyle.Fill;
        layout.ColumnCount = 1;
        layout.RowCount = 5;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.Controls.Add(layout);

        var rank = new Label();
        rank.Text = "#" + stock.Rank;
        rank.Dock = DockStyle.Fill;
        rank.ForeColor = first ? Teal : Gold;
        rank.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
        rank.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(rank, 0, 0);

        var symbol = new Label();
        symbol.Text = stock.Symbol;
        symbol.Dock = DockStyle.Fill;
        symbol.ForeColor = TextMain;
        symbol.Font = new Font("Segoe UI", 21f, FontStyle.Bold);
        symbol.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(symbol, 0, 1);

        var name = new Label();
        name.Text = stock.Name;
        name.Dock = DockStyle.Fill;
        name.ForeColor = TextMuted;
        name.TextAlign = ContentAlignment.TopLeft;
        layout.Controls.Add(name, 0, 2);

        var change = new Label();
        change.Text = FormatPercent(stock.ChangePercent);
        change.Dock = DockStyle.Fill;
        change.TextAlign = ContentAlignment.MiddleLeft;
        change.ForeColor = Green;
        change.Font = new Font("Segoe UI", 21f, FontStyle.Bold);
        layout.Controls.Add(change, 0, 3);

        var period = new Label();
        period.Text = FormatDate(stock.StartDate) + " bis " + FormatDate(stock.EndDate);
        period.Dock = DockStyle.Fill;
        period.ForeColor = TextMuted;
        period.Font = new Font("Segoe UI", 8.8f);
        period.TextAlign = ContentAlignment.BottomLeft;
        layout.Controls.Add(period, 0, 4);

        return panel;
    }

    private void NotifyIfChanged(List<StockPerformance> topFive)
    {
        if (lastTopFive == null || lastTopFive.Count == 0 || !notificationsToggle.Checked)
        {
            return;
        }

        List<string> current = topFive.Select(s => s.Symbol).ToList();
        bool changed = current.Count != lastTopFive.Count;
        for (int i = 0; !changed && i < current.Count; i++)
        {
            changed = current[i] != lastTopFive[i];
        }

        if (!changed)
        {
            return;
        }

        List<string> added = current.Where(s => !lastTopFive.Contains(s)).ToList();
        List<string> removed = lastTopFive.Where(s => !current.Contains(s)).ToList();
        string detail = added.Count > 0
            ? T("NotificationNew") + ": " + string.Join(", ", added.ToArray()) + (removed.Count > 0 ? " | " + T("NotificationOut") + ": " + string.Join(", ", removed.ToArray()) : "")
            : T("NotificationOrderChanged");

        ShowNotification(T("NotificationTitle"), detail);
    }

    private void ShowNotification(string title, string message)
    {
        notificationTitle.Text = title;
        notificationMessage.Text = message;
        notificationOverlay.Visible = true;
        notificationOverlay.BringToFront();
        CenterNotificationDialog();

        if (notificationsToggle.Checked)
        {
            notifyIcon.BalloonTipTitle = title;
            notifyIcon.BalloonTipText = message;
            notifyIcon.ShowBalloonTip(12000);
        }
    }

    private void CenterNotificationDialog()
    {
        if (notificationOverlay == null || notificationDialog == null || notificationOkButton == null)
        {
            return;
        }

        notificationDialog.Left = Math.Max(0, (notificationOverlay.ClientSize.Width - notificationDialog.Width) / 2);
        notificationDialog.Top = Math.Max(0, (notificationOverlay.ClientSize.Height - notificationDialog.Height) / 2);
        notificationOkButton.Left = (notificationDialog.ClientSize.Width - notificationOkButton.Width) / 2;
        notificationOkButton.Top = notificationDialog.ClientSize.Height - notificationOkButton.Height - 24;
        notificationOkButton.BringToFront();
    }

    private bool ShouldRunScheduledRefresh(DateTime now)
    {
        List<DateTime> schedule = MarketSchedule.GetBerlinSchedule(now.Date);
        DateTime currentMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
        bool due = schedule.Any(time => time.Year == currentMinute.Year
            && time.Month == currentMinute.Month
            && time.Day == currentMinute.Day
            && time.Hour == currentMinute.Hour
            && time.Minute == currentMinute.Minute);

        if (!due || lastScheduledRefreshMinute == currentMinute)
        {
            return false;
        }

        lastScheduledRefreshMinute = currentMinute;
        return true;
    }

    private static string GetNextRefreshText(DateTime now, string languageCode)
    {
        DateTime? next = MarketSchedule.GetNextBerlinRefresh(now);
        if (!next.HasValue)
        {
            return LocalizedText.Get(languageCode, "NextCheck") + ": -";
        }

        return LocalizedText.Get(languageCode, "NextCheck") + ": " + next.Value.ToString("dd.MM. HH:mm", CultureInfo.GetCultureInfo("de-DE"));
    }

    private static string FormatPercent(double value)
    {
        return (value >= 0 ? "+" : "") + value.ToString("0.00", CultureInfo.InvariantCulture) + "%";
    }

    private static string FormatDate(DateTime value)
    {
        return value.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE"));
    }

    private static GraphicsPath RoundedPath(Rectangle rect, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class LanguageChoice
{
    public readonly string Code;
    private readonly string displayName;

    public LanguageChoice(string code, string displayName)
    {
        Code = LocalizedText.NormalizeLanguage(code);
        this.displayName = displayName;
    }

    public override string ToString()
    {
        return displayName;
    }
}

internal sealed class MaterialButton : Button
{
    public MaterialButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent == null ? SystemColors.Control : Parent.BackColor);
        Color fill = Enabled ? BackColor : Blend(BackColor, Color.White, 0.28f);
        Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

        using (GraphicsPath path = RoundedPath(rect, Height / 2))
        using (SolidBrush brush = new SolidBrush(fill))
        {
            e.Graphics.FillPath(brush, path);
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            rect,
            ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath RoundedPath(Rectangle rect, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color Blend(Color from, Color to, float amount)
    {
        amount = Math.Max(0, Math.Min(1, amount));
        int r = (int)(from.R + (to.R - from.R) * amount);
        int g = (int)(from.G + (to.G - from.G) * amount);
        int b = (int)(from.B + (to.B - from.B) * amount);
        return Color.FromArgb(r, g, b);
    }
}

internal sealed class MaterialToggle : CheckBox
{
    private static readonly Color Teal = Color.FromArgb(0, 137, 123);
    private static readonly Color TrackOff = Color.FromArgb(214, 222, 232);
    private static readonly Color TextMain = Color.FromArgb(25, 32, 43);

    public MaterialToggle()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        base.OnCheckedChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent == null ? Color.Transparent : Parent.BackColor);

        int trackY = (Height - 22) / 2;
        Rectangle track = new Rectangle(0, trackY, 42, 22);
        using (GraphicsPath trackPath = RoundedPath(track, 11))
        using (SolidBrush trackBrush = new SolidBrush(Checked ? Teal : TrackOff))
        {
            e.Graphics.FillPath(trackBrush, trackPath);
        }

        int knobX = Checked ? 22 : 2;
        Rectangle knob = new Rectangle(knobX, trackY + 2, 18, 18);
        using (SolidBrush knobBrush = new SolidBrush(Color.White))
        {
            e.Graphics.FillEllipse(knobBrush, knob);
        }

        Rectangle textRect = new Rectangle(52, 0, Width - 52, Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, textRect, TextMain, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    private static GraphicsPath RoundedPath(Rectangle rect, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class MaterialLanguageDropdown : Control
{
    private static readonly Color Border = Color.FromArgb(220, 228, 238);
    private static readonly Color Teal = Color.FromArgb(0, 137, 123);
    private static readonly Color TextMain = Color.FromArgb(25, 32, 43);
    private static readonly Color TextMuted = Color.FromArgb(92, 103, 119);
    public readonly List<object> Items = new List<object>();
    public event EventHandler SelectedIndexChanged;
    private int selectedIndex = -1;

    public MaterialLanguageDropdown()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.White;
        Cursor = Cursors.Hand;
    }

    public int SelectedIndex
    {
        get { return selectedIndex; }
        set
        {
            if (value < -1 || value >= Items.Count || selectedIndex == value)
            {
                return;
            }

            selectedIndex = value;
            Invalidate();
            if (SelectedIndexChanged != null)
            {
                SelectedIndexChanged(this, EventArgs.Empty);
            }
        }
    }

    public object SelectedItem
    {
        get
        {
            return selectedIndex >= 0 && selectedIndex < Items.Count ? Items[selectedIndex] : null;
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        ShowMenu();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent == null ? SystemColors.Control : Parent.BackColor);

        Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath path = RoundedPath(rect, Height / 2))
        using (SolidBrush brush = new SolidBrush(Color.White))
        using (Pen pen = new Pen(Border))
        {
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }

        string text = SelectedItem == null ? "" : SelectedItem.ToString();
        TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle(16, 0, Width - 42, Height), TextMain, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

        Point[] arrow =
        {
            new Point(Width - 25, Height / 2 - 2),
            new Point(Width - 15, Height / 2 - 2),
            new Point(Width - 20, Height / 2 + 4)
        };
        using (SolidBrush arrowBrush = new SolidBrush(TextMuted))
        {
            e.Graphics.FillPolygon(arrowBrush, arrow);
        }
    }

    private void ShowMenu()
    {
        var menu = new ContextMenuStrip();
        menu.RenderMode = ToolStripRenderMode.Professional;
        menu.Renderer = new ToolStripProfessionalRenderer(new MaterialMenuColors());
        menu.ShowImageMargin = false;
        menu.Font = Font;

        for (int i = 0; i < Items.Count; i++)
        {
            int index = i;
            var item = new ToolStripMenuItem(Items[i].ToString());
            item.Padding = new Padding(10, 5, 18, 5);
            item.ForeColor = TextMain;
            item.BackColor = Color.White;
            item.Click += delegate { SelectedIndex = index; };
            if (i == selectedIndex)
            {
                item.ForeColor = Teal;
                item.Checked = true;
            }

            menu.Items.Add(item);
        }

        menu.Show(this, new Point(0, Height + 4));
    }

    private static GraphicsPath RoundedPath(Rectangle rect, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class MaterialDatePicker : Control
{
    private static readonly Color Border = Color.FromArgb(220, 228, 238);
    private static readonly Color TextMain = Color.FromArgb(25, 32, 43);
    private static readonly Color TextMuted = Color.FromArgb(92, 103, 119);
    private DateTime value = DateTime.Today;
    private DateTime maxDate = DateTime.MaxValue.Date;
    public event EventHandler ValueChanged;

    public MaterialDatePicker()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        BackColor = Color.White;
    }

    public DateTime Value
    {
        get { return value; }
        set
        {
            DateTime next = value.Date;
            if (next > maxDate)
            {
                next = maxDate;
            }

            if (this.value == next)
            {
                return;
            }

            this.value = next;
            Invalidate();
            if (ValueChanged != null)
            {
                ValueChanged(this, EventArgs.Empty);
            }
        }
    }

    public DateTime MaxDate
    {
        get { return maxDate; }
        set
        {
            maxDate = value.Date;
            if (this.value > maxDate)
            {
                Value = maxDate;
            }
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        ShowCalendar();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent == null ? SystemColors.Control : Parent.BackColor);

        Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath path = RoundedPath(rect, Height / 2))
        using (SolidBrush brush = new SolidBrush(Color.White))
        using (Pen pen = new Pen(Border))
        {
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }

        TextRenderer.DrawText(e.Graphics, value.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE")), Font, new Rectangle(14, 0, Width - 38, Height), TextMain, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        using (Pen iconPen = new Pen(TextMuted, 1.4f))
        {
            Rectangle icon = new Rectangle(Width - 28, (Height - 16) / 2, 15, 15);
            e.Graphics.DrawRectangle(iconPen, icon);
            e.Graphics.DrawLine(iconPen, icon.X, icon.Y + 4, icon.Right, icon.Y + 4);
        }
    }

    private void ShowCalendar()
    {
        var calendar = new MonthCalendar();
        calendar.MaxSelectionCount = 1;
        calendar.MaxDate = MaxDate;
        calendar.SelectionStart = Value;
        calendar.SelectionEnd = Value;

        var host = new ToolStripControlHost(calendar);
        host.Margin = Padding.Empty;
        host.Padding = Padding.Empty;
        var dropDown = new ToolStripDropDown();
        dropDown.Padding = Padding.Empty;
        dropDown.Items.Add(host);
        calendar.DateSelected += delegate
        {
            Value = calendar.SelectionStart;
            dropDown.Close();
        };

        dropDown.Show(this, new Point(0, Height + 4));
    }

    private static GraphicsPath RoundedPath(Rectangle rect, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class MaterialMenuColors : ProfessionalColorTable
{
    public override Color MenuItemSelected { get { return Color.FromArgb(232, 247, 244); } }
    public override Color MenuItemBorder { get { return Color.FromArgb(0, 137, 123); } }
    public override Color ToolStripDropDownBackground { get { return Color.White; } }
    public override Color ImageMarginGradientBegin { get { return Color.White; } }
    public override Color ImageMarginGradientMiddle { get { return Color.White; } }
    public override Color ImageMarginGradientEnd { get { return Color.White; } }
}

internal static class LocalizedText
{
    private static readonly Dictionary<string, Dictionary<string, string>> Texts = CreateTexts();

    public static string NormalizeLanguage(string languageCode)
    {
        string code = (languageCode ?? "").Trim().ToLowerInvariant();
        if (code == "en" || code == "es")
        {
            return code;
        }

        return "de";
    }

    public static string Get(string languageCode, string key)
    {
        string code = NormalizeLanguage(languageCode);
        Dictionary<string, string> language;
        if (Texts.TryGetValue(code, out language) && language.ContainsKey(key))
        {
            return language[key];
        }

        return Texts["de"].ContainsKey(key) ? Texts["de"][key] : key;
    }

    private static Dictionary<string, Dictionary<string, string>> CreateTexts()
    {
        var texts = new Dictionary<string, Dictionary<string, string>>();

        texts["de"] = new Dictionary<string, string>
        {
            { "Ready", "Bereit" },
            { "Refresh", "Aktualisieren" },
            { "Notifications", "Benachrichtigungen" },
            { "CachedUpdating", "Zeige gespeicherten Stand, aktualisiere..." },
            { "LoadingStatus", "Lade aktuelle Kursdaten..." },
            { "Cached", "Gespeicherter Stand" },
            { "NextCheck", "Nächster Check" },
            { "NotificationTitle", "Riehn Momentum Radar: Top 5 verändert" },
            { "NotificationNew", "Neu" },
            { "NotificationOut", "Raus" },
            { "NotificationOrderChanged", "Die Reihenfolge der Top 5 hat sich verändert." },
            { "Updated", "Aktualisiert" },
            { "Period", "Zeitraum" },
            { "TradingDays", "Handelstage" },
            { "StartPrice", "Startkurs" },
            { "TodayPrice", "Heute" },
            { "ReferenceDate", "Stichtag" },
            { "ReferenceRank", "Stichtag Rang" },
            { "DateTo", "bis" },
            { "LoadingCard", "Lade Daten..." },
            { "PlacesSixToTen", "Platz 6 bis 10" },
            { "Rank", "Rang" },
            { "Ticker", "Ticker" },
            { "Company", "Firma" },
            { "Momentum", "Momentum" },
            { "ReferencePrice", "Stichtag Kurs" },
            { "Portfolio", "im Depot" },
            { "SellCandidates", "zu Verkaufende Aktien" }
        };

        texts["en"] = new Dictionary<string, string>
        {
            { "Ready", "Ready" },
            { "Refresh", "Refresh" },
            { "Notifications", "Notifications" },
            { "CachedUpdating", "Showing saved data, refreshing..." },
            { "LoadingStatus", "Loading latest market data..." },
            { "Cached", "Saved data" },
            { "NextCheck", "Next check" },
            { "NotificationTitle", "Riehn Momentum Radar: Top 5 changed" },
            { "NotificationNew", "New" },
            { "NotificationOut", "Out" },
            { "NotificationOrderChanged", "The Top 5 order has changed." },
            { "Updated", "Updated" },
            { "Period", "Period" },
            { "TradingDays", "trading days" },
            { "StartPrice", "Start price" },
            { "TodayPrice", "Today" },
            { "ReferenceDate", "Reference date" },
            { "ReferenceRank", "Reference rank" },
            { "DateTo", "to" },
            { "LoadingCard", "Loading data..." },
            { "PlacesSixToTen", "Places 6 to 10" },
            { "Rank", "Rank" },
            { "Ticker", "Ticker" },
            { "Company", "Company" },
            { "Momentum", "Momentum" },
            { "ReferencePrice", "Reference price" },
            { "Portfolio", "In portfolio" },
            { "SellCandidates", "Stocks to sell" }
        };

        texts["es"] = new Dictionary<string, string>
        {
            { "Ready", "Listo" },
            { "Refresh", "Actualizar" },
            { "Notifications", "Notificaciones" },
            { "CachedUpdating", "Mostrando datos guardados, actualizando..." },
            { "LoadingStatus", "Cargando datos actuales..." },
            { "Cached", "Datos guardados" },
            { "NextCheck", "Próxima revisión" },
            { "NotificationTitle", "Riehn Momentum Radar: Top 5 cambiado" },
            { "NotificationNew", "Nuevo" },
            { "NotificationOut", "Sale" },
            { "NotificationOrderChanged", "El orden del Top 5 ha cambiado." },
            { "Updated", "Actualizado" },
            { "Period", "Periodo" },
            { "TradingDays", "días bursátiles" },
            { "StartPrice", "Precio inicial" },
            { "TodayPrice", "Hoy" },
            { "ReferenceDate", "Fecha base" },
            { "ReferenceRank", "Puesto base" },
            { "DateTo", "hasta" },
            { "LoadingCard", "Cargando datos..." },
            { "PlacesSixToTen", "Puestos 6 a 10" },
            { "Rank", "Puesto" },
            { "Ticker", "Ticker" },
            { "Company", "Empresa" },
            { "Momentum", "Momentum" },
            { "ReferencePrice", "Precio base" },
            { "Portfolio", "En cartera" },
            { "SellCandidates", "Acciones para vender" }
        };

        return texts;
    }
}

internal static class MarketSchedule
{
    public static List<DateTime> GetBerlinSchedule(DateTime berlinDate)
    {
        if (berlinDate.DayOfWeek == DayOfWeek.Saturday || berlinDate.DayOfWeek == DayOfWeek.Sunday)
        {
            return new List<DateTime>();
        }

        TimeZoneInfo eastern = FindEasternTimeZone();
        TimeZoneInfo berlin = FindBerlinTimeZone();

        DateTime berlinNoon = DateTime.SpecifyKind(berlinDate.Date.AddHours(12), DateTimeKind.Unspecified);
        DateTime easternDate = TimeZoneInfo.ConvertTime(berlinNoon, berlin, eastern).Date;
        DateTime openEt = DateTime.SpecifyKind(new DateTime(easternDate.Year, easternDate.Month, easternDate.Day, 9, 30, 0), DateTimeKind.Unspecified);
        DateTime preMarketEt = DateTime.SpecifyKind(new DateTime(easternDate.Year, easternDate.Month, easternDate.Day, 8, 0, 0), DateTimeKind.Unspecified);
        DateTime openCheckEt = openEt.AddMinutes(1);
        DateTime closeEt = DateTime.SpecifyKind(new DateTime(easternDate.Year, easternDate.Month, easternDate.Day, 16, 0, 0), DateTimeKind.Unspecified);

        var easternTimes = new List<DateTime>();
        easternTimes.Add(preMarketEt);
        easternTimes.Add(openCheckEt);
        DateTime next = openEt.AddHours(4);
        while (next < closeEt)
        {
            easternTimes.Add(next);
            next = next.AddHours(4);
        }
        easternTimes.Add(closeEt);

        return easternTimes
            .Select(time => TimeZoneInfo.ConvertTime(time, eastern, berlin))
            .Where(time => time.Date == berlinDate.Date)
            .OrderBy(time => time)
            .ToList();
    }

    public static DateTime? GetNextBerlinRefresh(DateTime now)
    {
        for (int dayOffset = 0; dayOffset < 10; dayOffset++)
        {
            foreach (DateTime scheduled in GetBerlinSchedule(now.Date.AddDays(dayOffset)))
            {
                if (scheduled > now)
                {
                    return scheduled;
                }
            }
        }

        return null;
    }

    private static TimeZoneInfo FindEasternTimeZone()
    {
        return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
    }

    private static TimeZoneInfo FindBerlinTimeZone()
    {
        return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    }
}

internal sealed class DashboardView : Control
{
    private static readonly Color PageBack = Color.FromArgb(244, 247, 251);
    private static readonly Color CardBack = Color.White;
    private static readonly Color CardBackSoft = Color.FromArgb(237, 250, 247);
    private static readonly Color HeaderBack = Color.FromArgb(242, 246, 250);
    private static readonly Color RowAlt = Color.FromArgb(250, 252, 254);
    private static readonly Color TextMain = Color.FromArgb(25, 32, 43);
    private static readonly Color TextMuted = Color.FromArgb(92, 103, 119);
    private static readonly Color Line = Color.FromArgb(220, 228, 238);
    private static readonly Color Shadow = Color.FromArgb(226, 232, 240);
    private static readonly Color SoftGold = Color.FromArgb(252, 246, 231);
    private static readonly Color Teal = Color.FromArgb(0, 137, 123);
    private static readonly Color Gold = Color.FromArgb(138, 111, 58);
    private static readonly Color Green = Color.FromArgb(22, 128, 82);

    private MarketResult result;
    private bool isLoading;
    private string languageCode = "de";
    private HashSet<string> portfolioSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<PortfolioHit> portfolioHits = new List<PortfolioHit>();

    public event EventHandler PortfolioChanged;

    public DashboardView()
    {
        DoubleBuffered = true;
        BackColor = PageBack;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    public MarketResult Result
    {
        get { return result; }
        set
        {
            result = value;
            Invalidate();
        }
    }

    public bool IsLoading
    {
        get { return isLoading; }
        set
        {
            isLoading = value;
            Invalidate();
        }
    }

    public string LanguageCode
    {
        get { return languageCode; }
        set
        {
            languageCode = LocalizedText.NormalizeLanguage(value);
            Invalidate();
        }
    }

    public HashSet<string> PortfolioSymbols
    {
        get { return new HashSet<string>(portfolioSymbols, StringComparer.OrdinalIgnoreCase); }
        set
        {
            portfolioSymbols = value == null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(value, StringComparer.OrdinalIgnoreCase);
            Invalidate();
        }
    }

    private string T(string key)
    {
        return LocalizedText.Get(languageCode, key);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        portfolioHits.Clear();
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(PageBack);

        int width = ClientSize.Width;
        int x = 0;
        int y = 10;

        const int metricsHeight = 68;
        const int topFiveHeight = 216;
        const int lowerHeight = 222;
        const int sectionGap = 16;

        DrawMetrics(g, new Rectangle(x, y, width, metricsHeight));
        y += metricsHeight + 12;
        DrawTopFive(g, new Rectangle(x, y, width, topFiveHeight));
        y += topFiveHeight + 16;
        DrawLowerTable(g, new Rectangle(x, y, width, lowerHeight));
        y += lowerHeight + sectionGap;
        DrawSellTable(g, new Rectangle(x, y, width, Math.Max(236, ClientSize.Height - y - 8)));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        foreach (PortfolioHit hit in portfolioHits.ToArray())
        {
            if (hit.Bounds.Contains(e.Location))
            {
                TogglePortfolio(hit.Symbol);
                return;
            }
        }
    }

    private void DrawMetrics(Graphics g, Rectangle area)
    {
        string updated = "-";
        if (result != null)
        {
            updated = result.UpdatedAt == DateTime.MinValue
                ? "-"
                : result.UpdatedAt.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("de-DE"));
        }

        string period = "180 " + T("TradingDays");
        if (result != null && result.Top10 != null && result.Top10.Count > 0)
        {
            StockPerformance first = result.Top10[0];
            period = FormatDate(first.StartDate) + " " + T("DateTo") + " " + FormatDate(first.EndDate);
        }

        string[] labels = { T("Updated"), T("Period") };
        string[] values = { updated, period };
        if (result != null && result.ReferenceTradingDate != DateTime.MinValue)
        {
            labels = new[] { T("Updated"), T("Period"), T("ReferenceDate") };
            values = new[] { updated, period, result.ReferenceTradingDate.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE")) };
        }

        int gap = 12;
        Rectangle bounds = new Rectangle(area.X, area.Y, area.Width - 2, area.Height - 3);
        int count = labels.Length;
        int cardWidth = (bounds.Width - gap * (count - 1)) / count;

        using (Font labelFont = new Font("Segoe UI", 9.5f))
        using (Font valueFont = new Font("Segoe UI Semibold", 12.2f, FontStyle.Bold))
        {
            for (int i = 0; i < count; i++)
            {
                int cardX = bounds.X + i * (cardWidth + gap);
                int width = i == count - 1 ? bounds.Right - cardX : cardWidth;
                Rectangle card = new Rectangle(cardX, bounds.Y, width, bounds.Height);
                FillRounded(g, card, 8, CardBack, Line, true);
                DrawText(g, labels[i], labelFont, TextMuted, new Rectangle(card.X + 20, card.Y + 14, card.Width - 40, 22), StringAlignment.Near, StringAlignment.Near);
                DrawText(g, values[i], valueFont, TextMain, new Rectangle(card.X + 20, card.Y + 40, card.Width - 40, 28), StringAlignment.Near, StringAlignment.Near);
            }
        }
    }

    private void DrawTopFive(Graphics g, Rectangle area)
    {
        int gap = 10;
        Rectangle bounds = new Rectangle(area.X, area.Y, area.Width - 2, area.Height - 3);
        int cardWidth = (bounds.Width - gap * 4) / 5;

        for (int i = 0; i < 5; i++)
        {
            int cardX = bounds.X + i * (cardWidth + gap);
            int width = i == 4 ? bounds.Right - cardX : cardWidth;
            Rectangle card = new Rectangle(cardX, bounds.Y, width, bounds.Height);
            bool promoted = IsPromoted(stockIndex: i);
            FillRounded(g, card, 8, promoted ? CardBackSoft : CardBack, promoted ? Teal : Line, true);
            FillRounded(g, new Rectangle(card.X + 18, card.Y + 16, card.Width - 36, 4), 3, promoted ? Teal : Color.FromArgb(224, 199, 135), null, false);

            if (isLoading && result == null)
            {
                DrawLoadingCard(g, card, i + 1);
                continue;
            }

            if (result == null || result.Top10 == null || result.Top10.Count <= i)
            {
                DrawLoadingCard(g, card, i + 1);
                continue;
            }

            StockPerformance stock = result.Top10[i];
            promoted = stock.EnteredTopFiveFromReference;
            using (Font rankFont = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (Font symbolFont = new Font("Segoe UI Semibold", 20.5f, FontStyle.Bold))
            using (Font nameFont = new Font("Segoe UI", 9.2f))
            using (Font changeFont = new Font("Segoe UI Semibold", 18.5f, FontStyle.Bold))
            using (Font priceFont = new Font("Segoe UI", 8.4f, FontStyle.Bold))
            using (Font portfolioFont = new Font("Segoe UI", 8.7f, FontStyle.Bold))
            {
                int left = card.X + 18;
                int innerWidth = card.Width - 36;
                Rectangle rankPill = new Rectangle(left, card.Y + 28, 54, 24);
                FillRounded(g, rankPill, 8, promoted ? Teal : SoftGold, promoted ? Teal : Color.FromArgb(236, 213, 150), false);
                DrawText(g, "#" + stock.Rank, rankFont, promoted ? Color.White : Gold, rankPill, StringAlignment.Center, StringAlignment.Center);
                if (stock.ReferenceRank > 0)
                {
                    string reference = "Stichtag #" + stock.ReferenceRank;
                    Rectangle refPill = new Rectangle(left + 62, card.Y + 28, innerWidth - 62, 24);
                    DrawText(g, reference, rankFont, promoted ? Teal : TextMuted, refPill, StringAlignment.Near, StringAlignment.Center);
                }
                DrawText(g, stock.Symbol, symbolFont, TextMain, new Rectangle(left, card.Y + 62, innerWidth, 36), StringAlignment.Near, StringAlignment.Near);
                DrawText(g, stock.Name, nameFont, TextMuted, new Rectangle(left, card.Y + 100, innerWidth, 28), StringAlignment.Near, StringAlignment.Near);
                DrawText(g, FormatPercent(stock.ChangePercent), changeFont, Green, new Rectangle(left, card.Y + 132, innerWidth, 34), StringAlignment.Near, StringAlignment.Near);
                DrawText(g, FormatPrice(stock.StartPrice) + " -> " + FormatPrice(stock.EndPrice), priceFont, TextMain, new Rectangle(left, card.Bottom - 52, innerWidth, 18), StringAlignment.Near, StringAlignment.Near);
                DrawPortfolioCheck(g, new Rectangle(left, card.Bottom - 28, innerWidth, 24), stock.Symbol, portfolioFont);
            }
        }
    }

    private bool IsPromoted(int stockIndex)
    {
        if (result == null || result.Top10 == null || result.Top10.Count <= stockIndex)
        {
            return false;
        }

        StockPerformance stock = result.Top10[stockIndex];
        return stock.EnteredTopFiveFromReference;
    }

    private void DrawLoadingCard(Graphics g, Rectangle card, int rank)
    {
        using (Font rankFont = new Font("Segoe UI", 10f, FontStyle.Bold))
        using (Font loadingFont = new Font("Segoe UI Semibold", 12f, FontStyle.Bold))
        {
            int left = card.X + 18;
            FillRounded(g, new Rectangle(card.X + 18, card.Y + 16, card.Width - 36, 4), 3, Color.FromArgb(224, 199, 135), null, false);
            Rectangle rankPill = new Rectangle(left, card.Y + 28, 54, 24);
            FillRounded(g, rankPill, 8, SoftGold, Color.FromArgb(236, 213, 150), false);
            DrawText(g, "#" + rank, rankFont, Gold, rankPill, StringAlignment.Center, StringAlignment.Center);
            FillRounded(g, new Rectangle(left, card.Y + 86, card.Width - 54, 12), 6, Color.FromArgb(232, 238, 246), null, false);
            FillRounded(g, new Rectangle(left, card.Y + 112, card.Width - 78, 12), 6, Color.FromArgb(238, 243, 249), null, false);
            DrawText(g, T("LoadingCard"), loadingFont, TextMuted, new Rectangle(left, card.Y + 144, card.Width - 36, 32), StringAlignment.Near, StringAlignment.Near);
        }
    }

    private void DrawLowerTable(Graphics g, Rectangle area)
    {
        using (Font titleFont = new Font("Segoe UI Semibold", 15.5f, FontStyle.Bold))
        {
            DrawText(g, T("PlacesSixToTen"), titleFont, TextMain, new Rectangle(area.X, area.Y, area.Width, 30), StringAlignment.Near, StringAlignment.Near);
        }

        int headerHeight = 34;
        int rowHeight = 32;
        int desiredTableHeight = headerHeight + rowHeight * 5 + 2;
        Rectangle table = new Rectangle(area.X, area.Y + 38, area.Width - 2, desiredTableHeight);
        FillRounded(g, table, 8, CardBack, Line, true);

        int[] weights = { 7, 10, 31, 12, 13, 13, 14 };
        string[] headers = { T("Rank"), T("Ticker"), T("Company"), T("Momentum"), T("ReferenceRank"), T("TodayPrice"), T("Portfolio") };

        FillRounded(g, new Rectangle(table.X, table.Y, table.Width, headerHeight), 8, HeaderBack, null, false);

        using (Pen linePen = new Pen(Line))
        using (Font headerFont = new Font("Segoe UI", 9f, FontStyle.Bold))
        using (Font rowFont = new Font("Segoe UI", 9f))
        using (Font rowBold = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold))
        {
            int[] xs = ColumnEdges(table, weights);
            for (int i = 0; i < headers.Length; i++)
            {
                Rectangle cell = new Rectangle(xs[i] + 10, table.Y + 8, xs[i + 1] - xs[i] - 20, 20);
                DrawText(g, headers[i], headerFont, TextMain, cell, StringAlignment.Near, StringAlignment.Near);
                if (i > 0)
                {
                    g.DrawLine(linePen, xs[i], table.Y + 7, xs[i], table.Y + headerHeight - 7);
                }
            }

            for (int row = 0; row < 5; row++)
            {
                int y = table.Y + headerHeight + row * rowHeight;
                if (row % 2 == 1)
                {
                    using (SolidBrush alt = new SolidBrush(RowAlt))
                    {
                        g.FillRectangle(alt, new Rectangle(table.X, y, table.Width, rowHeight));
                    }
                }

                g.DrawLine(linePen, table.X, y, table.Right, y);
                if (result == null || result.Top10 == null || result.Top10.Count <= row + 5)
                {
                    continue;
                }

                StockPerformance stock = result.Top10[row + 5];
                bool promoted = stock.EnteredTopFiveFromReference;
                if (promoted)
                {
                    using (SolidBrush highlight = new SolidBrush(CardBackSoft))
                    {
                        g.FillRectangle(highlight, new Rectangle(table.X, y, table.Width, rowHeight));
                    }
                }

                string[] values =
                {
                    "#" + stock.Rank,
                    stock.Symbol,
                    stock.Name,
                    FormatPercent(stock.ChangePercent),
                    stock.ReferenceRank > 0 ? "#" + stock.ReferenceRank : "-",
                    FormatPrice(stock.EndPrice),
                    ""
                };

                for (int col = 0; col < values.Length; col++)
                {
                    Rectangle cell = new Rectangle(xs[col] + 10, y + 8, xs[col + 1] - xs[col] - 20, 22);
                    if (col == 6)
                    {
                        DrawPortfolioCheck(g, new Rectangle(xs[col] + 10, y + 6, xs[col + 1] - xs[col] - 20, 24), stock.Symbol, rowBold);
                        continue;
                    }

                    Color color = col == 3 ? Green : col == 4 && promoted ? Teal : TextMain;
                    Font font = (col == 3 || col == 4 || col == 5) ? rowBold : rowFont;
                    DrawText(g, values[col], font, color, cell, StringAlignment.Near, StringAlignment.Near);
                }
            }
        }
    }

    private void DrawSellTable(Graphics g, Rectangle area)
    {
        if (area.Height < 96)
        {
            return;
        }

        using (Font titleFont = new Font("Segoe UI Semibold", 15.5f, FontStyle.Bold))
        {
            DrawText(g, T("SellCandidates"), titleFont, TextMain, new Rectangle(area.X, area.Y, area.Width, 30), StringAlignment.Near, StringAlignment.Near);
        }

        List<StockPerformance> sellCandidates = GetSellCandidates();
        int headerHeight = 34;
        int rowHeight = 32;
        int rowsThatFit = Math.Max(5, (area.Height - 40 - headerHeight - 2) / rowHeight);
        int visibleRows = Math.Max(5, Math.Min(8, rowsThatFit));
        Rectangle table = new Rectangle(area.X, area.Y + 38, area.Width - 2, headerHeight + rowHeight * visibleRows + 2);
        FillRounded(g, table, 8, CardBack, Line, true);

        int[] weights = { 12, 10, 36, 14, 14, 14 };
        string[] headers = { T("ReferenceRank"), T("Ticker"), T("Company"), T("Momentum"), T("ReferencePrice"), T("Portfolio") };

        FillRounded(g, new Rectangle(table.X, table.Y, table.Width, headerHeight), 8, HeaderBack, null, false);

        using (Pen linePen = new Pen(Line))
        using (Font headerFont = new Font("Segoe UI", 9f, FontStyle.Bold))
        using (Font rowFont = new Font("Segoe UI", 9f))
        using (Font rowBold = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold))
        {
            int[] xs = ColumnEdges(table, weights);
            for (int i = 0; i < headers.Length; i++)
            {
                Rectangle cell = new Rectangle(xs[i] + 10, table.Y + 8, xs[i + 1] - xs[i] - 20, 20);
                DrawText(g, headers[i], headerFont, TextMain, cell, StringAlignment.Near, StringAlignment.Near);
                if (i > 0)
                {
                    g.DrawLine(linePen, xs[i], table.Y + 7, xs[i], table.Y + headerHeight - 7);
                }
            }

            for (int row = 0; row < visibleRows; row++)
            {
                int y = table.Y + headerHeight + row * rowHeight;
                if (row % 2 == 1)
                {
                    using (SolidBrush alt = new SolidBrush(RowAlt))
                    {
                        g.FillRectangle(alt, new Rectangle(table.X, y, table.Width, rowHeight));
                    }
                }

                g.DrawLine(linePen, table.X, y, table.Right, y);
                if (sellCandidates.Count <= row)
                {
                    continue;
                }

                StockPerformance stock = sellCandidates[row];
                string[] values =
                {
                    stock.Rank > 0 ? "#" + stock.Rank : "-",
                    stock.Symbol,
                    stock.Name,
                    FormatPercent(stock.ChangePercent),
                    FormatPrice(stock.EndPrice),
                    ""
                };

                for (int col = 0; col < values.Length; col++)
                {
                    if (col == 5)
                    {
                        DrawPortfolioCheck(g, new Rectangle(xs[col] + 10, y + 6, xs[col + 1] - xs[col] - 20, 24), stock.Symbol, rowBold);
                        continue;
                    }

                    Rectangle cell = new Rectangle(xs[col] + 10, y + 8, xs[col + 1] - xs[col] - 20, 22);
                    Color color = col == 3 ? Green : TextMain;
                    Font font = (col == 0 || col == 3 || col == 4) ? rowBold : rowFont;
                    DrawText(g, values[col], font, color, cell, StringAlignment.Near, StringAlignment.Near);
                }
            }
        }
    }

    private List<StockPerformance> GetSellCandidates()
    {
        if (result == null || result.ReferenceTop10 == null || result.ReferenceTop10.Count == 0)
        {
            return new List<StockPerformance>();
        }

        var currentTop10 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (result.Top10 != null)
        {
            foreach (StockPerformance stock in result.Top10)
            {
                currentTop10.Add(stock.Symbol);
            }
        }

        return result.ReferenceTop10
            .Where(stock => stock != null
                && !string.IsNullOrWhiteSpace(stock.Symbol)
                && !currentTop10.Contains(stock.Symbol)
                && portfolioSymbols.Contains(stock.Symbol))
            .OrderBy(stock => stock.Rank)
            .ToList();
    }

    private void TogglePortfolio(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return;
        }

        if (portfolioSymbols.Contains(symbol))
        {
            portfolioSymbols.Remove(symbol);
        }
        else
        {
            portfolioSymbols.Add(symbol);
        }

        EventHandler handler = PortfolioChanged;
        if (handler != null)
        {
            handler(this, EventArgs.Empty);
        }

        Invalidate();
    }

    private void DrawPortfolioCheck(Graphics g, Rectangle area, string symbol, Font font)
    {
        if (string.IsNullOrWhiteSpace(symbol) || area.Width < 28)
        {
            return;
        }

        bool checkedValue = portfolioSymbols.Contains(symbol);
        Rectangle hit = new Rectangle(area.X, area.Y, area.Width, area.Height);
        portfolioHits.Add(new PortfolioHit { Symbol = symbol, Bounds = hit });

        Rectangle box = new Rectangle(hit.X, hit.Y + 3, 17, 17);
        Color border = checkedValue ? Teal : Color.FromArgb(190, 202, 216);
        Color fill = checkedValue ? Teal : Color.White;
        FillRounded(g, box, 5, fill, border, false);
        if (checkedValue)
        {
            using (Pen pen = new Pen(Color.White, 2f))
            {
                g.DrawLines(pen, new[]
                {
                    new Point(box.X + 4, box.Y + 9),
                    new Point(box.X + 8, box.Y + 13),
                    new Point(box.X + 14, box.Y + 5)
                });
            }
        }

        Rectangle label = new Rectangle(hit.X + 24, hit.Y, Math.Max(0, hit.Width - 24), hit.Height);
        DrawText(g, T("Portfolio"), font, checkedValue ? TextMain : TextMuted, label, StringAlignment.Near, StringAlignment.Center);
    }

    private static int[] ColumnEdges(Rectangle table, int[] weights)
    {
        int[] xs = new int[weights.Length + 1];
        xs[0] = table.X;
        int total = weights.Sum();
        int used = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            used += weights[i];
            xs[i + 1] = table.X + (table.Width * used / total);
        }
        xs[xs.Length - 1] = table.Right;
        return xs;
    }

    private static void FillRounded(Graphics g, Rectangle rect, int radius, Color fill, Color? border, bool shadow)
    {
        if (shadow)
        {
            Rectangle shadowRect = new Rectangle(rect.X + 1, rect.Y + 3, rect.Width - 1, rect.Height - 1);
            using (GraphicsPath shadowPath = RoundedPath(shadowRect, radius))
            using (SolidBrush shadowBrush = new SolidBrush(Shadow))
            {
                g.FillPath(shadowBrush, shadowPath);
            }
        }

        using (GraphicsPath path = RoundedPath(rect, radius))
        using (SolidBrush brush = new SolidBrush(fill))
        {
            g.FillPath(brush, path);
            if (border.HasValue)
            {
                using (Pen pen = new Pen(border.Value))
                {
                    g.DrawPath(pen, path);
                }
            }
        }
    }

    private static void FillRounded(Graphics g, Rectangle rect, int radius, Color fill, Color? border)
    {
        FillRounded(g, rect, radius, fill, border, false);
    }

    private static GraphicsPath RoundedPath(Rectangle rect, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void DrawText(Graphics g, string text, Font font, Color color, Rectangle rect, StringAlignment horizontal, StringAlignment vertical)
    {
        using (SolidBrush brush = new SolidBrush(color))
        using (StringFormat format = new StringFormat())
        {
            format.Alignment = horizontal;
            format.LineAlignment = vertical;
            format.Trimming = StringTrimming.EllipsisCharacter;
            format.FormatFlags = StringFormatFlags.NoWrap;
            g.DrawString(text ?? "", font, brush, rect, format);
        }
    }

    private static string FormatPercent(double value)
    {
        return (value >= 0 ? "+" : "") + value.ToString("0.00", CultureInfo.InvariantCulture) + "%";
    }

    private static string FormatPrice(double value)
    {
        if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
        {
            return "-";
        }

        return "$" + value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string FormatDate(DateTime value)
    {
        return value.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("de-DE"));
    }
}

internal static class MarketData
{
    private const string CsvUrl = "https://datahub.io/core/s-and-p-500-companies/r/constituents.csv";
    private const string WikiUrl = "https://en.wikipedia.org/wiki/List_of_S%26P_500_companies";
    private const int MaxConcurrency = 16;
    private const int SparkBatchSize = 20;
    private const int SparkBatchConcurrency = 6;
    private const int QuoteBatchSize = 50;
    private const int NetworkTimeoutMs = 15000;
    private const int MaxCsvBytes = 2 * 1024 * 1024;
    private const int MaxWikiBytes = 6 * 1024 * 1024;
    private const int MaxYahooBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedHosts =
    {
        "datahub.io",
        "r2.datahub.io",
        "query1.finance.yahoo.com",
        "en.wikipedia.org"
    };

    public static MarketResult GetTopMovers(int days)
    {
        return GetTopMovers(days, null);
    }

    public static MarketResult GetTopMovers(int days, DateTime? referenceDate)
    {
        ConstituentSet constituentSet = GetConstituents();
        List<Constituent> constituents = constituentSet.Items;
        List<StockPerformance> performances = FetchPerformancesBatch(constituents, days, null, true);
        List<StockPerformance> ranked = performances.OrderByDescending(s => s.ChangePercent).ToList();
        for (int i = 0; i < ranked.Count; i++)
        {
            ranked[i].Rank = i + 1;
        }

        if (ranked.Count < 10)
        {
            throw new InvalidOperationException("Zu wenige Kursdaten verfügbar.");
        }

        var top10 = ranked.Take(10).ToList();
        DateTime referenceTradingDate = DateTime.MinValue;
        List<StockPerformance> referenceTop10 = new List<StockPerformance>();
        if (referenceDate.HasValue)
        {
            ReferenceAnnotation annotation = AnnotateReferenceRanks(constituents, top10, days, referenceDate.Value);
            referenceTradingDate = annotation.TradingDate;
            referenceTop10 = annotation.Top10;
        }

        return new MarketResult
        {
            Days = days,
            TotalConstituents = constituents.Count,
            PricedConstituents = ranked.Count,
            ConstituentSource = constituentSet.Source,
            UpdatedAt = DateTime.Now,
            ReferenceDate = referenceDate.HasValue ? referenceDate.Value.Date : DateTime.MinValue,
            ReferenceTradingDate = referenceTradingDate,
            ReferenceTop10 = referenceTop10,
            Top10 = top10
        };
    }

    private static ReferenceAnnotation AnnotateReferenceRanks(List<Constituent> constituents, List<StockPerformance> currentTop10, int days, DateTime referenceDate)
    {
        List<StockPerformance> referencePerformances = FetchPerformancesBatch(constituents, days, referenceDate.Date, false);
        List<StockPerformance> referenceRanked = referencePerformances.OrderByDescending(s => s.ChangePercent).ToList();
        for (int i = 0; i < referenceRanked.Count; i++)
        {
            referenceRanked[i].Rank = i + 1;
        }

        var referenceBySymbol = referenceRanked.ToDictionary(stock => stock.Symbol, stock => stock, StringComparer.OrdinalIgnoreCase);
        foreach (StockPerformance current in currentTop10)
        {
            StockPerformance reference;
            if (!referenceBySymbol.TryGetValue(current.Symbol, out reference))
            {
                continue;
            }

            current.ReferenceRank = reference.Rank;
            current.ReferenceChangePercent = reference.ChangePercent;
            current.ReferenceDate = reference.EndDate;
            current.EnteredTopTenFromReference = current.Rank <= 10 && reference.Rank > 10;
            current.EnteredTopFiveFromReference = current.Rank <= 5 && reference.Rank > 5;
        }

        return new ReferenceAnnotation
        {
            TradingDate = referenceRanked.Count == 0 ? DateTime.MinValue : referenceRanked.Max(stock => stock.EndDate),
            Top10 = referenceRanked.Take(10).ToList()
        };
    }

    private static ConstituentSet GetConstituents()
    {
        try
        {
            List<Constituent> csvItems = ParseCsvConstituents(Download(CsvUrl, MaxCsvBytes));
            if (csvItems.Count >= 450)
            {
                return new ConstituentSet
                {
                    Items = csvItems,
                    Source = "Online-Aktienliste"
                };
            }
        }
        catch
        {
        }

        string html = Download(WikiUrl, MaxWikiBytes);
        Match table = Regex.Match(html, "<table[^>]*id=\"constituents\"[\\s\\S]*?</table>", RegexOptions.IgnoreCase);
        if (!table.Success)
        {
            throw new InvalidOperationException("Aktienliste wurde nicht gefunden.");
        }

        var items = new List<Constituent>();
        foreach (Match row in Regex.Matches(table.Value, "<tr[\\s\\S]*?</tr>", RegexOptions.IgnoreCase))
        {
            MatchCollection cells = Regex.Matches(row.Value, "<t[dh][^>]*>([\\s\\S]*?)</t[dh]>", RegexOptions.IgnoreCase);
            if (cells.Count < 2)
            {
                continue;
            }

            string symbol = CleanSymbol(StripHtml(cells[0].Groups[1].Value));
            string name = CleanDisplayText(StripHtml(cells[1].Groups[1].Value), 90);
            if (symbol.Length == 0 || symbol == "Symbol")
            {
                continue;
            }

            items.Add(new Constituent
            {
                Symbol = symbol,
                YahooSymbol = symbol.Replace(".", "-"),
                Name = name
            });
        }

        if (items.Count < 450)
        {
            throw new InvalidOperationException("Es wurden nur " + items.Count + " Aktien gelesen.");
        }

        return new ConstituentSet
        {
            Items = items,
            Source = "Wikipedia-Fallback"
        };
    }

    private static List<StockPerformance> FetchPerformancesBatch(List<Constituent> constituents, int days, DateTime? referenceDate, bool includeCurrentQuotes)
    {
        var results = new ConcurrentBag<StockPerformance>();
        var batches = new List<List<Constituent>>();

        for (int i = 0; i < constituents.Count; i += SparkBatchSize)
        {
            batches.Add(constituents.Skip(i).Take(SparkBatchSize).ToList());
        }

        Parallel.ForEach(batches, new ParallelOptions { MaxDegreeOfParallelism = SparkBatchConcurrency }, batch =>
        {
            try
            {
                foreach (StockPerformance performance in FetchSparkBatch(batch, days, referenceDate, includeCurrentQuotes))
                {
                        results.Add(performance);
                }
            }
            catch
            {
                foreach (Constituent stock in batch)
                {
                    try
                    {
                        results.Add(FetchPerformance(stock, days, referenceDate, includeCurrentQuotes));
                    }
                    catch
                    {
                    }
                }
            }
        });

        return results.ToList();
    }

    private static List<StockPerformance> FetchSparkBatch(List<Constituent> batch, int days)
    {
        return FetchSparkBatch(batch, days, null, true);
    }

    private static List<StockPerformance> FetchSparkBatch(List<Constituent> batch, int days, DateTime? referenceDate, bool includeCurrentQuotes)
    {
        var lookup = batch.ToDictionary(stock => stock.YahooSymbol, stock => stock, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, CurrentQuote> currentQuotes = includeCurrentQuotes ? FetchCurrentQuotes(batch) : new Dictionary<string, CurrentQuote>(StringComparer.OrdinalIgnoreCase);
        string symbols = Uri.EscapeDataString(string.Join(",", batch.Select(stock => stock.YahooSymbol).ToArray()));
        string range = referenceDate.HasValue ? "2y" : "1y";
        string url = "https://query1.finance.yahoo.com/v7/finance/spark?symbols=" + symbols + "&range=" + range + "&interval=1d";
        string json = Download(url, MaxYahooBytes);

        var serializer = new JavaScriptSerializer();
        serializer.MaxJsonLength = MaxYahooBytes;
        var root = AsDict(serializer.DeserializeObject(json));
        var spark = AsDict(root["spark"]);
        var resultArray = AsArray(spark["result"]);
        var performances = new List<StockPerformance>();

        foreach (object resultItem in resultArray)
        {
            var item = AsDict(resultItem);
            string yahooSymbol = Convert.ToString(item["symbol"], CultureInfo.InvariantCulture);
            if (!lookup.ContainsKey(yahooSymbol))
            {
                continue;
            }

            object[] responses = AsArray(item["response"]);
            if (responses.Length == 0)
            {
                continue;
            }

            var response = AsDict(responses[0]);
            object[] timestamps = AsArray(response["timestamp"]);
            var indicators = AsDict(response["indicators"]);
            object[] quote = AsArray(indicators["quote"]);
            if (quote.Length == 0)
            {
                continue;
            }

            var quoteData = AsDict(quote[0]);
            if (!quoteData.ContainsKey("close"))
            {
                continue;
            }

            object[] prices = AsArray(quoteData["close"]);
            StockPerformance performance = CalculatePerformance(lookup[yahooSymbol], timestamps, prices, days, referenceDate);
            CurrentQuote currentQuote;
            if (currentQuotes.TryGetValue(yahooSymbol, out currentQuote))
            {
                performance = ApplyCurrentQuote(performance, currentQuote);
            }

            performances.Add(performance);
        }

        return performances;
    }

    private static Dictionary<string, CurrentQuote> FetchCurrentQuotes(List<Constituent> stocks)
    {
        var quotes = new Dictionary<string, CurrentQuote>(StringComparer.OrdinalIgnoreCase);

        try
        {
            for (int i = 0; i < stocks.Count; i += QuoteBatchSize)
            {
                List<Constituent> batch = stocks.Skip(i).Take(QuoteBatchSize).ToList();
                string symbols = Uri.EscapeDataString(string.Join(",", batch.Select(stock => stock.YahooSymbol).ToArray()));
                string url = "https://query1.finance.yahoo.com/v7/finance/spark?symbols=" + symbols + "&range=1d&interval=1m&includePrePost=true";
                string json = Download(url, MaxYahooBytes);

                var serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = MaxYahooBytes;
                var root = AsDict(serializer.DeserializeObject(json));
                var spark = AsDict(root["spark"]);
                object[] resultArray = AsArray(spark["result"]);

                foreach (object resultItem in resultArray)
                {
                    var item = AsDict(resultItem);
                    string yahooSymbol = Convert.ToString(item["symbol"], CultureInfo.InvariantCulture);
                    if (string.IsNullOrWhiteSpace(yahooSymbol))
                    {
                        continue;
                    }

                    object[] responses = AsArray(item["response"]);
                    if (responses.Length == 0)
                    {
                        continue;
                    }

                    var response = AsDict(responses[0]);
                    double price;
                    DateTime time;
                    if (TryGetLastIntradayQuote(response, out price, out time))
                    {
                        quotes[yahooSymbol] = new CurrentQuote
                        {
                            Price = price,
                            Time = time
                        };
                    }
                }
            }
        }
        catch
        {
        }

        return quotes;
    }

    private static bool TryGetLastIntradayQuote(Dictionary<string, object> response, out double price, out DateTime time)
    {
        price = 0;
        time = DateTime.Now;

        try
        {
            object[] timestamps = AsArray(response["timestamp"]);
            var indicators = AsDict(response["indicators"]);
            object[] quote = AsArray(indicators["quote"]);
            if (quote.Length > 0)
            {
                var quoteData = AsDict(quote[0]);
                object[] prices = AsArray(quoteData["close"]);
                for (int i = Math.Min(timestamps.Length, prices.Length) - 1; i >= 0; i--)
                {
                    if (prices[i] == null)
                    {
                        continue;
                    }

                    double currentPrice = Convert.ToDouble(prices[i], CultureInfo.InvariantCulture);
                    if (currentPrice > 0 && !double.IsNaN(currentPrice) && !double.IsInfinity(currentPrice))
                    {
                        price = currentPrice;
                        time = FromUnixSeconds(Convert.ToInt64(timestamps[i], CultureInfo.InvariantCulture)).ToLocalTime();
                        return true;
                    }
                }
            }

            var meta = AsDict(response["meta"]);
            if (TryGetPositiveDouble(meta, "regularMarketPrice", out price))
            {
                double seconds;
                time = TryGetPositiveDouble(meta, "regularMarketTime", out seconds)
                    ? FromUnixSeconds(Convert.ToInt64(seconds)).ToLocalTime()
                    : DateTime.Now;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static List<Constituent> ParseCsvConstituents(string csv)
    {
        var items = new List<Constituent>();
        string[] lines = csv.Replace("\r\n", "\n").Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            List<string> cells = ParseCsvLine(lines[i]);
            if (cells.Count < 2)
            {
                continue;
            }

            string symbol = CleanSymbol(cells[0]);
            string name = CleanDisplayText(cells[1], 90);
            if (symbol.Length == 0)
            {
                continue;
            }

            items.Add(new Constituent
            {
                Symbol = symbol,
                YahooSymbol = symbol.Replace(".", "-"),
                Name = name
            });
        }

        return items;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                cells.Add(current.ToString());
                current.Length = 0;
            }
            else
            {
                current.Append(c);
            }
        }

        cells.Add(current.ToString());
        return cells;
    }

    private static StockPerformance FetchPerformance(Constituent stock, int days)
    {
        return FetchPerformance(stock, days, null, true);
    }

    private static StockPerformance FetchPerformance(Constituent stock, int days, DateTime? referenceDate, bool includeCurrentQuotes)
    {
        DateTime end = referenceDate.HasValue ? referenceDate.Value.Date.AddDays(1) : DateTime.UtcNow.AddDays(1);
        long from = ToUnixSeconds(end.AddDays(-(days * 3 + 30)));
        long to = ToUnixSeconds(end.AddDays(1));
        string url = "https://query1.finance.yahoo.com/v8/finance/chart/" + Uri.EscapeDataString(stock.YahooSymbol)
            + "?period1=" + from.ToString(CultureInfo.InvariantCulture)
            + "&period2=" + to.ToString(CultureInfo.InvariantCulture)
            + "&interval=1d&events=history&includeAdjustedClose=true";

        string json = Download(url, MaxYahooBytes);
        var serializer = new JavaScriptSerializer();
        serializer.MaxJsonLength = MaxYahooBytes;
        var root = (Dictionary<string, object>)serializer.DeserializeObject(json);
        var chart = AsDict(root["chart"]);
        var resultArray = AsArray(chart["result"]);
        var result = AsDict(resultArray[0]);
        var timestamps = AsArray(result["timestamp"]);
        var indicators = AsDict(result["indicators"]);
        object[] prices = null;

        if (indicators.ContainsKey("adjclose"))
        {
            var adj = AsArray(indicators["adjclose"]);
            if (adj.Length > 0)
            {
                var adjDict = AsDict(adj[0]);
                if (adjDict.ContainsKey("adjclose"))
                {
                    prices = AsArray(adjDict["adjclose"]);
                }
            }
        }

        if (prices == null)
        {
            var quote = AsArray(indicators["quote"]);
            prices = AsArray(AsDict(quote[0])["close"]);
        }

        StockPerformance performance = CalculatePerformance(stock, timestamps, prices, days, referenceDate);
        if (includeCurrentQuotes)
        {
            var quotes = FetchCurrentQuotes(new List<Constituent> { stock });
            CurrentQuote currentQuote;
            if (quotes.TryGetValue(stock.YahooSymbol, out currentQuote))
            {
                performance = ApplyCurrentQuote(performance, currentQuote);
            }
        }

        return performance;
    }

    private static StockPerformance ApplyCurrentQuote(StockPerformance performance, CurrentQuote quote)
    {
        if (quote == null || quote.Price <= 0 || performance.StartPrice <= 0)
        {
            return performance;
        }

        performance.EndPrice = quote.Price;
        performance.EndDate = quote.Time.Date;
        performance.ChangePercent = ((performance.EndPrice - performance.StartPrice) / performance.StartPrice) * 100.0;
        return performance;
    }

    private static StockPerformance CalculatePerformance(Constituent stock, object[] timestamps, object[] prices, int days)
    {
        return CalculatePerformance(stock, timestamps, prices, days, null);
    }

    private static StockPerformance CalculatePerformance(Constituent stock, object[] timestamps, object[] prices, int days, DateTime? referenceDate)
    {
        var points = new List<PricePoint>();
        for (int i = 0; i < timestamps.Length && i < prices.Length; i++)
        {
            if (prices[i] == null)
            {
                continue;
            }

            double close = Convert.ToDouble(prices[i], CultureInfo.InvariantCulture);
            if (close <= 0)
            {
                continue;
            }

            points.Add(new PricePoint
            {
                Date = FromUnixSeconds(Convert.ToInt64(timestamps[i], CultureInfo.InvariantCulture)),
                Close = close
            });
        }

        points = points.OrderBy(p => p.Date).ToList();
        if (referenceDate.HasValue)
        {
            DateTime cutoff = referenceDate.Value.Date;
            points = points.Where(p => p.Date.Date <= cutoff).ToList();
        }

        if (points.Count < 2)
        {
            throw new InvalidOperationException(stock.Symbol + ": zu wenig Kurshistorie.");
        }

        if (points.Count <= days)
        {
            throw new InvalidOperationException(stock.Symbol + ": weniger als " + days + " Handelstage verfügbar.");
        }

        PricePoint start = points[points.Count - 1 - days];
        PricePoint end = points[points.Count - 1];
        double change = ((end.Close - start.Close) / start.Close) * 100.0;

        return new StockPerformance
        {
            Symbol = CleanDisplayText(stock.Symbol, 12),
            Name = CleanDisplayText(stock.Name, 90),
            ChangePercent = change,
            StartDate = start.Date,
            EndDate = end.Date,
            StartPrice = start.Close,
            EndPrice = end.Close
        };
    }

    private static string Download(string url, int maxBytes)
    {
        Uri uri = ValidateUri(url);

        for (int redirect = 0; redirect < 4; redirect++)
        {
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.UserAgent = "riehn-momentum-radar/1.0";
            request.Timeout = NetworkTimeoutMs;
            request.ReadWriteTimeout = NetworkTimeoutMs;
            request.AllowAutoRedirect = false;
            request.KeepAlive = false;

            using (var response = GetResponse(request))
            {
                if (IsRedirect(response.StatusCode))
                {
                    string location = response.Headers[HttpResponseHeader.Location];
                    if (string.IsNullOrWhiteSpace(location))
                    {
                        throw new WebException("Redirect ohne Ziel.");
                    }

                    uri = ValidateUri(new Uri(uri, location).ToString());
                    continue;
                }

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new WebException("HTTP " + (int)response.StatusCode + " von " + uri.Host);
                }

                using (Stream stream = response.GetResponseStream())
                using (var memory = new MemoryStream())
                {
                    byte[] buffer = new byte[8192];
                    int read;
                    int total = 0;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        total += read;
                        if (total > maxBytes)
                        {
                            throw new InvalidOperationException("Antwort von " + uri.Host + " ist zu groß.");
                        }

                        memory.Write(buffer, 0, read);
                    }

                    return System.Text.Encoding.UTF8.GetString(memory.ToArray());
                }
            }
        }

        throw new WebException("Zu viele Redirects.");
    }

    private static HttpWebResponse GetResponse(HttpWebRequest request)
    {
        try
        {
            return (HttpWebResponse)request.GetResponse();
        }
        catch (WebException error)
        {
            if (error.Response is HttpWebResponse)
            {
                return (HttpWebResponse)error.Response;
            }

            throw;
        }
    }

    private static Uri ValidateUri(string url)
    {
        Uri uri;
        if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Nur HTTPS-Quellen sind erlaubt.");
        }

        bool allowed = AllowedHosts.Any(host => string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase));
        if (!allowed)
        {
            throw new InvalidOperationException("Nicht erlaubte Datenquelle: " + uri.Host);
        }

        return uri;
    }

    private static bool IsRedirect(HttpStatusCode status)
    {
        int code = (int)status;
        return code == 301 || code == 302 || code == 303 || code == 307 || code == 308;
    }

    private static string StripHtml(string value)
    {
        string stripped = Regex.Replace(value, "<style[\\s\\S]*?</style>|<script[\\s\\S]*?</script>", "", RegexOptions.IgnoreCase);
        stripped = Regex.Replace(stripped, "<[^>]+>", "");
        stripped = WebUtility.HtmlDecode(stripped);
        return Regex.Replace(stripped, "\\s+", " ").Trim();
    }

    private static string CleanSymbol(string value)
    {
        string cleaned = CleanDisplayText(value, 12).ToUpperInvariant();
        return Regex.IsMatch(cleaned, "^[A-Z0-9.\\-]{1,12}$") ? cleaned : "";
    }

    private static string CleanDisplayText(string value, int maxLength)
    {
        string cleaned = Regex.Replace(value ?? "", "\\p{C}+", "");
        cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();
        if (cleaned.Length > maxLength)
        {
            cleaned = cleaned.Substring(0, maxLength).Trim();
        }
        return cleaned;
    }

    private static Dictionary<string, object> AsDict(object value)
    {
        return (Dictionary<string, object>)value;
    }

    private static object[] AsArray(object value)
    {
        return (object[])value;
    }

    private static bool TryGetPositiveDouble(Dictionary<string, object> values, string key, out double number)
    {
        number = 0;
        object value;
        if (!values.TryGetValue(key, out value) || value == null)
        {
            return false;
        }

        try
        {
            number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return number > 0 && !double.IsNaN(number) && !double.IsInfinity(number);
        }
        catch
        {
            number = 0;
            return false;
        }
    }

    private static long ToUnixSeconds(DateTime value)
    {
        return (long)(value - new DateTime(1970, 1, 1)).TotalSeconds;
    }

    private static DateTime FromUnixSeconds(long value)
    {
        return new DateTime(1970, 1, 1).AddSeconds(value);
    }
}

internal static class StateStore
{
    private const long MaxCacheBytes = 256 * 1024;
    private static readonly string AppDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RiehnMomentumRadar");
    private static readonly string TopFivePath = Path.Combine(AppDir, "top5.txt");
    private static readonly string NotificationsPath = Path.Combine(AppDir, "notifications.txt");
    private static readonly string LanguagePath = Path.Combine(AppDir, "language.txt");
    private static readonly string PortfolioPath = Path.Combine(AppDir, "portfolio.txt");
    private static readonly string CachePath = Path.Combine(AppDir, "last-result.json");

    public static List<string> LoadTopFive()
    {
        try
        {
            if (!File.Exists(TopFivePath))
            {
                return null;
            }

            return File.ReadAllLines(TopFivePath).Where(line => line.Trim().Length > 0).ToList();
        }
        catch
        {
            return null;
        }
    }

    public static void SaveTopFive(List<string> symbols)
    {
        Directory.CreateDirectory(AppDir);
        File.WriteAllLines(TopFivePath, symbols.ToArray());
    }

    public static bool LoadNotificationsEnabled()
    {
        try
        {
            return !File.Exists(NotificationsPath) || File.ReadAllText(NotificationsPath).Trim() != "false";
        }
        catch
        {
            return true;
        }
    }

    public static void SaveNotificationsEnabled(bool enabled)
    {
        Directory.CreateDirectory(AppDir);
        File.WriteAllText(NotificationsPath, enabled ? "true" : "false");
    }

    public static string LoadLanguage()
    {
        try
        {
            if (!File.Exists(LanguagePath))
            {
                return "de";
            }

            return LocalizedText.NormalizeLanguage(File.ReadAllText(LanguagePath).Trim());
        }
        catch
        {
            return "de";
        }
    }

    public static void SaveLanguage(string languageCode)
    {
        Directory.CreateDirectory(AppDir);
        File.WriteAllText(LanguagePath, LocalizedText.NormalizeLanguage(languageCode));
    }

    public static HashSet<string> LoadPortfolioSymbols()
    {
        try
        {
            if (!File.Exists(PortfolioPath))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>(
                File.ReadAllLines(PortfolioPath)
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void SavePortfolioSymbols(IEnumerable<string> symbols)
    {
        try
        {
            Directory.CreateDirectory(AppDir);
            string[] lines = (symbols ?? Enumerable.Empty<string>())
                .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
                .Select(symbol => symbol.Trim().ToUpperInvariant())
                .Distinct()
                .OrderBy(symbol => symbol)
                .ToArray();
            File.WriteAllLines(PortfolioPath, lines);
        }
        catch
        {
        }
    }

    public static MarketResult LoadCachedResult()
    {
        try
        {
            if (!File.Exists(CachePath))
            {
                return null;
            }

            if (new FileInfo(CachePath).Length > MaxCacheBytes)
            {
                return null;
            }

            string json = File.ReadAllText(CachePath);
            var serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = (int)MaxCacheBytes;
            MarketResult result = serializer.Deserialize<MarketResult>(json);
            if (result == null || result.Top10 == null || result.Top10.Count < 10)
            {
                return null;
            }

            if (result.Top10.Any(stock => stock.StartPrice <= 0 || stock.EndPrice <= 0))
            {
                return null;
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    public static void SaveCachedResult(MarketResult result)
    {
        try
        {
            Directory.CreateDirectory(AppDir);
            var serializer = new JavaScriptSerializer();
            File.WriteAllText(CachePath, serializer.Serialize(result));
        }
        catch
        {
        }
    }

    public static void SaveTopFiveForTest(List<string> symbols)
    {
        SaveTopFive(symbols);
    }
}

internal static class NativeTest
{
    public static void Run()
    {
        string outputPath = GetOutputPath("RIEHN_MOMENTUM_RADAR_NATIVE_TEST_OUTPUT", "AKTIENMANAGER_NATIVE_TEST_OUTPUT");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = Path.Combine(Path.GetTempPath(), "riehn-momentum-radar-native-test.txt");
        }

        try
        {
            MarketResult result = MarketData.GetTopMovers(180, new DateTime(2026, 1, 1));
            string line = "OK TradingDays=" + result.Days
                + " Total=" + result.TotalConstituents
                + " Priced=" + result.PricedConstituents
                + " Source=" + result.ConstituentSource
                + " Reference=" + result.ReferenceTradingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                + " Top1=" + result.Top10[0].Symbol
                + " Top5=" + result.Top10[4].Symbol
                + " Top6=" + result.Top10[5].Symbol
                + " Top1ReferenceRank=" + result.Top10[0].ReferenceRank;
            File.WriteAllText(outputPath, line);
        }
        catch (Exception error)
        {
            File.WriteAllText(outputPath, "ERROR " + error);
        }
    }

    public static void RunNotificationTest()
    {
        string outputPath = GetOutputPath("RIEHN_MOMENTUM_RADAR_NOTIFICATION_TEST_OUTPUT", "AKTIENMANAGER_NOTIFICATION_TEST_OUTPUT");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = Path.Combine(Path.GetTempPath(), "riehn-momentum-radar-notification-test.txt");
        }

        try
        {
            MarketResult result = MarketData.GetTopMovers(180);
            List<string> simulatedOldTopFive = result.Top10.Take(4).Select(s => s.Symbol).ToList();
            simulatedOldTopFive.Add(result.Top10[5].Symbol);

            List<string> current = result.Top10.Take(5).Select(s => s.Symbol).ToList();
            List<string> added = current.Where(s => !simulatedOldTopFive.Contains(s)).ToList();
            List<string> removed = simulatedOldTopFive.Where(s => !current.Contains(s)).ToList();
            bool wouldNotify = added.Count > 0 || removed.Count > 0 || !current.SequenceEqual(simulatedOldTopFive);

            string detail = added.Count > 0
                ? "Neu: " + string.Join(", ", added.ToArray()) + (removed.Count > 0 ? " | Raus: " + string.Join(", ", removed.ToArray()) : "")
                : "Die Reihenfolge der Top 5 hat sich verändert.";

            string line = "OK WouldNotify=" + wouldNotify
                + " OldTop5=" + string.Join(",", simulatedOldTopFive.ToArray())
                + " CurrentTop5=" + string.Join(",", current.ToArray())
                + " Message=" + detail;
            File.WriteAllText(outputPath, line);
        }
        catch (Exception error)
        {
            File.WriteAllText(outputPath, "ERROR " + error);
        }
    }

    private static string GetOutputPath(string primaryName, string legacyName)
    {
        string outputPath = Environment.GetEnvironmentVariable(primaryName);
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            return outputPath;
        }

        return Environment.GetEnvironmentVariable(legacyName);
    }
}

internal sealed class Constituent
{
    public string Symbol;
    public string YahooSymbol;
    public string Name;
}

internal sealed class StockPerformance
{
    public int Rank;
    public string Symbol;
    public string Name;
    public double ChangePercent;
    public DateTime StartDate;
    public DateTime EndDate;
    public double StartPrice;
    public double EndPrice;
    public int ReferenceRank;
    public double ReferenceChangePercent;
    public DateTime ReferenceDate;
    public bool EnteredTopTenFromReference;
    public bool EnteredTopFiveFromReference;
}

internal sealed class ReferenceAnnotation
{
    public DateTime TradingDate;
    public List<StockPerformance> Top10;
}

internal sealed class CurrentQuote
{
    public double Price;
    public DateTime Time;
}

internal sealed class MarketResult
{
    public int Days;
    public int TotalConstituents;
    public int PricedConstituents;
    public string ConstituentSource;
    public DateTime UpdatedAt;
    public DateTime ReferenceDate;
    public DateTime ReferenceTradingDate;
    public List<StockPerformance> ReferenceTop10;
    public List<StockPerformance> Top10;
}

internal sealed class PortfolioHit
{
    public string Symbol;
    public Rectangle Bounds;
}

internal sealed class ConstituentSet
{
    public List<Constituent> Items;
    public string Source;
}

internal sealed class PricePoint
{
    public DateTime Date;
    public double Close;
}
