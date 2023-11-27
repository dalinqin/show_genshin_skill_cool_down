using System.Drawing;
using System.IO;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Brush = System.Windows.Media.Brush;
using Point = System.Drawing.Point;
using Timer = System.Timers.Timer;
using Window = System.Windows.Window;

//The program takes about 30ms in total to capture the screenshot, recognize the image, and display the numbers each time.

namespace Raiden;

using System;
using System.Collections.Generic;
using System.IO;

class IniFile
{
    private Dictionary<string, string> values = new Dictionary<string, string>();

    public IniFile(string filePath)
    {
        LoadValuesFromFile(filePath);
    }

    private void LoadValuesFromFile(string filePath)
    {
        foreach (var line in File.ReadAllLines(filePath))
        {
            // Skip comments and empty lines
            if (line.StartsWith(";") || string.IsNullOrWhiteSpace(line))
                continue;

            var keyValue = line.Split('=');
            if (keyValue.Length == 2)
            {
                values[keyValue[0].Trim()] = keyValue[1].Trim();
            }
        }
    }

    public string GetValue(string key, string defaultValue)
    {
        return values.TryGetValue(key, out var value) ? value : defaultValue;
    }

    public int GetIntValue(string key, int defaultValue)
    {
        if (values.TryGetValue(key, out var value))
        {
            if (int.TryParse(value, out var intValue))
            {
                return intValue;
            }
        }

        return defaultValue;
    }
}

public partial class MainWindow : Window
{
    //3840x2160 The resolution of the screen
    //3290,480 Starting coordinates for screenshot

    
    private static readonly int resolv_X = 3840;
    private static readonly int resolv_Y = 2160;
    private static readonly int crop_X = 3290;
    private static readonly int crop_Y = 480;

    private static readonly double duration = 0.5; //Duration in seconds

    //Button state
    private bool _isStarted = true;

    // Keyboard hook
    private readonly KeyboardHook _keyboardHook = new();

    // Four small rectangles, used to determine which character is selected
    private readonly Rectangle charRect0 = new(3720 - crop_X, 520 - crop_Y, 5, 10);
    private readonly Rectangle charRect1 = new(3720 - crop_X, 715 - crop_Y, 5, 10);
    private readonly Rectangle charRect2 = new(3720 - crop_X, 910 - crop_Y, 5, 10);
    private readonly Rectangle charRect3 = new(3720 - crop_X, 1110 - crop_Y, 5, 10);

    //Cooldown digits for each character
    private readonly decimal[] cooldown_digits = new decimal[4];
    private int current_char = -1;
    
    // Templates for numbers and dots
    private readonly Dictionary<int, Mat> numberTemplates;
    // Previous cooldown digits to check for changes
    private readonly decimal[] pre_cooldown_digits = new decimal[4];

    //Dictionary mapping character index to screen area
    private readonly Dictionary<int, Rectangle> Rect_area;

    // Overall screenshot area, including skills and character selection boxes on the bottom right of the screen
    private readonly Rectangle screenRect = new(crop_X, crop_Y, resolv_X - crop_X, resolv_Y - crop_Y);

    // Skill capture area
    private readonly Rectangle skillRect = new(3330 - crop_X, 1950 - crop_Y, 110, 70);

    // Timer for the application
    private readonly Timer timer;

    // Key strings from the INI file
    private string elementskillkeyString;
    private string showhidekeyString;
    
    // Key enums parsed from the strings
    private Key elementskillkey;
    private Key showhidekey;

    public MainWindow()
    {
        InitializeComponent();
        // 钩子
        _keyboardHook.KeyUP += KeyboardHook_KeyUp;
        _keyboardHook.Start();

        var iniFile = new IniFile("config.ini");

        elementskillkeyString = iniFile.GetValue("elementSkillKey", "E");
        showhidekeyString = iniFile.GetValue("HideShowKey", "E");
        Console.WriteLine(elementskillkeyString);
        Console.WriteLine(showhidekeyString);

        if (Enum.TryParse(elementskillkeyString, true, out Key elementKey))
        {
            elementskillkey = elementKey;
        }
        else
        {
            elementskillkey = Key.E; // 或任何您选择的默认键
        }

        if (Enum.TryParse(showhidekeyString, true, out Key hideKey))
        {
            showhidekey = hideKey;
        }
        else
        {
            Console.WriteLine("无法解析技能键配置项，使用默认值。");
            showhidekey = Key.F11; // 或任何您选择的默认键
        }

        Rect_area = new Dictionary<int, Rectangle>();

        Rect_area[0] = charRect0;
        Rect_area[1] = charRect1;
        Rect_area[2] = charRect2;
        Rect_area[3] = charRect3;

        //载入数字和点的模板
        // numberTemplates = new Dictionary<int, Mat>();
        // for (var i = 0; i <= 9; i++)
        //     numberTemplates[i] = Cv2.ImRead($@"C:\root\code\c#\numbers\{i}.png", ImreadModes.Grayscale);

        numberTemplates = new Dictionary<int, Mat>();
        for (var i = 0; i <= 9; i++)
        {
            using (Stream imageStream = Application
                       .GetResourceStream(new Uri($"pack://application:,,,/Raiden;component/image/{i}.png")).Stream)
            {
                Mat image = Mat.FromStream(imageStream, ImreadModes.Grayscale);
                numberTemplates[i] = image;
            }
        }

        timer = new Timer(duration * 1000); // duration是秒数，Timer期望的是毫秒数
        timer.Elapsed += OnTimedEvent; // 使用Elapsed事件而不是Tick
        timer.AutoReset = true; // 设置为True让定时器重复触发
        timer.Enabled = true; // 启动定时器
    }
    
    
    
