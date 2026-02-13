using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace clawapp.Controls;

/// <summary>
/// A control for displaying file attachments with preview and download capabilities.
/// </summary>
public class FileAttachmentView : TemplatedControl
{
    public static readonly StyledProperty<string?> FileNameProperty =
        AvaloniaProperty.Register<FileAttachmentView, string?>(nameof(FileName));

    public static readonly StyledProperty<string?> MimeTypeProperty =
        AvaloniaProperty.Register<FileAttachmentView, string?>(nameof(MimeType));

    public static readonly StyledProperty<object?> ContentDataProperty =
        AvaloniaProperty.Register<FileAttachmentView, object?>(nameof(ContentData));

    public static readonly StyledProperty<long?> FileSizeProperty =
        AvaloniaProperty.Register<FileAttachmentView, long?>(nameof(FileSize));

    public static readonly DirectProperty<FileAttachmentView, string> FileIconProperty =
        AvaloniaProperty.RegisterDirect<FileAttachmentView, string>(
            nameof(FileIcon),
            o => o.FileIcon);

    public static readonly DirectProperty<FileAttachmentView, string> FileSizeDisplayProperty =
        AvaloniaProperty.RegisterDirect<FileAttachmentView, string>(
            nameof(FileSizeDisplay),
            o => o.FileSizeDisplay);

    public static readonly DirectProperty<FileAttachmentView, bool> IsImageProperty =
        AvaloniaProperty.RegisterDirect<FileAttachmentView, bool>(
            nameof(IsImage),
            o => o.IsImage);

    public static readonly DirectProperty<FileAttachmentView, Bitmap?> ImagePreviewProperty =
        AvaloniaProperty.RegisterDirect<FileAttachmentView, Bitmap?>(
            nameof(ImagePreview),
            o => o.ImagePreview);

    private Button? _downloadButton;
    private string _fileIcon = "📎";
    private string _fileSizeDisplay = "";
    private bool _isImage;
    private Bitmap? _imagePreview;

    static FileAttachmentView()
    {
        MimeTypeProperty.Changed.AddClassHandler<FileAttachmentView>((x, _) => x.UpdateFileIcon());
        MimeTypeProperty.Changed.AddClassHandler<FileAttachmentView>((x, _) => x.UpdateIsImage());
        FileSizeProperty.Changed.AddClassHandler<FileAttachmentView>((x, _) => x.UpdateFileSizeDisplay());
        ContentDataProperty.Changed.AddClassHandler<FileAttachmentView>((x, _) => x.UpdateImagePreview());
    }

    /// <summary>
    /// The original filename of the attachment.
    /// </summary>
    public string? FileName
    {
        get => GetValue(FileNameProperty);
        set => SetValue(FileNameProperty, value);
    }

    /// <summary>
    /// The MIME type of the file (e.g., "image/png", "application/pdf").
    /// </summary>
    public string? MimeType
    {
        get => GetValue(MimeTypeProperty);
        set => SetValue(MimeTypeProperty, value);
    }

    /// <summary>
    /// The file content (base64 string or URL).
    /// </summary>
    public object? ContentData
    {
        get => GetValue(ContentDataProperty);
        set => SetValue(ContentDataProperty, value);
    }

    /// <summary>
    /// The file size in bytes.
    /// </summary>
    public long? FileSize
    {
        get => GetValue(FileSizeProperty);
        set => SetValue(FileSizeProperty, value);
    }

    /// <summary>
    /// Icon representing the file type.
    /// </summary>
    public string FileIcon
    {
        get => _fileIcon;
        private set => SetAndRaise(FileIconProperty, ref _fileIcon, value);
    }

    /// <summary>
    /// Human-readable file size (e.g., "1.5 MB").
    /// </summary>
    public string FileSizeDisplay
    {
        get => _fileSizeDisplay;
        private set => SetAndRaise(FileSizeDisplayProperty, ref _fileSizeDisplay, value);
    }

    /// <summary>
    /// Whether the file is an image that can be previewed.
    /// </summary>
    public bool IsImage
    {
        get => _isImage;
        private set => SetAndRaise(IsImageProperty, ref _isImage, value);
    }

