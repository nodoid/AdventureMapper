using AdventureMapper.Resources.Strings;
using AdventureMapper.Views;

namespace AdventureMapper;

public partial class AppShell : Shell
{
	public AppShell(MapTabPage mapPage, InspectorTabPage inspectorPage)
	{
		InitializeComponent();
		FlyoutBehavior = FlyoutBehavior.Disabled;

		var tabs = new TabBar();
		tabs.Items.Add(new ShellContent
		{
			Title = L.T("Tab_Map"),
			Route = "map",
			Content = mapPage,
		});
		tabs.Items.Add(new ShellContent
		{
			Title = L.T("Tab_Details"),
			Route = "details",
			Content = inspectorPage,
		});

		Items.Add(tabs);
	}
}
