using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
namespace GenericPadBridge;

// Resolution-independent icons use fixed view boxes, including the brand marks.
internal static class DashboardIcon
{
    public static FrameworkElement Create(string key,double size,Brush ink)
    {
        string? path=key switch
        {
            "\uE71B"=>"M9,15 L15,9 M9,8 L12,5 C16,1 23,7 19,11 L16,14 M15,16 L12,19 C8,23 1,17 5,13 L8,10",
            "\uE823"=>"M12,4 A8,8 0 1 1 11.99,4 M12,7 L12,12 L16,14",
            "\uE9D9"=>"M2,5 H7 M11,5 H22 M2,12 H14 M18,12 H22 M2,19 H5 M9,19 H22 M7,3 H11 V7 H7 Z M14,10 H18 V14 H14 Z M5,17 H9 V21 H5 Z",
            "\uE9D5"=>"M8,4 H5 V22 H20 V4 H17 M8,2 H17 V6 H8 Z M8,13 L11,16 L17,10",
            "\uE95E"=>"M1,12 H5 L8,3 L11,21 L14,6 L17,17 L20,10 H24",
            "\uE90F"=>"M3,2 L8,7 L7,10 L2,5 Z M7,10 L19,22 L22,19 L10,7 M13,7 L19,1 L19,5 L23,5 L22,10 L16,12 M12,15 L5,22 L2,19 L9,12",
            "\uEA4B"=>"M8,2 H16 M10,2 V10 L3,21 Q2,23 5,23 H20 Q23,23 21,20 L14,10 V2 M7,16 H18",
            "\uE768"=>"M12,2 A10,10 0 1 1 11.99,2 M9,7 L17,12 L9,17 Z",
            "\uE968"=>"M9,2 H15 V7 H9 Z M3,17 H9 V22 H3 Z M15,17 H21 V22 H15 Z M12,7 V12 M6,17 V12 H18 V17",
            "\uE890"=>"M1,12 Q12,-3 23,12 Q12,27 1,12 Z M12,7 A5,5 0 1 1 11.99,7",
            "\uE946"=>"M12,2 A10,10 0 1 1 11.99,2 M12,6 V7 M12,10 V17 M10,17 H14",
            "\uE7F4"=>"M2,3 H22 V17 H2 Z M12,17 V22 M7,22 H17",
            "\uE88E"=>"M12,22 V2 M9,5 L12,2 L15,5 M12,17 L6,13 V8 M6,8 A2,2 0 1 1 5.99,8 M12,13 L18,9 V5 M16,2 H20 V6 H16 Z M12,19 A2,2 0 1 1 11.99,19",
            "\uE7E8"=>"M12,1 V11 M7,4 A9,9 0 1 0 17,4",
            "\uE897"=>"M12,2 A10,10 0 1 1 11.99,2 M9,8 C9,4 17,4 16,9 C15,11 12,11 12,14 M12,17 V18",
            "\uE943"=>"M12,2 A10,10 0 1 1 11.99,2 M7,10 L6,5 L10,7 H14 L18,5 L17,10 C20,16 16,18 14,18 V22 M10,22 V18 C5,18 3,16 3,13",
            _=>null
        };
        var canvas=new Canvas{Width=24,Height=24};
        if(key=="▶")canvas.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse("M5,2 L22,12 L5,22 Z"),Fill=ink});
        else if(key=="\uE990")
        {
            var circle=new Ellipse{Width=24,Height=24,Fill=ink};canvas.Children.Add(circle);
            canvas.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse("M3,3 Q12,4 21,3 L15,10 Q21,16 23,23 L12,14 L1,23 Q3,16 9,10 Z"),Fill=ReferenceDashboard.Ink("#0064CF")});
        }
        else if(key=="\uE7FC")
        {
            path="M7,2 V17 L11,18 V6 C17,8 19,10 18,13 C22,12 23,7 18,5 Z M5,15 C-1,18 1,21 8,21 L13,19 M14,16 L21,14 C27,15 24,18 15,21";
            canvas.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse(path),Stroke=ink,StrokeThickness=1.7,StrokeLineJoin=PenLineJoin.Round});
        }
        else if(path!=null)canvas.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse(path),Stroke=ink,StrokeThickness=1.65,StrokeLineJoin=PenLineJoin.Round,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});
        else return new TextBlock{Text=key,FontFamily=new("Segoe MDL2 Assets"),FontSize=size,Foreground=ink,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
        return new Viewbox{Width=size,Height=size,Child=canvas,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
    }
}
