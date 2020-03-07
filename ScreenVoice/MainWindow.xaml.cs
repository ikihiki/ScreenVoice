using Microsoft.Toolkit.Wpf.UI.XamlHost;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Windows.Foundation.Metadata;
using Windows.Graphics.Capture;

namespace ScreenVoice
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            SetupTimer();
        }
        // タイマのインスタンス
        private DispatcherTimer _timer;

        // タイマを設定する
        private void SetupTimer()
        {
            var dataContext = (MainWindowViewModel)DataContext;
            // タイマのインスタンスを生成
            _timer = new DispatcherTimer(); // 優先度はDispatcherPriority.Background
                                            // インターバルを設定
            _timer.Interval = TimeSpan.FromMilliseconds(dataContext.RefreshRate.Value);
            // タイマメソッドを設定
            _timer.Tick += new EventHandler(MyTimerMethod);
            // タイマを開始
            _timer.Start();

            // 画面が閉じられるときに、タイマを停止
            this.Closing += new CancelEventHandler(StopTimer);

            dataContext.RefreshRate.Subscribe(late => _timer.Interval = TimeSpan.FromMilliseconds(late));


        }
        // タイマメソッド
        private void MyTimerMethod(object sender, EventArgs e)
        {
            var dataContext = (MainWindowViewModel)DataContext;
            dataContext.E();
        }
        // タイマを停止
        private void StopTimer(object sender, CancelEventArgs e)
        {
            _timer.Stop();
        }
        private void WindowsXamlHost_ChildChanged(object sender, EventArgs e)
        {
            var host = (WindowsXamlHost)sender;
            var textBox = host.Child as App1.MyUserControl1;
            var dataContext = (MainWindowViewModel)DataContext;
            dataContext.E(textBox);
        }
    }

}
