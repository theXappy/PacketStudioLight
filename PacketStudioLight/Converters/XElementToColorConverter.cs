using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;
using System.Xml.Linq;

namespace PacketStudioLight.Converters
{
    //MyColorsConverter
    public class XElementToColorConverter : IValueConverter
    {
        public const string ITEM_STATE_NORMAL = "normal";
        public const string ITEM_STATE_SELECTED = "selected";
        public const string ITEM_STATE_SELECTED_INACTIVE = "selected_inactive";

        XName showname = XName.Get("showname");


        // Heirarchy:
        // normal ->
        //          normal -> white
        //          error -> pink
        //          warn -> yellow
        //          layer -> gray
        // selected ->
        //          normal -> blue
        //          error -> pink-ish
        //          warn -> yellow-ish
        //          layer -> gray-ish
        // selected inactive -> ...
        static Dictionary<string, Dictionary<string, Brush>> stateToBrushes = new Dictionary<string, Dictionary<string, Brush>>();

        static XElementToColorConverter()
        {
            stateToBrushes[ITEM_STATE_NORMAL] = new Dictionary<string, Brush>
            {
                {"normal", Brushes.Transparent },
                {"error",  new SolidColorBrush(Color.FromRgb(255, 92, 92)) },
                {"warn", Brushes.Yellow },
                {"layer",  new SolidColorBrush(Color.FromRgb(70, 70, 70)) }
            };
            stateToBrushes[ITEM_STATE_SELECTED] = new Dictionary<string, Brush>
            {
                {"normal", new SolidColorBrush(Color.FromRgb(30, 144, 255)) }, // TODO: Make this transparent-ish blue instead of opaque
                {"error",  new SolidColorBrush(Color.FromRgb(205, 101, 124)) },
                {"warn",  new SolidColorBrush(Color.FromRgb(199, 222, 117)) },
                {"layer",  new SolidColorBrush(Color.FromRgb(44, 44, 44)) },
            };
            stateToBrushes[ITEM_STATE_SELECTED_INACTIVE] = new Dictionary<string, Brush>
            {
                {"normal", new SolidColorBrush(Color.FromRgb(65, 121, 177)) },
                {"error",  new SolidColorBrush(Color.FromRgb(217, 78, 78)) },
                {"warn",  new SolidColorBrush(Color.FromRgb(210, 206, 71)) },
                {"layer",  new SolidColorBrush(Color.FromRgb(55, 55, 55)) },
            };

        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string state = ITEM_STATE_NORMAL;

            XElement element = value as XElement;
            bool isLayer = false;
            if(element?.Name?.LocalName == "proto")
            {
                isLayer = true;
            }

            if(parameter is string strParam)
                state = strParam;

            System.Collections.Generic.List<string?>? erroElements = element?.DescendantsAndSelf().Select(x => x.Attribute(showname)?.Value).Where(x => x?.StartsWith("Severity level") == true).ToList();

            // Note that Errors/Warns override layers tint
            if (erroElements?.Contains("Severity level: Error") == true)
                return stateToBrushes[state]["error"];
            if (erroElements?.Contains("Severity level: Warning") == true)
                return stateToBrushes[state]["warn"];

            return stateToBrushes[state][isLayer ? "layer" : "normal"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
            //...
        }
    }
}