    // Method to animate the background color for indication
    private void AnimateBackground(Border border)
    {
        // Set background color to yellow
        // ... (The rest of your method)
        var brush = new SolidColorBrush(Colors.Yellow);
        border.Background = brush;

        // 创建不透明度动画以实现闪烁效果
        var opacityAnimation = new DoubleAnimation
        {
            From = 1.0, // 完全不透明
            To = 0.0, // 完全透明
            Duration = TimeSpan.FromSeconds(0.5),     //repeat every 0.5 second
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(3) // repeat three times
        };

        // 动画完成后，将背景色变回透明
        opacityAnimation.Completed += (s, e) => { brush.Color = Colors.Transparent; };

        brush.BeginAnimation(Brush.OpacityProperty, opacityAnimation);
    }
    
    
    // Method to update the UI elements
    private void UpdateUI()
    {
        Dispatcher.Invoke(() =>
        {
            // 更新每个 TextBlock 的文本
            TextBlock1.Text = cooldown_digits[0].ToString("0.#");
            TextBlock2.Text = cooldown_digits[1].ToString("0.#");
            TextBlock3.Text = cooldown_digits[2].ToString("0.#");
            TextBlock4.Text = cooldown_digits[3].ToString("0.#");

            if (cooldown_digits[0] == 0 && pre_cooldown_digits[0] > 0) AnimateBackground(Border1);
            if (cooldown_digits[1] == 0 && pre_cooldown_digits[1] > 0) AnimateBackground(Border2);
            if (cooldown_digits[2] == 0 && pre_cooldown_digits[2] > 0) AnimateBackground(Border3);
            if (cooldown_digits[3] == 0 && pre_cooldown_digits[3] > 0) AnimateBackground(Border4);
        });
    }
    
    
    // Method to capture a specific screen area
    private Bitmap CaptureScreen(Rectangle area)
    {
        var bmp = new Bitmap(area.Width, area.Height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(area.Location, Point.Empty, area.Size);
        }

        return bmp;
    }
    
    
    // Timer event to update cooldowns and UI
    private void OnTimedEvent(object source, ElapsedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                //var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                //Console.WriteLine(Topmost);
                Topmost = true;

                for (var i = 0; i < cooldown_digits.Length; i++)
                {
                    pre_cooldown_digits[i] = cooldown_digits[i];
                    cooldown_digits[i] = Math.Max(0, cooldown_digits[i] - (decimal)duration);
                    UpdateUI();
                }

                //stopwatch.Stop();
                //Console.WriteLine($"Timer_Tick took {stopwatch.ElapsedMilliseconds} ms to execute.");
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex);
                UpdateUI(); // 发生异常时在UI显示0或错误信息
            }
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        timer.Stop();
        timer.Dispose();
        _keyboardHook.Dispose();
        base.OnClosed(e);
    }
    
    
    // Method to check if an image is completely white
    public bool IsImagePureWhite(Mat image)
    {

        if (image.Channels() != 1) Cv2.CvtColor(image, image, ColorConversionCodes.BGR2GRAY);

        var whitePixels = new Mat();
        Cv2.Threshold(image, whitePixels, 250, 255, ThresholdTypes.Binary);

        var nonWhitePixels = Cv2.CountNonZero(whitePixels);
        if (nonWhitePixels >= 45) return true;

        return false;
    }

    // Method called when the window is loaded
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            var dpiX = source.CompositionTarget.TransformToDevice.M11;
            var dpiY = source.CompositionTarget.TransformToDevice.M22;

            // 设定的屏幕坐标
            double screenX = 2970;
            double screenY = 440;
            //double screenY = 1600;

            // 调整坐标以考虑 DPI 缩放
            var adjustedX = screenX / dpiX;
            var adjustedY = screenY / dpiY;

            // 设置窗口位置
            Left = adjustedX;
            Top = adjustedY;
            Height = (1200 - 420) / dpiY;
            Width = (1200 - 450) / dpiY / 4;

            toggleButton.Content = $"hide  {showhidekeyString}";
        }
    }

    // Method to toggle the display of the window
    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isStarted)
        {
            //StartActions();
            actionBorder.Visibility = Visibility.Visible;
            toggleButton.Content = $"Hidden {showhidekeyString}";
        }
        else
        {
            actionBorder.Visibility = Visibility.Collapsed;
            toggleButton.Content = $"Show   {showhidekeyString}";
        }

        _isStarted = !_isStarted;
    }


    // Keyboard hook event to handle key presses
    private async void KeyboardHook_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == showhidekey)
            ToggleButton_Click(sender, e);

        if (_isStarted && (e.Key == elementskillkey || e.Key == Key.D1 || e.Key == Key.D2 || e.Key == Key.D3 ||
                           e.Key == Key.D4))
        {
            //var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            //Console.WriteLine("w pressed");
            //Thread.Sleep(500);
            
            // Asynchronously wait for 500 milliseconds to accurately capture the screen
            await Task.Delay(500); 

            using var bmpScreenCapture = CaptureScreen(screenRect);
            var croppedBitmap = bmpScreenCapture.Clone(skillRect, bmpScreenCapture.PixelFormat);


            using var skillMat = croppedBitmap.ToMat();

            Cv2.CvtColor(skillMat, skillMat, ColorConversionCodes.BGR2GRAY);

            //skillMat.ImWrite(@"c:/root/code/c#/error.png");

            // Determine the current character number
            for (var rec_number = 0; rec_number <= 3; rec_number++)
            {
                var charBitMap = bmpScreenCapture.Clone(Rect_area[rec_number], bmpScreenCapture.PixelFormat);
                var charMat = charBitMap.ToMat();

                if (!IsImagePureWhite(charMat))
                {
                    current_char = rec_number;
                    //Console.WriteLine($"current_char is :{current_char}");
                    break;
                }
            }

            // Identify the cooldown numbers of the skill

            var matches = new List<(Point, int)>();

            var foundNumbers = new HashSet<int>();

            foreach (var templatePair in numberTemplates)
            {
                var result = new Mat();
                Cv2.MatchTemplate(skillMat, templatePair.Value, result, TemplateMatchModes.CCoeffNormed);
                Cv2.Threshold(result, result, 0.85, 1.0, ThresholdTypes.Binary);
                var nonZeroCoordinates = new Mat();
                Cv2.FindNonZero(result, nonZeroCoordinates);
                var matchLocations = new Point[nonZeroCoordinates.Rows];

                for (var i = 0; i < nonZeroCoordinates.Rows; i++) matchLocations[i] = nonZeroCoordinates.At<Point>(i);
                //Console.WriteLine($"templatePair key is {templatePair.Key} ;i is: {i}   matchLOcation is:{matchLocations[i]}");
                foreach (var point in matchLocations)
                    // De-duplication: if this number has already been found, ignore it
                    if (!matches.Any(m => m.Item2 == templatePair.Key && Math.Abs(m.Item1.X - point.X) < 15))
                    {
                        matches.Add((point, templatePair.Key));
                        foundNumbers.Add(templatePair.Key);
                    }

                result.Dispose();
                nonZeroCoordinates.Dispose();

                // If 3 numbers have been found, end the matching immediately
                if (foundNumbers.Count == 3) break;
            }

            // Sort the match results
            var sortedMatches = matches.OrderBy(m => m.Item1.X).ToList();

            // Build the final number string
            var finalNumber = new StringBuilder();
            foreach (var match in sortedMatches) finalNumber.Append(match.Item2.ToString());

            // Insert a decimal point according to the number of digits
            if (finalNumber.Length >= 2)
                finalNumber.Insert(finalNumber.Length - 1, "."); // Insert a decimal point at the second to last position

            //Console.WriteLine($"Final number string: {finalNumber}");

            if (decimal.TryParse(finalNumber.ToString(), out var decimalValue))
            {
                //Console.WriteLine($"Parsed decimal value: {decimalValue}");

                pre_cooldown_digits[current_char] = cooldown_digits[current_char];
                cooldown_digits[current_char] = decimalValue;
                current_char = -1; // reset  current_char

                UpdateUI();
            }

            //stopwatch.Stop();
            //Console.WriteLine($"Timer_Tick took {stopwatch.ElapsedMilliseconds} ms to execute.");
        }
    }
}