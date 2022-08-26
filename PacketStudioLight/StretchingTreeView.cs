using System.Windows;
using System.Windows.Controls;

namespace PacketStudioLight
{
    public class StretchingTreeView : TreeView
    {
        protected override DependencyObject GetContainerForItemOverride()
        {
            return new StretchingTreeViewItem()
            {
                Padding = new Thickness(3,1,0,1),
            };
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is StretchingTreeViewItem;
        }
    }
}
