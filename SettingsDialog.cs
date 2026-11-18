using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace RestTimer
{
    internal sealed class SettingsDialog : Form
    {
        private DurationEditor workDuration, breakDuration;
        private Label note, timerStatus;
        private Button startTimer, stopTimer, resetTimer, savePreferences;
        private CheckBox showTaskbarCountdown, startupPreference, daily;
        private ComboBox language, calendarChoice, themeChoice;
        private Timer sidebarAnimation;
        private float sidebarFrom, sidebarTarget;
        private readonly Stopwatch sidebarClock = new Stopwatch();
        private readonly UpdateService service;
        private readonly UpdatePanel updatePanel;
        private readonly Panel content;
        private readonly Control generalPage, aboutPage;
        private readonly WorkReportPanel reportPage;
        public event Action RestartRequested;
        private readonly Dictionary<string, RoundedButton> navigation = new Dictionary<string, RoundedButton>();
        private readonly Bitmap logo;
        private readonly Bitmap gitLogo;
        private readonly Timer themeTimer;
        private string paletteKey;
        private RoundedPanel sidebar;
        private TableLayoutPanel rootLayout, sidebarTabs;
        private Panel sidebarBrand;
        private Label sidebarVersion, sidebarName;
        private RoundedButton sidebarToggle;
        private readonly ToolTip sidebarHints = new ToolTip();
        public bool SidebarCollapsed { get; private set; }
        private Panel generalBody;
        private bool relocalizing;
        public event Action LanguagePreviewChanged;
        private readonly Preferences originalPreferences;
        private readonly bool originalDaily;
        private bool originalStartup, saving;
        public Preferences SelectedPreferences { get; private set; }
        public string CurrentPage { get; private set; }
        public bool SelectedDailyEnabled => daily.Checked;
        public bool? SelectedStartupEnabled => startupPreference.Enabled ? (bool?)startupPreference.Checked : null;
        public bool StartupChanged => startupPreference.Enabled && startupPreference.Checked != originalStartup;
        public bool HasPendingChanges => !workDuration.TryGetSeconds(out int work) || !breakDuration.TryGetSeconds(out int rest)
            || work != originalPreferences.WorkSeconds || rest != originalPreferences.BreakSeconds
            || showTaskbarCountdown.Checked != originalPreferences.ShowTaskbarCountdown
            || ((LanguageItem)language.SelectedItem).Code != originalPreferences.Language
            || ((LanguageItem)calendarChoice.SelectedItem).Code != originalPreferences.Calendar
            || ((LanguageItem)themeChoice.SelectedItem).Code != originalPreferences.ColorMode
            || daily.Checked != originalDaily || StartupChanged;

        private sealed class LanguageItem
        {
            public string Code { get; }
            private readonly string name;
            public LanguageItem(string code, string name) { Code = code; this.name = name; }
            public override string ToString() => name;
        }

        public SettingsDialog(Preferences preferences, Icon icon, Action start, Action stop)
            : this(preferences, icon, start, stop, null) { }
        public SettingsDialog(Preferences preferences, Icon icon, Action start, Action stop, UpdateService updater)
            : this(preferences, icon, start, stop, updater, "general", true) { }

        public SettingsDialog(Preferences preferences, Icon icon, Action start, Action stop, UpdateService updater,
            string initialPage, bool checkOnShow)
        {
            service = updater ?? new UpdateService();
            originalPreferences = preferences;
            DateDisplay.Preference=preferences.Calendar;
            originalDaily = service.State.DailyEnabled;
            Theme.Refresh();
            Text = "RestTimer — " + I18n.T("preferences_title");
            Icon = icon;
            logo = icon.ToBitmap();
            using (var stream = typeof(SettingsDialog).Assembly.GetManifestResourceStream("RestTimer.Git.png"))
            using (var image = Image.FromStream(stream)) gitLogo = new Bitmap(image);
            Font = Theme.BodyFont;
            RightToLeft = I18n.Flow;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(960, 780);
            MinimumSize = new Size(780, 640);
            DoubleBuffered = true;
            MinimizeBox = false;
            var layout = new BufferedTableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 1 };
            rootLayout = layout;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            sidebar = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(8),
                Margin = I18n.IsRtl ? new Padding(10, 0, 0, 0) : new Padding(0, 0, 10, 0) };
            var tabs = new BufferedTableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9, AccessibleName = I18n.T("settings_navigation") };
            sidebarTabs = tabs;
            tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tabs.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            tabs.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            for (int i = 0; i < 5; i++) tabs.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            tabs.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tabs.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            var toggleRow = new Panel { Dock = DockStyle.Fill, RightToLeft = RightToLeft.No, Margin=Padding.Empty };
            sidebarToggle = new RoundedButton { Name = "sidebar_toggle", Glyph = "menu", IconOnly = true, Quiet = true, Width = 40, Dock = I18n.IsRtl ? DockStyle.Right : DockStyle.Left };
            sidebarToggle.Click += (_, __) => AnimateSidebar(!SidebarCollapsed);
            toggleRow.Controls.Add(sidebarToggle); tabs.Controls.Add(toggleRow,0,0);
            var brand = new Panel { Dock = DockStyle.Fill };
            sidebarBrand = brand;
            brand.Controls.Add(new PictureBox { Image = logo, SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Top, Height = 36 });
            brand.Controls.Add(sidebarName=new Label { Text = "RestTimer", Font = Theme.TitleFont, TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Bottom, Height = 32 });
            tabs.Controls.Add(brand, 0, 1);
            string[] pages = { "report", "general", "updates", "history", "about" };
            string[] keys = { "dashboard", "general", "updates_tab", "version_history", "about" };
            for (int i = 0; i < pages.Length; i++)
            {
                string page = pages[i];
                var button = new RoundedButton { Text = I18n.T(keys[i]), Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3), TabIndex = i + 1, Quiet = true, KeepIconAtStart = true, StartAligned = true, RightToLeft = I18n.Flow, TextAlign = I18n.IsRtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft };
                button.Click += (_, __) => SelectPage(page);
                navigation.Add(page, button);
                tabs.Controls.Add(button, 0, i + 2);
            }
            sidebarVersion = new Label { Text = "v" + UpdateClient.CurrentVersion.ToString(3), Font = Theme.SmallFont,
                Tag = "muted", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, RightToLeft = RightToLeft.No };
            tabs.Controls.Add(sidebarVersion,0,8);
            sidebar.Controls.Add(tabs);
            content = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            generalPage = CreateGeneralPage(preferences, start, stop);
            aboutPage = CreateAboutPage();
            reportPage = new WorkReportPanel();
            updatePanel = new UpdatePanel(service, checkOnShow) { Dock = DockStyle.Fill };
            updatePanel.DailyPreferenceChanged += () => daily.Checked = updatePanel.DailyEnabled;
            content.Controls.AddRange(new Control[] { generalPage, updatePanel, aboutPage, reportPage });
            layout.Controls.Add(sidebar, 0, 0);
            layout.Controls.Add(content, 1, 0);
            Controls.Add(layout);
            SetSidebarCollapsed(false);
            language.SelectedIndexChanged += (_, __) => PreviewLanguage();
            SelectPage(initialPage);
            ApplyTheme();
            themeTimer = new Timer { Interval = 750 };
            themeTimer.Tick += (_, __) => { Theme.Refresh(); if (paletteKey != Theme.PaletteKey) ApplyTheme(); };
            themeTimer.Start();
        }

        private float SidebarWidth(bool collapsed)
        {
            float z=AutoScaleDimensions.Width/96f;
            if(collapsed) return 66*z;
            int text=0;foreach(var button in navigation.Values) text=Math.Max(text,TextRenderer.MeasureText(button.Text,button.Font).Width);
            float desired=Math.Max(190*z,text+74*z);
            float available=Math.Max(190*z,ClientSize.Width-24*z-480*z);
            return Math.Min(Math.Min(280*z,available),desired);
        }
        private void RenderSidebar(bool iconsOnly)
        {
            float z=AutoScaleDimensions.Width/96f;
            // Keep the brand/footer rows in place so navigation never jumps vertically.
            sidebar.Padding=new Padding((int)(8*z));
            sidebarTabs.RowStyles[1].Height=76*z;
            sidebarTabs.RowStyles[8].Height=28*z;
            sidebarBrand.Visible=true;
            sidebarName.Visible=sidebarVersion.Visible=!iconsOnly;
            sidebarToggle.Text=I18n.T(SidebarCollapsed?"expand_sidebar":"collapse_sidebar");
            sidebarToggle.AccessibleName=sidebarToggle.Text;
            sidebarToggle.Dock=I18n.IsRtl?DockStyle.Right:DockStyle.Left;
            sidebarToggle.Width=(int)(40*z);
            sidebarHints.SetToolTip(sidebarToggle,sidebarToggle.Text);
            foreach(var button in navigation.Values)
            {
                button.IconOnly=iconsOnly;button.StartAligned=true;button.RightToLeft=I18n.Flow;
                button.TextAlign=I18n.IsRtl?ContentAlignment.MiddleRight:ContentAlignment.MiddleLeft;
                button.AccessibleName=button.Text;sidebarHints.SetToolTip(button,button.Text);button.Invalidate();
            }
        }
        public void SetSidebarCollapsed(bool collapsed)
        {
            sidebarAnimation?.Stop();SidebarCollapsed=collapsed;
            rootLayout.SuspendLayout();
            RenderSidebar(collapsed);rootLayout.ColumnStyles[0].Width=SidebarWidth(collapsed);
            rootLayout.ResumeLayout(true);
        }

        private Control CreateGeneralPage(Preferences preferences, Action start, Action stop)
        {
            var body = new Panel { Dock = DockStyle.Fill, RightToLeft = I18n.Flow, AutoScroll = false };
            generalBody = body;
            var timeSection=CreateSection("time_settings");
            var localeSection=CreateSection("language_region");
            var appearanceSection=CreateSection("appearance");
            var behaviorSection=CreateSection("app_behavior");
            workDuration = new DurationEditor(I18n.T("work_short"), I18n.T("work_hint"), preferences.WorkSeconds);
            breakDuration = new DurationEditor(I18n.T("break_short"), I18n.T("break_hint"), preferences.BreakSeconds);
            startTimer = new RoundedButton(true) { Name="i18n:start", Text=I18n.T("start") };
            stopTimer = new RoundedButton { Name="i18n:stop", Text=I18n.T("stop") };
            resetTimer = new RoundedButton { Name="i18n:reset_timer", Text=I18n.T("reset_timer") };
            startTimer.Click+=(_,__)=>start(); stopTimer.Click+=(_,__)=>stop(); resetTimer.Click+=(_,__)=>RestartRequested?.Invoke();
            timerStatus = new Label { Font=Theme.SmallFont, Tag="muted", TextAlign=ContentAlignment.MiddleLeft };
            language = new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList, RightToLeft=RightToLeft.No, FlatStyle=FlatStyle.Flat,
                DrawMode=DrawMode.OwnerDrawFixed, ItemHeight=Font.Height+6, DropDownHeight=320, AccessibleName=I18n.T("language") };
            language.Items.Add(new LanguageItem("auto", I18n.T("system_language")));
            foreach (string code in I18n.SupportedCodes) language.Items.Add(new LanguageItem(code, I18n.NativeName(code)));
            language.DrawItem += (_, e) =>
            {
                if (e.Index < 0) return;
                bool selected = (e.State & DrawItemState.Selected) != 0;
                using (var brush = new SolidBrush(selected ? Theme.Selection : Theme.Input)) e.Graphics.FillRectangle(brush, e.Bounds);
                var item = (LanguageItem)language.Items[e.Index];
                var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.Left;
                if (item.Code == "fa" || item.Code == "ar" || item.Code == "auto" && I18n.IsRtl) flags |= TextFormatFlags.RightToLeft;
                TextRenderer.DrawText(e.Graphics, item.ToString(), Font, Rectangle.Inflate(e.Bounds, -5, 0), selected ? Theme.OnSelection : Theme.Text, flags);
                if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
            };
            for (int i = 0; i < language.Items.Count; i++)
                if (((LanguageItem)language.Items[i]).Code == preferences.Language) language.SelectedIndex = i;

            calendarChoice=Choice(new[]{"auto","gregorian","persian","hijri"},new[]{"system_calendar","gregorian","persian","hijri"},preferences.Calendar);
            themeChoice=Choice(new[]{"system","light","dark"},new[]{"system_theme","light_theme","dark_theme"},preferences.ColorMode);
            var selectors=new[]{language,calendarChoice,themeChoice};
            var labels=new Label[3]; string[] labelKeys={"language","calendar","appearance"};
            for(int i=0;i<3;i++) { labels[i]=new Label { Name="i18n:"+labelKeys[i], Text=I18n.T(labelKeys[i]), TextAlign=ContentAlignment.MiddleLeft }; }
            daily=new ThemedCheckBox { Name="i18n:daily_check", Text=I18n.T("daily_check"), Checked=service.State.DailyEnabled };
            daily.CheckedChanged+=(_,__)=>updatePanel?.SyncDailyPreference(daily.Checked);
            startupPreference=new ThemedCheckBox { Name="i18n:startup", Text=I18n.T("startup") };
            try { startupPreference.Checked=Preferences.StartsWithWindows(); }
            catch(Exception ex) when(ex is System.IO.IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException) { startupPreference.Enabled=false; }
            originalStartup=startupPreference.Checked;
            showTaskbarCountdown=new ThemedCheckBox { Name="i18n:show_countdown", Text=I18n.T("show_countdown"), Checked=preferences.ShowTaskbarCountdown };
            note=new Label { Name="i18n:duration_note", Text=I18n.T("duration_note"), Font=Theme.SmallFont, Tag="muted", TextAlign=ContentAlignment.MiddleLeft };
            savePreferences=new RoundedButton(true) { Name="i18n:save", Text=I18n.T("save") };savePreferences.Click+=(_,__)=>SaveDurations();
            timeSection.Controls.AddRange(new Control[]{workDuration,breakDuration,startTimer,stopTimer,resetTimer,timerStatus});
            localeSection.Controls.AddRange(new Control[]{labels[0],language,labels[1],calendarChoice});
            appearanceSection.Controls.Add(themeChoice);
            // The section title already labels the appearance selector.
            labels[2].Dispose();themeChoice.AccessibleName=I18n.T("appearance");
            behaviorSection.Controls.AddRange(new Control[]{daily,startupPreference,showTaskbarCountdown});
            body.Controls.AddRange(new Control[]{timeSection,localeSection,appearanceSection,behaviorSection,note,savePreferences});
            Action arrange=()=>
            {
                float z=AutoScaleDimensions.Width/96f;
                int pad=(int)(12*z), w=Math.Max(1,Math.Min(body.ClientSize.Width-2*pad,(int)(720*z))), x=(body.ClientSize.Width-w)/2;
                int h=body.ClientSize.Height, y=(int)(8*z);
                bool compact=h<660*z;
                int gap=(int)((compact?6:8)*z);
                int inset=(int)((compact?6:12)*z), heading=(int)((compact?24:28)*z);
                int row=(int)((compact?28:32)*z), checkHeight=(int)((compact?22:28)*z);
                foreach(var check in new[]{daily,startupPreference,showTaskbarCountdown}) check.Font=compact?note.Font:Font;
                int middleHeight=heading+2*row+gap+2*inset;
                int behaviorHeight=heading+2*inset;
                foreach(ThemedCheckBox item in new[]{daily,startupPreference,showTaskbarCountdown}) behaviorHeight+=Math.Max(checkHeight,item.HeightForWidth(w-2*inset));
                int noteWidth=w-(int)(154*z)-gap;
                int footer=Math.Max((int)((compact?38:42)*z),TextRenderer.MeasureText(note.Text,note.Font,new Size(Math.Max(1,noteWidth),32767),TextFormatFlags.WordBreak).Height+(int)(4*z));
                int controls=(int)((compact?28:32)*z), status=(int)((compact?18:22)*z);
                int card=Math.Max((int)(90*z),Math.Min((int)(120*z),h-y-middleHeight-behaviorHeight-footer-4*gap-heading-controls-status-2*inset-gap));
                int timeHeight=heading+card+gap+controls+status+2*inset;
                timeSection.SetBounds(x,y,w,timeHeight);y+=timeHeight+gap;
                int inner=w-2*inset, half=(inner-gap)/2;
                workDuration.SetBounds(I18n.IsRtl?inset+inner-half:inset,inset+heading,half,card);
                breakDuration.SetBounds(I18n.IsRtl?inset:inset+inner-half,inset+heading,half,card);
                workDuration.CompactLayout();breakDuration.CompactLayout();
                int cy=inset+heading+card+gap;
                var timerButtons=new[]{(RoundedButton)startTimer,(RoundedButton)stopTimer,(RoundedButton)resetTimer};
                int wanted=0;foreach(var button in timerButtons) wanted+=button.LabeledWidth;
                bool iconActions=wanted>inner-2*gap;
                int free=inner-2*gap-(iconActions?0:wanted), cursor=I18n.IsRtl?inset+inner:inset;
                for(int i=0;i<timerButtons.Length;i++)
                {
                    var button=timerButtons[i];button.IconOnly=iconActions;button.AccessibleName=button.Text;sidebarHints.SetToolTip(button,button.Text);
                    int bw=(iconActions?0:button.LabeledWidth)+free/3+(i==2?free%3:0);
                    button.SetBounds(I18n.IsRtl?cursor-bw:cursor,cy,bw,controls);cursor+=(I18n.IsRtl?-1:1)*(bw+gap);
                }
                timerStatus.SetBounds(inset,cy+controls,inner,status);
                int localeWidth=(int)((w-gap)*0.62), appearanceWidth=w-gap-localeWidth;
                localeSection.SetBounds(I18n.IsRtl?x+w-localeWidth:x,y,localeWidth,middleHeight);
                appearanceSection.SetBounds(I18n.IsRtl?x:x+localeWidth+gap,y,appearanceWidth,middleHeight);y+=middleHeight+gap;
                for(int i=0;i<2;i++)
                {
                    int available=localeWidth-2*inset;
                    int lw=Math.Min(available/3,Math.Max((int)(70*z),TextRenderer.MeasureText(labels[i].Text,labels[i].Font).Width+(int)(4*z)));
                    labels[i].AutoEllipsis=true;
                    int ly=inset+heading+i*(row+gap);
                    labels[i].SetBounds(I18n.IsRtl?inset+available-lw:inset,ly,lw,row);
                    selectors[i].SetBounds(I18n.IsRtl?inset:inset+lw,ly,Math.Max(1,available-lw-gap),row);
                }
                themeChoice.SetBounds(inset,inset+heading,appearanceWidth-2*inset,row);
                behaviorSection.SetBounds(x,y,w,behaviorHeight);y+=behaviorHeight+gap;
                int iy=inset+heading;
                foreach(var check in new[]{daily,startupPreference,showTaskbarCountdown}) { int ch=Math.Max(checkHeight,((ThemedCheckBox)check).HeightForWidth(w-2*inset));check.SetBounds(inset,iy,w-2*inset,ch);iy+=ch; }
                foreach(var section in new[]{timeSection,localeSection,appearanceSection,behaviorSection})
                    section.Controls["section_heading"].SetBounds(inset,inset,section.Width-2*inset,heading);
                int saveWidth=(int)(154*z), footerY=Math.Max(y,h-footer);
                savePreferences.SetBounds(I18n.IsRtl?x+w-saveWidth:x,footerY,saveWidth,(int)(34*z));
                note.SetBounds(I18n.IsRtl?x:x+saveWidth+gap,footerY,w-saveWidth-gap,footer);
            };
            body.Resize+=(_,__)=>arrange(); body.Layout+=(_,__)=>{ if(!body.IsDisposed) arrange(); };
            calendarChoice.SelectedIndexChanged+=(_,__)=> { if(calendarChoice.SelectedItem==null) return;DateDisplay.Preference=((LanguageItem)calendarChoice.SelectedItem).Code;reportPage?.RefreshReport(); };
            themeChoice.SelectedIndexChanged+=(_,__)=> { if(themeChoice.SelectedItem==null) return;Theme.Use(((LanguageItem)themeChoice.SelectedItem).Code);ApplyTheme(); };
            return body;
        }
        private RoundedPanel CreateSection(string key)
        {
            var section=new RoundedPanel { Name="section:"+key, AccessibleName=I18n.T(key), AccessibleRole=AccessibleRole.Grouping,
                RightToLeft=I18n.Flow, CornerRadius=14 };
            section.Controls.Add(new Label { Name="section_heading", Tag="accent", Text=I18n.T(key), Font=Theme.TitleFont,
                RightToLeft=I18n.Flow, TextAlign=ContentAlignment.MiddleLeft });
            return section;
        }
        private ComboBox Choice(string[] values,string[] keys,string selection)
        {
            var box=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList, FlatStyle=FlatStyle.Flat };
            for(int i=0;i<values.Length;i++) { box.Items.Add(new LanguageItem(values[i],I18n.T(keys[i])));if(values[i]==selection) box.SelectedIndex=i; }
            box.DrawMode=DrawMode.OwnerDrawFixed;box.ItemHeight=Font.Height+6;
            box.DrawItem+=(_,e)=> { if(e.Index<0) return;bool selected=(e.State&DrawItemState.Selected)!=0;
                using(var brush=new SolidBrush(selected?Theme.Selection:Theme.Input)) e.Graphics.FillRectangle(brush,e.Bounds);
                TextRenderer.DrawText(e.Graphics,box.Items[e.Index].ToString(),Font,Rectangle.Inflate(e.Bounds,-6,0),selected?Theme.OnSelection:Theme.Text,
                    TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|(I18n.IsRtl?TextFormatFlags.Right|TextFormatFlags.RightToLeft:TextFormatFlags.Left)); };
            return box;
        }

        public void SetRunning(bool running)
        {
            startTimer.Enabled = !running;
            stopTimer.Enabled = running;
            timerStatus.Text = running ? I18n.T("timer_running") : I18n.T("timer_paused");
        }

        private Control CreateAboutPage()
        {
            var page = new RoundedPanel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 64, 40, 30, 50, 30, 30, 46 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(new PictureBox { Image = logo, SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) }, 0, 0);
            layout.Controls.Add(new Label { Text = "RestTimer", Font = Theme.HeadingFont, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 1);
            layout.Controls.Add(new Label { Text = I18n.T("installed", UpdateClient.CurrentVersion.ToString(3)), Tag = "accent",
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 2);
            layout.Controls.Add(new Label { Name = "i18n:about_description", Text = I18n.T("about_description"), Tag = "muted", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 3);
            layout.Controls.Add(new Label { Name = "about_author", Text = I18n.T("author") + ": \u200eSaleh Ghotbani", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 4);
            layout.Controls.Add(new Label { Name = "about_license", Text = I18n.T("license") + ": MIT", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 5);
            var project = new RoundedButton { Name = "i18n:project_site", Text = I18n.T("project_site"), Image = gitLogo, Dock = DockStyle.Fill, Margin = new Padding(8, 4, 8, 4) };
            project.Click += (_, __) =>
            {
                try { Process.Start(new ProcessStartInfo("https://github.com/salehghotbani/RestTimer") { UseShellExecute = true }); }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
                { MessageBox.Show(this, I18n.T("open_link_error"), "RestTimer", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            };
            layout.Controls.Add(project, 0, 6);
            string licenseText;
            using (var stream = typeof(SettingsDialog).Assembly.GetManifestResourceStream("RestTimer.LICENSE"))
            using (var reader = new System.IO.StreamReader(stream)) licenseText = reader.ReadToEnd();
            var license = new RichTextBox { Name = "license_text", Text = licenseText,
                ReadOnly = true, BorderStyle = BorderStyle.None, DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.Vertical, RightToLeft = RightToLeft.No,
                Font = Theme.SmallFont, AccessibleName = "MIT License", WordWrap = true };
            layout.Controls.Add(new ScrollFrame(license) { Margin = new Padding(0, 12, 0, 0) }, 0, 7);
            page.Controls.Add(layout);
            return page;
        }

        public void SelectPage(string page)
        {
            if (!navigation.ContainsKey(page)) page = "general";
            CurrentPage = page;
            generalPage.Visible = page == "general";
            aboutPage.Visible = page == "about";
            reportPage.Visible = page == "report";
            if (reportPage.Visible) reportPage.RefreshReport();
            updatePanel.Visible = page == "updates" || page == "history";
            updatePanel.SelectPage(page == "history");
            foreach (var pair in navigation) { pair.Value.Selected = pair.Key == page; pair.Value.Invalidate(); }
            AcceptButton = page == "general" ? (IButtonControl)savePreferences : null;
            if (Visible && updatePanel.Visible) updatePanel.EnsureLoaded();
        }

        private void PreviewLanguage()
        {
            if (relocalizing || language.SelectedItem == null) return;
            string previous = I18n.Code;
            I18n.Use(((LanguageItem)language.SelectedItem).Code);
            if (previous == I18n.Code) return;
            relocalizing = true;
            SuspendLayout();
            try
            {
                I18n.RelocalizeControls(this, previous);
                Text = "RestTimer — " + I18n.T("preferences_title");
                RightToLeft = generalBody.RightToLeft = I18n.Flow;
                int gap = (int)(10 * DeviceDpi / 96f);
                sidebar.Margin = I18n.IsRtl ? new Padding(gap, 0, 0, 0) : new Padding(0, 0, gap, 0);

                workDuration.Relocalize(I18n.T("work_short"), I18n.T("work_hint"));
                breakDuration.Relocalize(I18n.T("break_short"), I18n.T("break_hint"));
                string[] pages = { "report", "general", "updates", "history", "about" };
                string[] keys = { "dashboard", "general", "updates_tab", "version_history", "about" };
                for (int i = 0; i < pages.Length; i++) navigation[pages[i]].Text = I18n.T(keys[i]);
                SetSidebarCollapsed(SidebarCollapsed);
                int selected = language.SelectedIndex;
                language.Items[0] = new LanguageItem("auto", I18n.T("system_language"));
                language.SelectedIndex = selected;
                aboutPage.Controls.Find("about_author", true)[0].Text = I18n.T("author") + ": \u200eSaleh Ghotbani";
                aboutPage.Controls.Find("about_license", true)[0].Text = I18n.T("license") + ": MIT";
                updatePanel.Relocalize(previous, CurrentPage == "history");
                RefreshChoices(calendarChoice,new[]{"system_calendar","gregorian","persian","hijri"});
                RefreshChoices(themeChoice,new[]{"system_theme","light_theme","dark_theme"});
                foreach(Control section in generalBody.Controls) if(section.Name.StartsWith("section:")) { section.RightToLeft=I18n.Flow;section.Controls["section_heading"].RightToLeft=I18n.Flow; }
                generalBody.PerformLayout();
                reportPage.RefreshReport();
                ApplyTheme();
            }
            finally { ResumeLayout(true); relocalizing = false; }
            LanguagePreviewChanged?.Invoke();
        }

        private void RefreshChoices(ComboBox box,string[] keys)
        {
            int selected=box.SelectedIndex;
            for(int i=0;i<keys.Length;i++) { var item=(LanguageItem)box.Items[i];box.Items[i]=new LanguageItem(item.Code,I18n.T(keys[i])); }
            box.SelectedIndex=selected;
        }
        private void AnimateSidebar(bool collapsed)
        {
            sidebarAnimation?.Stop();
            sidebarFrom=rootLayout.ColumnStyles[0].Width;SidebarCollapsed=collapsed;sidebarTarget=SidebarWidth(collapsed);
            // Draw only icons while resizing; labels are revealed at their final width.
            RenderSidebar(true);
            if(sidebarAnimation==null)
            {
                sidebarAnimation=new Timer { Interval=15 };
                sidebarAnimation.Tick+=(_,__)=>
                {
                    double t=Math.Min(1,sidebarClock.Elapsed.TotalMilliseconds/190), ease=1-Math.Pow(1-t,3);
                    rootLayout.ColumnStyles[0].Width=sidebarFrom+(sidebarTarget-sidebarFrom)*(float)ease;
                    if(t>=1) { sidebarAnimation.Stop();RenderSidebar(SidebarCollapsed); }
                };
            }
            sidebarClock.Restart();sidebarAnimation.Start();
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if(rootLayout!=null && navigation.Count>0 && sidebarAnimation?.Enabled!=true)
                rootLayout.ColumnStyles[0].Width=SidebarWidth(SidebarCollapsed);
        }
        private void ApplyTheme() { Theme.Apply(this); paletteKey = Theme.PaletteKey; }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Theme.ApplyWindowFrame(Handle); }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyTheme();
            var area = Screen.FromControl(this).WorkingArea;
            if (Height > area.Height - 24) Height = area.Height - 24;
            if (Width > area.Width - 24) Width = area.Width - 24;
            SetSidebarCollapsed(SidebarCollapsed);
            SelectPage(CurrentPage);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                if (updatePanel.Installing) e.Cancel = true;
                else if (!saving && HasPendingChanges)
                {
                    using (var prompt = new UnsavedChangesDialog(Icon))
                    {
                        var choice = prompt.ShowDialog(this);
                        if (choice == DialogResult.Yes)
                        {
                            if (SelectPreferences()) { saving = true; DialogResult = DialogResult.OK; }
                            else { SelectPage("general"); e.Cancel = true; }
                        }
                        else if (choice == DialogResult.No) { I18n.Use(originalPreferences.Language); Theme.Use(originalPreferences.ColorMode); DateDisplay.Preference=originalPreferences.Calendar; LanguagePreviewChanged?.Invoke(); }
                        else e.Cancel = true;
                    }
                }
            }
            base.OnFormClosing(e);
        }
        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessDialogKey(keyData);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { themeTimer?.Dispose(); sidebarAnimation?.Dispose(); sidebarHints.Dispose(); }
            base.Dispose(disposing);
            if (disposing) { logo?.Dispose(); gitLogo?.Dispose(); }
        }

        private void SaveDurations()
        {
            if (updatePanel.Installing) { SelectPage("updates"); return; }
            if (!SelectPreferences()) return;
            saving = true;
            DialogResult = DialogResult.OK;
        }
        private bool SelectPreferences()
        {
            if (!workDuration.TryGetSeconds(out int work) || work < 1 || work > Preferences.MaxWorkSeconds)
            {
                ShowValidationError(I18n.T("work_validation"));
                return false;
            }
            if (!breakDuration.TryGetSeconds(out int rest) || rest < 1 || rest > Preferences.MaxBreakSeconds)
            {
                ShowValidationError(I18n.T("break_validation"));
                return false;
            }

            SelectedPreferences = new Preferences(work, rest, showTaskbarCountdown.Checked,
                ((LanguageItem)language.SelectedItem).Code, ((LanguageItem)calendarChoice.SelectedItem).Code, ((LanguageItem)themeChoice.SelectedItem).Code);
            return true;
        }

        private void ShowValidationError(string message)
        {
            note.Text = message;
            note.Tag = "error";
            note.ForeColor = Theme.Error;
            generalBody.PerformLayout();
        }
    }
}
