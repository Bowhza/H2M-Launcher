using System.Windows;
using System.Windows.Controls;

using H2MLauncher.UI.ViewModels;

namespace H2MLauncher.UI.View
{
    internal class ServerTabItemTemplateSelector : DataTemplateSelector
    {
        public required DataTemplate Custom { get; set; }
        public required DataTemplate Default { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            return item switch
            {
                CustomServerTabViewModel => Custom,
                IServerTabViewModel => Default,
                _ => base.SelectTemplate(item, container)
            };
        }
    }
}
