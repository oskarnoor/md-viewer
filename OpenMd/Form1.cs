namespace OpenMd;

public partial class Form1 : Form
{
    private readonly Panel _viewHost = new();
    private readonly SplitContainer _splitView = new();
    private readonly TextBox _splitEditor = new();
    private readonly TextBox _editEditor = new();
    private readonly WebBrowser _splitPreview = new();
    private readonly WebBrowser _preview = new();
    private readonly ToolStripStatusLabel _statusLabel = new();
    private readonly ToolStripComboBox _themeSelector = new();
    private readonly List<ToolStripMenuItem> _themeMenuItems = [];
    private readonly System.Windows.Forms.Timer _renderTimer = new();

    private MenuStrip _menu = null!;
    private ToolStrip _toolbar = null!;
    private ToolStripButton _splitViewButton = null!;
    private ToolStripButton _previewViewButton = null!;
    private ToolStripButton _editViewButton = null!;
    private StatusStrip _statusStrip = null!;
    private AppTheme _currentTheme = AppThemes.Light;
    private ViewMode _currentView = ViewMode.Split;
    private bool _dirty;
    private bool _loading;
    private bool _syncingEditors;
    private bool _syncingThemeUi;
    private string? _currentPath;
    private string? _lastRenderedMarkdown;

