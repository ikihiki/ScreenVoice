using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Graphics.Canvas;
using Model;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design.Serialization;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.ApplicationModel.VoiceCommands;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Networking.NetworkOperators;

namespace ScreenVoice
{
    public class MainWindowViewModel
    {
        public delegate bool EnumWindowsDelegate(IntPtr hWnd, IntPtr lparam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public extern static bool EnumWindows(EnumWindowsDelegate lpEnumFunc,
            IntPtr lparam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd,
            StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd,
            StringBuilder lpClassName, int nMaxCount);

        /// <summary>
        ///     Retrieves a handle to the Shell's desktop window.
        ///     <para>
        ///     Go to https://msdn.microsoft.com/en-us/library/windows/desktop/ms633512%28v=vs.85%29.aspx for more
        ///     information
        ///     </para>
        /// </summary>
        /// <returns>
        ///     C++ ( Type: HWND )<br />The return value is the handle of the Shell's desktop window. If no Shell process is
        ///     present, the return value is NULL.
        /// </returns>
        [DllImport("user32.dll")]
        static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);
        /// <summary>
        /// Retrieves the handle to the ancestor of the specified window.
        /// </summary>
        /// <param name="hwnd">A handle to the window whose ancestor is to be retrieved.
        /// If this parameter is the desktop window, the function returns NULL. </param>
        /// <param name="flags">The ancestor to be retrieved.</param>
        /// <returns>The return value is the handle to the ancestor window.</returns>
        [DllImport("user32.dll", ExactSpelling = true)]
        static extern IntPtr GetAncestor(IntPtr hwnd, GetAncestorFlags flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
        [DllImport("dwmapi.dll")]
        static extern int DwmGetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE dwAttribute, out ulong pvAttribute, int cbAttribute);
        [DllImport("Dll1.dll")]
        static extern bool CreateCaptureItemForWindow(IntPtr hwnd, out IntPtr result);
        const ulong DWM_CLOAKED_SHELL = 0x00000002;

        private static object lockObjct = new object();
        private static List<WindowInfo> windowList;

        private static bool EnumWindowCallBack(IntPtr hWnd, IntPtr lparam)
        {
            //ウィンドウのタイトルの長さを取得する
            int textLen = GetWindowTextLength(hWnd);
            if (0 < textLen)
            {

                //ウィンドウのタイトルを取得する
                StringBuilder tsb = new StringBuilder(textLen + 1);
                GetWindowText(hWnd, tsb, tsb.Capacity);

                //ウィンドウのクラス名を取得する
                StringBuilder csb = new StringBuilder(256);
                GetClassName(hWnd, csb, csb.Capacity);

                windowList.Add(new WindowInfo { Hwnd = hWnd, Title = tsb.ToString(), Class = csb.ToString() });
            }

            //すべてのウィンドウを列挙する

            return true;
        }
        static readonly IEnumerable<KnownWindow> BlackList = new[]
        {
            new KnownWindow{Title="Task View",Class="Windows.UI.Core.CoreWindow"},
            new KnownWindow{Title="DesktopWindowXamlSource",Class="Windows.UI.Core.CoreWindow"},
            new KnownWindow{Title="PopupHost",Class="Xaml_WindowedPopupClass"},
        };

        public ObservableCollection<WindowInfo> Windows { get; } = new ObservableCollection<WindowInfo>();
        public ReactiveProperty<WindowInfo> SelectedWindow { get; } = new ReactiveProperty<WindowInfo>();
        public ReactiveProperty<GraphicsCaptureItem> CurrentGraphicsCaptureItem { get; } = new ReactiveProperty<GraphicsCaptureItem>();

        public ReactiveProperty<BitmapSource> Bitmap { get; } = new ReactiveProperty<BitmapSource>();
        public ReadOnlyReactiveProperty<double> BitmapWidth => Bitmap.Select(bitmap => bitmap?.Width ?? 0).ToReadOnlyReactiveProperty();
        public ReadOnlyReactiveProperty<double> BitmapHeight => Bitmap.Select(bitmap => bitmap?.Height ?? 0).ToReadOnlyReactiveProperty();

        public ReactiveCommand StartCapture { get; }
        public ReactiveCommand RefreshWindow { get; } = new ReactiveCommand();
        public ReactiveCommand AddClip { get; } = new ReactiveCommand();
        public ReactiveCommand<ClipImageViewModel> RemoveClip { get; } = new ReactiveCommand<ClipImageViewModel>();

        public ReactiveCollection<ClipImageViewModel> ClipImages { get; } = new ReactiveCollection<ClipImageViewModel>();
        public ReactiveProperty<ClipImageViewModel> SelectedClipImage { get; } = new ReactiveProperty<ClipImageViewModel>();
        public BehaviorSubject<List<string>> RecognizedText { get; } = new BehaviorSubject<List<string>>(new List<string>());
        public ReactiveProperty<string> Script { get; } = new ReactiveProperty<string>();
        public ReadOnlyReactiveProperty<ScriptResult> ScriptReslut { get; }

