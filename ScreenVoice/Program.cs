using System;
using System.Collections.Generic;
using System.Text;

namespace ScreenVoice
{
    public class Program
    {
        [System.STAThreadAttribute()]
        public static void Main()
        {
            using (new App1.App())
            {
                ScreenVoice.App app = new ScreenVoice.App();
                app.InitializeComponent();
                app.Run();
            }
        }
    }
}
