using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace clawapp.Controls;

/// <summary>
/// A control for displaying tool/function call invocations with expandable arguments.
/// </summary>
public class ToolCallView : TemplatedControl
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static readonly StyledProperty<string?> ToolIdProperty =
        AvaloniaProperty.Register<ToolCallView, string?>(nameof(ToolId));

    public static readonly StyledProperty<string?> ToolNameProperty =
        AvaloniaProperty.Register<ToolCallView, string?>(nameof(ToolName));

    public static readonly StyledProperty<object?> ArgumentsProperty =
        AvaloniaProperty.Register<ToolCallView, object?>(nameof(Arguments));

    public static readonly StyledProperty<string?> ResultProperty =
        AvaloniaProperty.Register<ToolCallView, string?>(nameof(Result));

    public static readonly StyledProperty<ToolCallStatus> StatusProperty =
        AvaloniaProperty.Register<ToolCallView, ToolCallStatus>(nameof(Status), defaultValue: ToolCallStatus.Pending);

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ToolCallView, bool>(nameof(IsExpanded), defaultValue: false);

    public static readonly StyledProperty<bool> ShowToolResultsProperty =
        AvaloniaProperty.Register<ToolCallView, bool>(nameof(ShowToolResults));

    public static readonly DirectProperty<ToolCallView, string> FormattedArgumentsProperty =
        AvaloniaProperty.RegisterDirect<ToolCallView, string>(
            nameof(FormattedArguments),
            o => o.FormattedArguments);

    public static readonly DirectProperty<ToolCallView, string> ToolIconProperty =
        AvaloniaProperty.RegisterDirect<ToolCallView, string>(
            nameof(ToolIcon),
            o => o.ToolIcon);

    private Button? _toggleButton;
    private Border? _resultBorder;
    private string _formattedArguments = "{}";
    private string _toolIcon = "🔧";

    static ToolCallView()
    {
        ArgumentsProperty.Changed.AddClassHandler<ToolCallView>((x, _) => x.UpdateFormattedArguments());
        ToolNameProperty.Changed.AddClassHandler<ToolCallView>((x, _) => x.UpdateToolIcon());
        ResultProperty.Changed.AddClassHandler<ToolCallView>((x, _) => x.UpdateResultVisibility());
        IsExpandedProperty.Changed.AddClassHandler<ToolCallView>((x, _) => x.UpdateResultVisibility());
        ShowToolResultsProperty.Changed.AddClassHandler<ToolCallView>((x, _) => x.UpdateResultVisibility());
    }

    public ToolCallView()
    {
    }

    /// <summary>
    /// The unique identifier for this tool call.
    /// </summary>
    public string? ToolId
    {
        get => GetValue(ToolIdProperty);
        set => SetValue(ToolIdProperty, value);
    }

    /// <summary>
    /// The name of the tool/function being called.
    /// </summary>
    public string? ToolName
    {
        get => GetValue(ToolNameProperty);
        set => SetValue(ToolNameProperty, value);
    }

    /// <summary>
    /// The arguments passed to the tool (JSON object).
    /// </summary>
    public object? Arguments
    {
        get => GetValue(ArgumentsProperty);
        set => SetValue(ArgumentsProperty, value);
    }

    /// <summary>
    /// The result returned from the tool execution.
    /// </summary>
    public string? Result
    {
        get => GetValue(ResultProperty);
        set => SetValue(ResultProperty, value);
    }

    /// <summary>
    /// The execution status of the tool call.
    /// </summary>
    public ToolCallStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <summary>
    /// Whether the arguments section is expanded.
    /// </summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    /// Whether to show the tool execution results.
    /// </summary>
    public bool ShowToolResults
    {
        get => GetValue(ShowToolResultsProperty);
        set => SetValue(ShowToolResultsProperty, value);
    }

    /// <summary>
    /// Pretty-printed JSON representation of Arguments.
    /// </summary>
    public string FormattedArguments
    {
        get => _formattedArguments;
        private set => SetAndRaise(FormattedArgumentsProperty, ref _formattedArguments, value);
    }

    /// <summary>
    /// Icon representing the tool type.
    /// </summary>
    public string ToolIcon
    {
        get => _toolIcon;
        private set => SetAndRaise(ToolIconProperty, ref _toolIcon, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_toggleButton != null)
        {
            _toggleButton.Click -= OnToggleButtonClick;
        }

        _toggleButton = e.NameScope.Find<Button>("PART_ToggleButton");
        if (_toggleButton != null)
        {
            _toggleButton.Click += OnToggleButtonClick;
        }

        _resultBorder = e.NameScope.Find<Border>("PART_ResultBorder");

        UpdateFormattedArguments();
        UpdateToolIcon();
        UpdateResultVisibility();
    }

    private void OnToggleButtonClick(object? sender, RoutedEventArgs e)
    {
        IsExpanded = !IsExpanded;
    }

    private void UpdateFormattedArguments()
    {
        if (Arguments == null)
        {
            FormattedArguments = "{}";
            return;
        }

        try
        {
            if (Arguments is JsonElement jsonElement)
            {
                FormattedArguments = JsonSerializer.Serialize(jsonElement, JsonOptions);
            }
            else if (Arguments is string str)
            {
                // Try to parse and reformat
                var parsed = JsonSerializer.Deserialize<JsonElement>(str);
                FormattedArguments = JsonSerializer.Serialize(parsed, JsonOptions);
            }
            else
            {
                FormattedArguments = JsonSerializer.Serialize(Arguments, JsonOptions);
            }
        }
        catch
        {
            FormattedArguments = Arguments.ToString() ?? "{}";
        }
    }

    private void UpdateToolIcon()
    {
        ToolIcon = GetToolIcon(ToolName);
    }

    private void UpdateResultVisibility()
    {
        if (_resultBorder != null)
        {
            // Show result if available and user setting allows it.
            // Note: We intentionally decoupled this from IsExpanded so "Show tool results"
            // acts as a global toggle for result visibility, while IsExpanded controls arguments.
            _resultBorder.IsVisible = !string.IsNullOrEmpty(Result) && ShowToolResults;
        }
    }

    /// <summary>
    /// Maps tool names to appropriate icons.
    /// </summary>
    private static string GetToolIcon(string? toolName)
    {
        if (string.IsNullOrEmpty(toolName))
            return "🔧";

        var name = toolName.ToLowerInvariant();

        return name switch
        {
            // Search tools
            _ when name.Contains("search") => "🔍",
            _ when name.Contains("find") => "🔍",
            _ when name.Contains("query") => "🔍",
            
            // Web/Browser tools
            _ when name.Contains("web") => "🌐",
            _ when name.Contains("browse") => "🌐",
            _ when name.Contains("url") => "🔗",
            _ when name.Contains("http") => "🌐",
            
            // File tools
            _ when name.Contains("file") => "📄",
            _ when name.Contains("read") => "📖",
            _ when name.Contains("write") => "✍️",
            _ when name.Contains("save") => "💾",
            _ when name.Contains("download") => "⬇️",
            _ when name.Contains("upload") => "⬆️",
            
            // Code tools
            _ when name.Contains("code") => "💻",
            _ when name.Contains("execute") => "▶️",
            _ when name.Contains("run") => "▶️",
            _ when name.Contains("compile") => "⚙️",
            
            // Database tools
            _ when name.Contains("database") => "🗄️",
            _ when name.Contains("sql") => "🗄️",
            _ when name.Contains("db") => "🗄️",
            
            // Communication tools
            _ when name.Contains("email") => "📧",
            _ when name.Contains("message") => "💬",
            _ when name.Contains("send") => "📤",
            
            // Analysis tools
            _ when name.Contains("analyze") => "📊",
            _ when name.Contains("calculate") => "🧮",
            _ when name.Contains("math") => "🧮",
            
            // Image tools
            _ when name.Contains("image") => "🖼️",
            _ when name.Contains("photo") => "📷",
            _ when name.Contains("vision") => "👁️",
            
            // Audio tools
            _ when name.Contains("audio") => "🔊",
            _ when name.Contains("speech") => "🎤",
            _ when name.Contains("voice") => "🎤",
            
            // Weather/Location
            _ when name.Contains("weather") => "🌤️",
            _ when name.Contains("location") => "📍",
            _ when name.Contains("map") => "🗺️",
            
            // Time tools
            _ when name.Contains("time") => "⏰",
            _ when name.Contains("calendar") => "📅",
            _ when name.Contains("schedule") => "📅",
            
            // Default
            _ => "🔧"
        };
    }
}

/// <summary>
/// Status of a tool call execution.
/// </summary>
public enum ToolCallStatus
{
    /// <summary>Tool call is pending/in progress.</summary>
    Pending,
    /// <summary>Tool call completed successfully.</summary>
    Success,
    /// <summary>Tool call failed with an error.</summary>
    Error
}