        public ReactiveProperty<double> Speed { get; } = new ReactiveProperty<double>(1);
        public ReactiveProperty<double> Volume { get; } = new ReactiveProperty<double>(1);
        public ReactiveProperty<double> Pitch { get; } = new ReactiveProperty<double>(1);
        public ReactiveProperty<double> Intonation { get; } = new ReactiveProperty<double>(1);

        public ReactiveProperty<int> RefreshRate { get; } = new ReactiveProperty<int>(1000);

        public App1.MyUserControl1 util;
        HttpClient httpClient = new HttpClient();
        public MainWindowViewModel()
        {
            StartCapture = new ReactiveCommand();
            StartCapture.Subscribe(() =>
            {
                if (CreateCaptureItemForWindow(SelectedWindow.Value.Hwnd, out var result))
                {
                    var item = (GraphicsCaptureItem)Marshal.GetObjectForIUnknown(result);
                    CurrentGraphicsCaptureItem.Value = item;
                    util.StartCaptureInternal(item);
                }
            });

            AddClip.Subscribe(() =>
            {
                if (util == null)
                    return;
                var clip = new App1.ClipImage();
                var vm = new ClipImageViewModel(clip);
                var co = new App1.ClipImageConteir(clip);
                util.ClipImages.Add(clip, co);
                ClipImages.Add(vm);
                CreateRecogStream();
            });
            RemoveClip.Subscribe(vm =>
            {
                if (vm == null)
                {
                    return;
                }
                if (util != null)
                {
                    var co = util.ClipImages.GetValueOrDefault(vm.ClipImage);
                    util.ClipImages.Remove(vm.ClipImage);
                    co.Dispose();
                }
                if (SelectedClipImage.Value == vm)
                {
                    SelectedClipImage.Value = null;
                }
                ClipImages.Remove(vm);
                CreateRecogStream();
            });
            ScriptReslut = Observable.CombineLatest(Script, RecognizedText.Pairwise(), (script, pair) => (script, pair)).SelectMany(async scriptData =>
            {
                try
                {
                    var state = await CSharpScript.RunAsync<ScriptResult>(scriptData.script, globals: new ScriptModel(scriptData.pair.OldItem, scriptData.pair.NewItem));
                    return state.ReturnValue;
                }
                catch (Exception e)
                {
                    return new ScriptResult
                    {
                        IsRead = false,
                        Text = e.ToString()
                    };
                }
            }
            ).ToReadOnlyReactiveProperty();

            ScriptReslut.Where(result => result != null).Where(result => !string.IsNullOrWhiteSpace(result.Text)).Where(result => result.IsRead).Select(result => result.Text).Distinct().Subscribe(async text =>
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(new VoiceloidParam
                        {
                            Effects = new VoiceloidEffects
                            {
                                Volume = Volume.Value,
                                Speed = Speed.Value,
                                Pitch = Pitch.Value,
                                Intonation = Intonation.Value
                            },
                            Talktext = text
                        }, 
                        new System.Text.Json.JsonSerializerOptions { 
                            PropertyNamingPolicy= System.Text.Json.JsonNamingPolicy.CamelCase
                        });
                        var content = new StringContent(json, Encoding.UTF8, @"application/json");
                        try
                        {
                            await httpClient.PostAsync("http://localhost:7180//PLAY2/1700", content);
                        }
                        catch { }
                    });

