using Microsoft.Maui.Controls.Xaml;

namespace AdventureMapper.Resources.Strings;

/// <summary>XAML access to the RESX strings: Text="{loc:Translate Menu_File}".</summary>
[ContentProperty(nameof(Key))]
public sealed class TranslateExtension : IMarkupExtension<string>
{
	public string Key { get; set; } = string.Empty;

	public string ProvideValue(IServiceProvider serviceProvider) => L.T(Key);

	object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
