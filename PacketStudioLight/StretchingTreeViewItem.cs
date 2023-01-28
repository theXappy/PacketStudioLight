using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using PacketStudioLight.Converters;

namespace PacketStudioLight
{
    public class StretchingTreeViewItem : TreeViewItem
    {
        static XElementToColorConverter _colorConverter = new XElementToColorConverter();

        public StretchingTreeViewItem()
        {
            this.Loaded += new RoutedEventHandler(StretchingTreeViewItem_Loaded);
        }

        private void StretchingTreeViewItem_Loaded(object sender, RoutedEventArgs e)
        {
            var dataContext = (sender as FrameworkElement)?.DataContext;
            var xxx = _colorConverter.Convert(dataContext,
                typeof(Brush),
                XElementToColorConverter.ITEM_STATE_NORMAL,
                System.Globalization.CultureInfo.CurrentCulture) as Brush;
            Binding myBinding = new Binding();
            myBinding.Source = dataContext;
            myBinding.Converter = _colorConverter;
            this.SetBinding(TreeViewItem.BackgroundProperty, myBinding);

            Resources[SystemColors.HighlightBrushKey] = _colorConverter.Convert(dataContext,
                typeof(Brush),
                XElementToColorConverter.ITEM_STATE_SELECTED,
                System.Globalization.CultureInfo.CurrentCulture) as Brush;

            Resources[SystemColors.HighlightTextBrushKey] = Brushes.Black;
            Resources[SystemColors.InactiveSelectionHighlightBrushKey] = _colorConverter.Convert(dataContext,
                typeof(Brush),
                XElementToColorConverter.ITEM_STATE_SELECTED_INACTIVE,
                System.Globalization.CultureInfo.CurrentCulture) as Brush;

            if (this.VisualChildrenCount > 0)
            {
                Grid grid = this.GetVisualChild(0) as Grid;
                if (grid != null && grid.ColumnDefinitions.Count == 3)
                {
                    grid.ColumnDefinitions.RemoveAt(2);
                    grid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                }
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new StretchingTreeViewItem();
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            return item is StretchingTreeViewItem;
        }
    }
}
