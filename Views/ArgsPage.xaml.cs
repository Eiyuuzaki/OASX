using System.Globalization;
using System.IO;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OASX.Services;

namespace OASX.Views;

/// <summary>
/// 任务参数编辑页：按参数组分节渲染，编辑器按 type 映射，
/// 值变更即 PUT 保存（与 Flutter 版 putScriptArg 相同的接口）。
/// </summary>
public partial class ArgsPage : Page
{
    private string _script = "";
    private string _task = "";
    private readonly Dictionary<Control, (string Group, string Name, string Type)> _saveMap = [];

    /// <summary>用户点"返回任务列表"时触发（宿主切回概览）。</summary>
    public event Action? BackRequested;

    /// <summary>参数：(script, task)。宿主以实例导航传入。</summary>
    public ArgsPage(string script, string task)
    {
        InitializeComponent();
        _script = script;
        _task = task;
        TitleText.Text = $"{_script} / {I18nMenu.Tr(_task)} 参数";
        _ = LoadArgsAsync();
    }

    private async Task LoadArgsAsync()
    {
        StatusText.Text = "加载中…";
        GroupsPanel.Children.Clear();
        _saveMap.Clear();
        try
        {
            var groups = await OasArgsApi.GetTaskArgsAsync(_script, _task);
            foreach (var (group, args) in groups)
            {
                GroupsPanel.Children.Add(BuildGroupCard(group, args));
            }
            StatusText.Text = $"共 {groups.Count} 组参数，修改后自动保存";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"加载失败：{ex.Message}";
        }
    }

    private Border BuildGroupCard(string group, List<JsonElement> args)
    {
        var stack = new StackPanel();
        foreach (var arg in args)
        {
            var name = arg.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            // ⚠️ i18n 键是 API 的 name（serial='serial'），不是 title（"Serial"）：
            // Tr(name) → 模拟器 Serial；title 仅作回退
            var title = I18nMenu.Tr(name) != name ? I18nMenu.Tr(name)
                : (arg.TryGetProperty("title", out var t) && !string.IsNullOrEmpty(t.GetString())
                    ? t.GetString()! : name);
            var type = arg.TryGetProperty("type", out var ty) ? ty.GetString() ?? "string" : "string";
            var value = arg.TryGetProperty("value", out var v) ? JsonToString(v) : "";
            var desc = arg.TryGetProperty("description", out var de) ? de.GetString() ?? "" : "";
            desc = I18nMenu.Tr(desc);
            var enumOptions = arg.TryGetProperty("enumEnum", out var ee) && ee.ValueKind == JsonValueKind.Array
                ? ee.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                : null;

            stack.Children.Add(BuildArgRow(group, name, title, type, value, desc, enumOptions));
        }

        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 12),
            Background = FindBrush("ControlFillColorDefaultBrush"),
            BorderBrush = FindBrush("ControlStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        // 参数组名也是 i18n 键（device→模拟器设置）
                        Text = I18nMenu.Tr(group), FontSize = 14, FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 0, 0, 10),
                    },
                    stack,
                },
            },
        };
    }

    /// <summary>单行参数：左标签+说明，右编辑器（按 type 映射）。</summary>
    private UIElement BuildArgRow(string group, string name, string title, string type,
        string value, string desc, List<string>? enumOptions)
    {
        var label = new TextBlock
        {
            Text = title,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 180,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        if (!string.IsNullOrEmpty(desc) && desc != title)
        {
            label.ToolTip = desc;
        }

        // ⚠️ 用完整限定名：WPF-UI 包注入了全局 using，TextBox/TextBlock
        // 与 Wpf.Ui.Controls 的同名控件有二义性
        Control editor = type switch
        {
            "boolean" => BuildBooleanEditor(value == "true"),
            "enum" when enumOptions is { Count: > 0 } => BuildEnumEditor(value, enumOptions),
            "multi_line" => BuildTextEditor(value, multiline: true),
            "time" => BuildTextEditor(value, multiline: false, hint: "格式 HH:mm:ss"),
            "time_delta" => BuildTextEditor(value, multiline: false, hint: "格式 dd HH:mm:ss"),
            "date_time" => BuildTextEditor(value, multiline: false, hint: "格式 yyyy-MM-dd HH:mm:ss"),
            _ => BuildTextEditor(value, multiline: false),
        };
        editor.Margin = new Thickness(0, 2, 0, 2);
        editor.MinWidth = 260;

        _saveMap[editor] = (group, name, type);

        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(label, 0);
        Grid.SetColumn(editor, 1);
        grid.Children.Add(label);
        grid.Children.Add(editor);
        return grid;
    }

    private Control BuildTextEditor(string value, bool multiline, string? hint = null)
    {
        var box = new System.Windows.Controls.TextBox
        {
            Text = value,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinWidth = 260,
            MaxWidth = 420,
        };
        if (hint is not null) box.ToolTip = hint;
        if (multiline) box.MinHeight = 60;
        // 失焦即保存（TextChanged 每键都 PUT 太吵）
        box.LostFocus += async (_, _) =>
        {
            if (_saveMap.TryGetValue(box, out var meta))
            {
                await SaveArgAsync(meta.Group, meta.Name, meta.Type, box.Text);
            }
        };
        return box;
    }

    private Control BuildEnumEditor(string value, List<string> options)
    {
        var combo = new System.Windows.Controls.ComboBox { MinWidth = 260, ItemsSource = options };
        combo.SelectedIndex = options.IndexOf(value);
        combo.SelectionChanged += async (_, _) =>
        {
            if (combo.SelectedItem is string v && _saveMap.TryGetValue(combo, out var meta))
            {
                await SaveArgAsync(meta.Group, meta.Name, meta.Type, v);
            }
        };
        return combo;
    }

    private Control BuildBooleanEditor(bool value)
    {
        var check = new System.Windows.Controls.CheckBox { IsChecked = value };
        check.Checked += async (_, _) => await SaveFor(check);
        check.Unchecked += async (_, _) => await SaveFor(check);
        return check;

        async Task SaveFor(System.Windows.Controls.CheckBox c)
        {
            if (_saveMap.TryGetValue(c, out var meta))
            {
                await SaveArgAsync(meta.Group, meta.Name, meta.Type,
                    c.IsChecked == true ? "true" : "false");
            }
        }
    }

    private async Task SaveArgAsync(string group, string name, string type, string value)
    {
        if (type == "date_time" && !DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            StatusText.Text = $"{name} 格式应为 yyyy-MM-dd HH:mm:ss，未保存";
            return;
        }
        var ok = await OasArgsApi.PutArgValueAsync(_script, _task, group, name, type, value);
        StatusText.Text = ok
            ? $"{name} 已保存 ✓"
            : $"{name} 保存失败";
    }

    private static string JsonToString(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString() ?? "",
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => v.GetRawText(),
    };

    private static Brush FindBrush(string key) =>
        Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;

    private void BackToOverview_Click(object sender, RoutedEventArgs e) =>
        BackRequested?.Invoke();
}
