using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ShopDrawing.Plugin.Core;
using ShopDrawing.Plugin.Models;
using Panel = ShopDrawing.Plugin.Models.Panel;

namespace ShopDrawing.Plugin.UI
{
    public class WasteSuggestionDialog : Window
    {
        public bool UseFromStock { get; private set; }

        private static string JointSign(JointType j) => j switch
        {
            JointType.Male => "+",
            JointType.Female => "-",
            _ => "0"
        };

        private static string JointName(JointType j) => j switch
        {
            JointType.Male => "+ (Dương)",
            JointType.Female => "- (Âm)",
            _ => "0 (Cắt)"
        };

        public WasteSuggestionDialog(Panel needed, WastePanel found, MatchDirection direction = MatchDirection.Direct)
        {
            Title = "Gợi ý tái sử dụng tấm lẻ";
            Width = 480;
            Height = 420;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(241, 242, 246));
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid { Margin = new Thickness(20) };
            var stack = new StackPanel();
            grid.Children.Add(stack);

            stack.Children.Add(new TextBlock
            {
                Text = "KHO CÓ TẤM PHÙ HỢP",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(41, 128, 185)),
                Margin = new Thickness(0, 0, 0, 12)
            });

            string neededJoints = $"{JointSign(needed.JointLeft)}/{JointSign(needed.JointRight)}";
            stack.Children.Add(CreateInfoBlock(
                "Tấm cần:",
                $"Kích thước: {needed.WidthMm:F0} x {needed.LengthMm:F0} x {needed.ThickMm} mm",
                $"Ngàm: Trái {JointName(needed.JointLeft)} | Phải {JointName(needed.JointRight)}",
                Color.FromRgb(52, 73, 94)));

            string foundJoints = $"{JointSign(found.JointLeft)}/{JointSign(found.JointRight)}";
            stack.Children.Add(CreateInfoBlock(
                "Tấm kho:",
                $"Mã: {found.PanelCode} | KT: {found.WidthMm:F0} x {found.LengthMm:F0} x {found.ThickMm} mm",
                $"Ngàm: Trái {JointName(found.JointLeft)} | Phải {JointName(found.JointRight)}",
                Color.FromRgb(39, 174, 96)));

            stack.Children.Add(new TextBlock
            {
                Text = $"Nguồn: {found.SourceWall} | Dự án: {found.Project}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141)),
                Margin = new Thickness(0, 0, 0, 10)
            });

            bool jointMatch = found.JointLeft == needed.JointLeft && found.JointRight == needed.JointRight;
            bool jointFlipped = found.JointRight == needed.JointLeft && found.JointLeft == needed.JointRight;

            if (jointMatch)
            {
                stack.Children.Add(CreateStatusBanner(
                    "Ngàm khớp hoàn hảo",
                    "Ngàm tấm kho trùng khớp với vị trí cần lắp. Không cần điều chỉnh.",
                    Color.FromRgb(39, 174, 96)));
            }
            else if (jointFlipped)
            {
                stack.Children.Add(CreateStatusBanner(
                    "Ngàm khớp khi lật tấm",
                    "Lật 180 độ: Trái sang Phải. Toàn bộ dãy tường sẽ đổi chiều ngàm.",
                    Color.FromRgb(230, 126, 34)));
            }
            else
            {
                stack.Children.Add(CreateStatusBanner(
                    "Ngàm không khớp",
                    $"Tấm kho [{foundJoints}] khác vị trí cần [{neededJoints}]. Có thể lắp nhưng cần cắt chỉnh mối nối.",
                    Color.FromRgb(192, 57, 43)));
            }

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            stack.Children.Add(footer);

            string reuseLabel;
            if (jointMatch)
            {
                reuseLabel = "Dùng tấm này";
            }
            else if (jointFlipped)
            {
                reuseLabel = "Dùng và đổi chiều";
            }
            else
            {
                reuseLabel = "Dùng (cần chỉnh ngàm)";
            }

            var btnReuse = new Button
            {
                Content = reuseLabel,
                MinWidth = 170,
                Height = 38,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(41, 128, 185)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13
            };
            btnReuse.Click += (_, _) => { UseFromStock = true; DialogResult = true; };

            var btnNew = new Button
            {
                Content = "Cắt tấm mới",
                MinWidth = 120,
                Height = 38,
                FontSize = 13
            };
            btnNew.Click += (_, _) => { UseFromStock = false; DialogResult = true; };

            footer.Children.Add(btnReuse);
            footer.Children.Add(btnNew);

            Content = grid;
            UiText.NormalizeWindow(this);
        }

        private Border CreateInfoBlock(string title, string line1, string line2, Color accentColor)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(accentColor)
            });
            sp.Children.Add(new TextBlock
            {
                Text = line1,
                FontSize = 12,
                Margin = new Thickness(8, 2, 0, 0)
            });
            sp.Children.Add(new TextBlock
            {
                Text = line2,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(8, 2, 0, 0)
            });

            return new Border
            {
                BorderBrush = new SolidColorBrush(accentColor),
                BorderThickness = new Thickness(3, 0, 0, 0),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 8),
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                Child = sp
            };
        }

        private Border CreateStatusBanner(string title, string detail, Color accentColor)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            sp.Children.Add(new TextBlock
            {
                Text = detail,
                FontSize = 11,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0)
            });

            return new Border
            {
                Background = new SolidColorBrush(accentColor),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 8),
                Child = sp
            };
        }
    }
}