    public Form1(string? initialPath = null)
    {
        InitializeComponent();
        BuildUi();

        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            TryLoadFile(initialPath);
        }
        else
        {
            NewDocument();
        }
    }

    private void BuildUi()
    {
        SuspendLayout();

        Text = "OpenMd";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(780, 520);
        Size = new Size(1120, 780);
        AllowDrop = true;
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        FormClosing += OnFormClosing;

        _menu = BuildMenu();
        _toolbar = BuildToolbar();

        _renderTimer.Interval = 300;
        _renderTimer.Tick += (_, _) =>
        {
            _renderTimer.Stop();
            RenderPreview();
        };

        ConfigureEditor(_splitEditor);
        ConfigureEditor(_editEditor);
        ConfigureBrowser(_splitPreview);
        ConfigureBrowser(_preview);

        _splitView.Dock = DockStyle.Fill;
        _splitView.Orientation = Orientation.Vertical;
        _splitView.BorderStyle = BorderStyle.None;
        var splitSized = false;
        _splitView.SizeChanged += (_, _) =>
        {
            if (splitSized || _splitView.Width < 560)
            {
                return;
            }

            _splitView.Panel1MinSize = 260;
            _splitView.Panel2MinSize = 260;
            _splitView.SplitterDistance = _splitView.Width / 2;
            splitSized = true;
        };
        _splitView.Panel1.Controls.Add(_splitEditor);
        _splitView.Panel2.Controls.Add(_splitPreview);

        _viewHost.Dock = DockStyle.Fill;
        _viewHost.Margin = Padding.Empty;
        _viewHost.Padding = Padding.Empty;

        _statusStrip = new StatusStrip();
        _statusStrip.Items.Add(_statusLabel);

        Controls.Add(_viewHost);
        Controls.Add(_statusStrip);
        Controls.Add(_toolbar);
        Controls.Add(_menu);
        MainMenuStrip = _menu;

        ApplyTheme(LoadSavedTheme(), persist: false, render: false);
        ShowView(ViewMode.Split);

        ResumeLayout();
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");

        file.DropDownItems.Add(new ToolStripMenuItem("&New", null, (_, _) => NewDocument(), Keys.Control | Keys.N));
        file.DropDownItems.Add(new ToolStripMenuItem("&Open...", null, (_, _) => OpenFromDialog(), Keys.Control | Keys.O));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(new ToolStripMenuItem("&Save", null, (_, _) => SaveCurrent(), Keys.Control | Keys.S));
        file.DropDownItems.Add(new ToolStripMenuItem("Save &As...", null, (_, _) => SaveAs(), Keys.Control | Keys.Shift | Keys.S));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(new ToolStripMenuItem("E&xit", null, (_, _) => Close(), Keys.Alt | Keys.F4));

        var view = new ToolStripMenuItem("&View");
        view.DropDownItems.Add(new ToolStripMenuItem("&Split", null, (_, _) => ShowView(ViewMode.Split), Keys.Control | Keys.D1));
        view.DropDownItems.Add(new ToolStripMenuItem("&Preview", null, (_, _) => ShowView(ViewMode.Preview), Keys.Control | Keys.D2));
        view.DropDownItems.Add(new ToolStripMenuItem("&Edit", null, (_, _) => ShowView(ViewMode.Edit), Keys.Control | Keys.D3));
        view.DropDownItems.Add(new ToolStripSeparator());

        var themes = new ToolStripMenuItem("&Theme");
        foreach (var theme in AppThemes.All)
        {
            var item = new ToolStripMenuItem(theme.Name, null, (_, _) => ApplyTheme(theme, persist: true));
            _themeMenuItems.Add(item);
            themes.DropDownItems.Add(item);
        }

        view.DropDownItems.Add(themes);

        menu.Items.Add(file);
        menu.Items.Add(view);
        return menu;
    }

    private ToolStrip BuildToolbar()
    {
        var toolbar = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            RenderMode = ToolStripRenderMode.System
        };

        toolbar.Items.Add(new ToolStripButton("Open", null, (_, _) => OpenFromDialog()) { ToolTipText = "Open Markdown file" });
        toolbar.Items.Add(new ToolStripButton("Save", null, (_, _) => SaveCurrent()) { ToolTipText = "Save current file" });
        toolbar.Items.Add(new ToolStripSeparator());
        _splitViewButton = new ToolStripButton("Split", null, (_, _) => ShowView(ViewMode.Split)) { ToolTipText = "Show editor and preview" };
        _previewViewButton = new ToolStripButton("Preview", null, (_, _) => ShowView(ViewMode.Preview)) { ToolTipText = "Show rendered Markdown" };
        _editViewButton = new ToolStripButton("Edit", null, (_, _) => ShowView(ViewMode.Edit)) { ToolTipText = "Show editor" };
        toolbar.Items.Add(_splitViewButton);
        toolbar.Items.Add(_previewViewButton);
        toolbar.Items.Add(_editViewButton);
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripLabel("Theme"));

        _themeSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _themeSelector.Width = 132;
        _themeSelector.ToolTipText = "Choose app theme";
        foreach (var theme in AppThemes.All)
        {
            _themeSelector.Items.Add(theme.Name);
        }

        _themeSelector.SelectedIndexChanged += (_, _) =>
        {
            if (_syncingThemeUi || _themeSelector.SelectedItem is not string themeName)
            {
                return;
            }

            ApplyTheme(AppThemes.FindByName(themeName), persist: true);
        };

        toolbar.Items.Add(_themeSelector);

        return toolbar;
    }

    private void ConfigureEditor(TextBox editor)
    {
        editor.AcceptsReturn = true;
        editor.AcceptsTab = true;
        editor.BorderStyle = BorderStyle.None;
        editor.Dock = DockStyle.Fill;
        editor.Font = new Font("Consolas", 10.5f);
        editor.Multiline = true;
        editor.ScrollBars = ScrollBars.Both;
        editor.WordWrap = false;
        editor.TextChanged += (_, _) => OnEditorTextChanged(editor);
    }

    private void ConfigureBrowser(WebBrowser browser)
    {
        browser.AllowWebBrowserDrop = false;
        browser.Dock = DockStyle.Fill;
        browser.ScriptErrorsSuppressed = true;
        browser.WebBrowserShortcutsEnabled = true;
        browser.Navigating += OnBrowserNavigating;
    }

    private void NewDocument()
    {
        if (!ConfirmSafeToContinue())
        {
            return;
        }

        _currentPath = null;
        SetEditorText("# Untitled Markdown\r\n\r\nStart typing in the editor. The preview updates automatically.\r\n");
        SetDirty(false);
        RenderPreview(force: true);
        UpdateTitleAndStatus();
    }

    private void OpenFromDialog()
    {
        if (!ConfirmSafeToContinue())
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Filter = "Markdown files (*.md;*.markdown)|*.md;*.markdown|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "Open Markdown File"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            TryLoadFile(dialog.FileName);
        }
    }

    private void TryLoadFile(string path)
    {
        try
        {
            _loading = true;
            _currentPath = Path.GetFullPath(path);
            SetEditorText(File.ReadAllText(_currentPath));
            _loading = false;
            SetDirty(false);
            RenderPreview(force: true);
            ShowView(ViewMode.Split);
        }
        catch (Exception ex)
        {
            _loading = false;
            MessageBox.Show(this, $"Could not open file:\r\n\r\n{ex.Message}", "OpenMd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateTitleAndStatus();
        }
    }

    private bool SaveCurrent()
    {
        if (string.IsNullOrWhiteSpace(_currentPath))
        {
            return SaveAs();
        }

        try
        {
            File.WriteAllText(_currentPath, _editEditor.Text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            SetDirty(false);
            UpdateTitleAndStatus();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save file:\r\n\r\n{ex.Message}", "OpenMd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private bool SaveAs()
    {
        using var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = "md",
            FileName = string.IsNullOrWhiteSpace(_currentPath) ? "Untitled.md" : Path.GetFileName(_currentPath),
            Filter = "Markdown files (*.md)|*.md|Markdown files (*.markdown)|*.markdown|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "Save Markdown File"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return false;
        }

        _currentPath = dialog.FileName;
        return SaveCurrent();
    }

    private bool ConfirmSafeToContinue()
    {
        if (!_dirty)
        {
            return true;
        }

        var result = MessageBox.Show(
            this,
            "Save changes before continuing?",
            "OpenMd",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning);

        return result switch
        {
            DialogResult.Yes => SaveCurrent(),
            DialogResult.No => true,
            _ => false
        };
    }

    private void SetEditorText(string text)
    {
        _syncingEditors = true;
        _splitEditor.Text = text;
        _editEditor.Text = text;
        _syncingEditors = false;
    }

    private void OnEditorTextChanged(TextBox source)
    {
        if (_loading || _syncingEditors)
        {
            return;
        }

        var target = ReferenceEquals(source, _splitEditor) ? _editEditor : _splitEditor;
        _syncingEditors = true;
        var selectionStart = Math.Min(target.SelectionStart, source.TextLength);
        target.Text = source.Text;
        target.SelectionStart = selectionStart;
        target.SelectionLength = 0;
        _syncingEditors = false;

        SetDirty(true);
        _renderTimer.Stop();
        _renderTimer.Start();
    }

    private void SetDirty(bool dirty)
    {
        _dirty = dirty;
        UpdateTitleAndStatus();
    }

    private void RenderPreview(bool force = false)
    {
        var markdown = _editEditor.Text;
        if (!force && string.Equals(markdown, _lastRenderedMarkdown, StringComparison.Ordinal))
        {
            return;
        }

        _lastRenderedMarkdown = markdown;
        var title = string.IsNullOrWhiteSpace(_currentPath) ? "Untitled" : Path.GetFileName(_currentPath);
        var baseDirectory = string.IsNullOrWhiteSpace(_currentPath) ? null : Path.GetDirectoryName(_currentPath);
        var html = MarkdownRenderer.ToHtmlDocument(markdown, title, baseDirectory, _currentTheme);
        SetBrowserHtml(_splitPreview, html);
        SetBrowserHtml(_preview, html);
    }

    private void ApplyTheme(AppTheme theme, bool persist, bool render = true)
    {
        _currentTheme = theme;

        BackColor = theme.AppBack;
        ForeColor = theme.ChromeFore;

        _viewHost.BackColor = theme.PreviewCanvas;

        _splitView.BackColor = theme.SubtleBorder;
        _splitView.Panel1.BackColor = theme.EditorBack;
        _splitView.Panel2.BackColor = theme.PreviewCanvas;

        ApplyEditorTheme(_splitEditor, theme);
        ApplyEditorTheme(_editEditor, theme);

        _splitPreview.BackColor = theme.PreviewCanvas;
        _preview.BackColor = theme.PreviewCanvas;

        ApplyToolStripTheme(_menu, theme);
        ApplyToolStripTheme(_toolbar, theme);
        ApplyToolStripTheme(_statusStrip, theme);
        _statusLabel.BackColor = theme.ChromeBack;
        _statusLabel.ForeColor = theme.ChromeFore;

        UpdateThemeControls();
        UpdateViewControls();

        if (persist)
        {
            SaveTheme(theme);
        }

        if (render)
        {
            RenderPreview(force: true);
        }
    }

    private static void ApplyEditorTheme(TextBox editor, AppTheme theme)
    {
        editor.BackColor = theme.EditorBack;
        editor.ForeColor = theme.EditorFore;
    }

    private static void ApplyToolStripTheme(ToolStrip strip, AppTheme theme)
    {
        strip.BackColor = theme.ChromeBack;
        strip.ForeColor = theme.ChromeFore;
        strip.RenderMode = ToolStripRenderMode.Professional;
        strip.Renderer = new ToolStripProfessionalRenderer(new ThemeColorTable(theme));
        ApplyToolStripItemTheme(strip.Items, theme);
    }

    private static void ApplyToolStripItemTheme(ToolStripItemCollection items, AppTheme theme)
    {
        foreach (ToolStripItem item in items)
        {
            item.BackColor = theme.ChromeBack;
            item.ForeColor = theme.ChromeFore;

            if (item is ToolStripComboBox combo)
            {
                combo.BackColor = theme.EditorBack;
                combo.ForeColor = theme.EditorFore;
                combo.FlatStyle = FlatStyle.Flat;
            }

            if (item is ToolStripMenuItem menuItem)
            {
                ApplyToolStripItemTheme(menuItem.DropDownItems, theme);
            }
        }
    }

    private void ShowView(ViewMode view)
    {
        _currentView = view;
        Control nextView = view switch
        {
            ViewMode.Split => _splitView,
            ViewMode.Preview => _preview,
            _ => _editEditor
        };

        if (_viewHost.Controls.Count == 1 && ReferenceEquals(_viewHost.Controls[0], nextView))
        {
            UpdateViewControls();
            return;
        }

        _viewHost.SuspendLayout();
        _viewHost.Controls.Clear();
        nextView.Dock = DockStyle.Fill;
        _viewHost.Controls.Add(nextView);
        _viewHost.ResumeLayout();
        UpdateViewControls();
    }

    private void UpdateViewControls()
    {
        if (_splitViewButton is null || _previewViewButton is null || _editViewButton is null)
        {
            return;
        }

        _splitViewButton.Checked = _currentView == ViewMode.Split;
        _previewViewButton.Checked = _currentView == ViewMode.Preview;
        _editViewButton.Checked = _currentView == ViewMode.Edit;
    }

    private void UpdateThemeControls()
    {
        _syncingThemeUi = true;

        foreach (var item in _themeMenuItems)
        {
            item.Checked = string.Equals(item.Text, _currentTheme.Name, StringComparison.OrdinalIgnoreCase);
        }

        _themeSelector.SelectedItem = _currentTheme.Name;
        _syncingThemeUi = false;
    }

    private static AppTheme LoadSavedTheme()
    {
        try
        {
            var path = GetThemeSettingsPath();
            return File.Exists(path) ? AppThemes.FindByName(File.ReadAllText(path).Trim()) : AppThemes.Light;
        }
        catch
        {
            return AppThemes.Light;
        }
    }

    private static void SaveTheme(AppTheme theme)
    {
        try
        {
            var path = GetThemeSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, theme.Name);
        }
        catch
        {
            // Theme persistence should never block editing or file opening.
        }
    }

    private static string GetThemeSettingsPath()
    {
        return Path.Combine(Application.UserAppDataPath, "theme.txt");
    }

    private static void SetBrowserHtml(WebBrowser browser, string html)
    {
        if (browser.Document is not null)
        {
            browser.Document.OpenNew(true);
            browser.Document.Write(html);
        }
        else
        {
            browser.DocumentText = html;
        }
    }

    private void UpdateTitleAndStatus()
    {
        var name = string.IsNullOrWhiteSpace(_currentPath) ? "Untitled.md" : Path.GetFileName(_currentPath);
        Text = $"{(_dirty ? "*" : string.Empty)}{name} - OpenMd";
        var fileText = string.IsNullOrWhiteSpace(_currentPath) ? "No file path yet" : _currentPath;
        _statusLabel.Text = $"{fileText}{(_dirty ? " - modified" : string.Empty)}";
    }

    private void OnBrowserNavigating(object? sender, WebBrowserNavigatingEventArgs e)
    {
        if (e.Url is null)
        {
            return;
        }

        if (e.Url.Scheme is "http" or "https" or "mailto")
        {
            e.Cancel = true;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Url.AbsoluteUri)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not open link:\r\n\r\n{ex.Message}", "OpenMd", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            return;
        }

        if (ConfirmSafeToContinue())
        {
            TryLoadFile(files[0]);
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!ConfirmSafeToContinue())
        {
            e.Cancel = true;
        }
    }

    private sealed class ThemeColorTable(AppTheme theme) : ProfessionalColorTable
    {
        public override Color ToolStripGradientBegin => theme.ChromeBack;
        public override Color ToolStripGradientMiddle => theme.ChromeBack;
        public override Color ToolStripGradientEnd => theme.ChromeBack;
        public override Color MenuStripGradientBegin => theme.ChromeBack;
        public override Color MenuStripGradientEnd => theme.ChromeBack;
        public override Color ToolStripDropDownBackground => theme.ChromeBack;
        public override Color ToolStripBorder => theme.Border;
        public override Color MenuBorder => theme.Border;
        public override Color MenuItemBorder => theme.Border;
        public override Color MenuItemSelected => theme.ChromeHover;
        public override Color MenuItemSelectedGradientBegin => theme.ChromeHover;
        public override Color MenuItemSelectedGradientEnd => theme.ChromeHover;
        public override Color MenuItemPressedGradientBegin => theme.ChromeHover;
        public override Color MenuItemPressedGradientMiddle => theme.ChromeHover;
        public override Color MenuItemPressedGradientEnd => theme.ChromeHover;
        public override Color ButtonSelectedGradientBegin => theme.ChromeHover;
        public override Color ButtonSelectedGradientMiddle => theme.ChromeHover;
        public override Color ButtonSelectedGradientEnd => theme.ChromeHover;
        public override Color ButtonPressedGradientBegin => theme.SelectionBack;
        public override Color ButtonPressedGradientMiddle => theme.SelectionBack;
        public override Color ButtonPressedGradientEnd => theme.SelectionBack;
        public override Color ButtonCheckedGradientBegin => theme.SelectionBack;
        public override Color ButtonCheckedGradientMiddle => theme.SelectionBack;
        public override Color ButtonCheckedGradientEnd => theme.SelectionBack;
        public override Color ButtonCheckedHighlight => theme.SelectionBack;
        public override Color ButtonCheckedHighlightBorder => theme.Border;
        public override Color ImageMarginGradientBegin => theme.ChromeBack;
        public override Color ImageMarginGradientMiddle => theme.ChromeBack;
        public override Color ImageMarginGradientEnd => theme.ChromeBack;
        public override Color SeparatorDark => theme.Border;
        public override Color SeparatorLight => theme.SubtleBorder;
    }

    private enum ViewMode
    {
        Split,
        Preview,
        Edit
    }
}
