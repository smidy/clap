using Avalonia.Controls;
using Avalonia.Controls.Templates;
using clawapp.Models;

namespace clawapp.Selectors;

public class ContentBlockDataTemplateSelector : IDataTemplate
{
    // Templates will be set from XAML resources
    public IDataTemplate? TextTemplate { get; set; }
    public IDataTemplate? ThinkingTemplate { get; set; }
    public IDataTemplate? ToolCallTemplate { get; set; }
    public IDataTemplate? FileTemplate { get; set; }
    public IDataTemplate? AudioTemplate { get; set; }
    public IDataTemplate? UnknownTemplate { get; set; }
    
    public Control? Build(object? param)
    {
        if (param is not ContentBlock block)
            return null;
            
        var template = block.Type switch
        {
            "text" => TextTemplate,
            "thinking" => ThinkingTemplate,
            "toolCall" => ToolCallTemplate,
            "file" => FileTemplate,
            "audio" => AudioTemplate,
            _ => UnknownTemplate
        };
        
        return template?.Build(param);
    }
    
    public bool Match(object? data) => data is ContentBlock;
}