    /// <summary>
    /// Bitmap for image preview (if applicable).
    /// </summary>
    public Bitmap? ImagePreview
    {
        get => _imagePreview;
        private set => SetAndRaise(ImagePreviewProperty, ref _imagePreview, value);
    }

    /// <summary>
    /// Event raised when the download button is clicked.
    /// </summary>
    public event EventHandler<RoutedEventArgs>? DownloadRequested;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_downloadButton != null)
        {
            _downloadButton.Click -= OnDownloadButtonClick;
        }

        _downloadButton = e.NameScope.Find<Button>("PART_DownloadButton");
        if (_downloadButton != null)
        {
            _downloadButton.Click += OnDownloadButtonClick;
        }

        UpdateFileIcon();
        UpdateFileSizeDisplay();
        UpdateIsImage();
        UpdateImagePreview();
    }

    private void OnDownloadButtonClick(object? sender, RoutedEventArgs e)
    {
        DownloadRequested?.Invoke(this, e);
    }

    private void UpdateFileIcon()
    {
        FileIcon = GetFileIcon(MimeType);
    }

    private void UpdateFileSizeDisplay()
    {
        FileSizeDisplay = FormatFileSize(FileSize);
    }

    private void UpdateIsImage()
    {
        IsImage = MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
    }

    private void UpdateImagePreview()
    {
        if (!IsImage || ContentData == null)
        {
            ImagePreview = null;
            return;
        }

        try
        {
            byte[]? imageBytes = null;

            if (ContentData is string base64String)
            {
                // Handle data URI format: data:image/png;base64,xxxx
                if (base64String.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var commaIndex = base64String.IndexOf(',');
                    if (commaIndex > 0)
                    {
                        base64String = base64String[(commaIndex + 1)..];
                    }
                }

                imageBytes = Convert.FromBase64String(base64String);
            }
            else if (ContentData is byte[] bytes)
            {
                imageBytes = bytes;
            }

            if (imageBytes != null)
            {
                using var stream = new MemoryStream(imageBytes);
                ImagePreview = new Bitmap(stream);
            }
        }
        catch
        {
            // Failed to load image preview
            ImagePreview = null;
        }
    }

    /// <summary>
    /// Maps MIME types to appropriate icons.
    /// </summary>
    private static string GetFileIcon(string? mimeType)
    {
        if (string.IsNullOrEmpty(mimeType))
            return "📎";

        var type = mimeType.ToLowerInvariant();

        // Check main type first
        if (type.StartsWith("image/"))
            return "🖼️";
        if (type.StartsWith("audio/"))
            return "🔊";
        if (type.StartsWith("video/"))
            return "🎬";
        if (type.StartsWith("text/"))
            return "📄";

        // Check specific types
        return type switch
        {
            "application/pdf" => "📕",
            "application/msword" or 
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => "📘",
            "application/vnd.ms-excel" or 
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => "📊",
            "application/vnd.ms-powerpoint" or 
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => "📙",
            "application/zip" or 
            "application/x-rar-compressed" or 
            "application/x-7z-compressed" or
            "application/gzip" => "🗜️",
            "application/json" => "📋",
            "application/xml" or "text/xml" => "📋",
            "application/javascript" or "text/javascript" => "💻",
            _ => "📎"
        };
    }

    /// <summary>
    /// Formats file size in human-readable format.
    /// </summary>
    private static string FormatFileSize(long? bytes)
    {
        if (bytes == null || bytes <= 0)
            return "";

        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes.Value;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return order == 0 
            ? $"{len:0} {sizes[order]}" 
            : $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Sanitizes a filename by removing potentially dangerous characters.
    /// </summary>
    public static string SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "attachment";

        // Remove path separators and other dangerous characters
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));

        // Limit length
        if (sanitized.Length > 100)
            sanitized = sanitized[..100];

        return string.IsNullOrWhiteSpace(sanitized) ? "attachment" : sanitized;
    }
}