            var byteArray = Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "SeikaServerUser", "SeikaServerPassword"));
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            RefreshWindow.Subscribe(() => RefreshWindows());
            RefreshWindows();
        }

        private IDisposable recSt;
        private void CreateRecogStream()
        {
            recSt?.Dispose();
            recSt = Observable.CombineLatest(ClipImages.Select(clip => clip.Text)).Subscribe(list => RecognizedText.OnNext(list.ToList()));
        }
        private IDisposable disposable;
        public void E(App1.MyUserControl1 util)
        {
            disposable?.Dispose();
            disposable =
             util?.Image.Where(image => image != null).Select(image =>
                BitmapSource.Create(image.SoftwareBitmap.PixelWidth, image.SoftwareBitmap.PixelHeight, image.SoftwareBitmap.DpiX, image.SoftwareBitmap.DpiY, PixelFormats.Bgra32, null, image.Bytes, image.SoftwareBitmap.PixelWidth * 4)
             ).Subscribe(image =>
             {
                 Bitmap.Value = image;
             });
            this.util = util;
        }

        public void E()
        {

            util?.RefreshImage();
        }

        private void RefreshWindows()
        {
            lock (lockObjct)
            {
                windowList = new List<WindowInfo>();
                EnumWindows(new EnumWindowsDelegate(EnumWindowCallBack), IntPtr.Zero);

                Windows.Clear();
                foreach (var window in windowList)
                {
                    if (IsCapturableWindow(window))
                    {
                        Windows.Add(window);
                    }
                }
            }
        }


        private bool IsCapturableWindow(WindowInfo window)
        {
            if (string.IsNullOrEmpty(window.Title) || window.Hwnd == GetShellWindow() ||
                !IsWindowVisible(window.Hwnd) || GetAncestor(window.Hwnd, GetAncestorFlags.GetRoot) != window.Hwnd)
            {
                return false;
            }

            var style = (WindowStyles)GetWindowLongPtr(window.Hwnd, (int)GWL.GWL_STYLE);
            if (style.HasFlag(WindowStyles.WS_DISABLED))
            {
                return false;
            }

            var exStyle = (WindowStylesEx)GetWindowLongPtr(window.Hwnd, (int)GWL.GWL_EXSTYLE);
            if (exStyle.HasFlag(WindowStylesEx.WS_EX_TOOLWINDOW))    // No tooltips
            {
                return false;
            }

            // Check to see if the window is cloaked if it's a UWP
            if (window.Class == "Windows.UI.Core.CoreWindow" ||
                window.Class == "ApplicationFrameWindow")
            {
                ulong cloaked;
                if (DwmGetWindowAttribute(window.Hwnd, DWMWINDOWATTRIBUTE.Cloaked, out cloaked, sizeof(ulong)) >= 0 && (cloaked == DWM_CLOAKED_SHELL))
                {
                    return false;
                }
            }

            // Unfortunate work-around. Not sure how to avoid this.
            if (BlackList.Any(known => known.Title == window.Title && known.Class == window.Class))
            {
                return false;
            }

            return true;
        }


    }

    public class WindowInfo
    {
        public IntPtr Hwnd { get; set; }
        public string Title { get; set; }
        public string Class { get; set; }
    }

    public class KnownWindow
    {
        public string Title { get; set; }
        public string Class { get; set; }
    }

    public class ClipImageViewModel
    {
        public ReactiveProperty<int> X => ClipImage.X;
        public ReactiveProperty<int> Y => ClipImage.Y;
        public ReactiveProperty<int> Height => ClipImage.Height;
        public ReactiveProperty<int> Width => ClipImage.Width;
        public ReadOnlyReactiveProperty<string> XText => ClipImage.X.Select(x => $"X: {x}").ToReadOnlyReactiveProperty();
        public ReadOnlyReactiveProperty<string> YText => ClipImage.Y.Select(x => $"Y: {x}").ToReadOnlyReactiveProperty();
        public ReadOnlyReactiveProperty<string> HeightText => ClipImage.Height.Select(x => $"Height: {x}").ToReadOnlyReactiveProperty();
        public ReadOnlyReactiveProperty<string> WidthText => ClipImage.Width.Select(x => $"Width: {x}").ToReadOnlyReactiveProperty();

        public App1.ClipImage ClipImage { get; }
        public ReactiveProperty<BitmapSource> Bitmap { get; }
        public ReadOnlyReactiveProperty<string> Text { get; }
        public OcrEngine ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages();

        public ClipImageViewModel(App1.ClipImage clipImage)
        {
            this.ClipImage = clipImage;
            Bitmap = clipImage.Image.Where(image => image != null).Select(image =>
      BitmapSource.Create(image.SoftwareBitmap.PixelWidth, image.SoftwareBitmap.PixelHeight, image.SoftwareBitmap.DpiX, image.SoftwareBitmap.DpiY, PixelFormats.Bgra32, null, image.Bytes, image.SoftwareBitmap.PixelWidth * 4)
  ).ToReactiveProperty();

            Text = clipImage.Image.Where(image => image != null)
                .SelectMany(async image =>
                    await ocrEngine.RecognizeAsync(image.SoftwareBitmap)
                ).Select(result => result.Text)
                .ToReadOnlyReactiveProperty();
        }

        //design time
        public ClipImageViewModel() : this(new App1.ClipImage())
        {


        }
    }
    public static class ObservablePairwiseExtensions
    {
        // OldNewPair<T>はReactivePropertyに入っています
        // using Codeplex.Reactive.Extensions;

        public static IObservable<OldNewPair<T>> Pairwise<T>(this IObservable<T> source)
        {
            return source.Scan(
                    new OldNewPair<T>(default(T), default(T)),
                    (pair, newValue) => new OldNewPair<T>(pair.NewItem, newValue))
                .Skip(1);
        }

        public static IObservable<TR> Pairwise<T, TR>(this IObservable<T> source, Func<T, T, TR> selector)
        {
            return source.Pairwise().Select(x => selector(x.OldItem, x.NewItem));
        }
    }

    public class VoiceloidParam
    {
        public string Talktext { get; set; }
        public VoiceloidEffects Effects { get; set; }

    }
    public class VoiceloidEffects
    {
        public double Speed { get; set; }
        public double Volume { get; set; }
        public double Pitch { get; set; }
        public double Intonation { get; set; }
    }
}
