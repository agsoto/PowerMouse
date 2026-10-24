using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerMouse.Windows.Tests;

[TestClass]
public sealed class SettingsLayoutTests
{
    [TestMethod]
    public void ExpanderItemsAcceptTheDefaultSettingsCardStyle()
    {
        var page = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "App", "SettingsPage.xaml"));
        XNamespace toolkit = "using:CommunityToolkit.WinUI.Controls";
        var itemLists = page.Descendants(toolkit + "SettingsExpander.Items").ToArray();
        Assert.IsNotEmpty(itemLists);
        foreach (var item in itemLists.SelectMany(list => list.Elements()))
        {
            // The toolkit assigns a SettingsCard style when realizing each item,
            // so incompatible children fail on expansion even if XAML compiles.
            Assert.AreEqual(toolkit + "SettingsCard", item.Name,
                $"{item.Name.LocalName} cannot receive SettingsExpander's default SettingsCard style. Put status messages outside Items.");
        }
    }
}
