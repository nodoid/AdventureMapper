namespace AdventureMapper.Views;

/// <summary>Tablet tab: the shared inspector.</summary>
public partial class InspectorTabPage : ContentPage
{
	public InspectorTabPage(InspectorView inspector)
	{
		InitializeComponent();
		InspectorHost.Content = inspector;
	}
}
