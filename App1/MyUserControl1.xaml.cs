using Microsoft.Graphics.Canvas;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace App1
{
    public class ImageData
    {
        public SoftwareBitmap SoftwareBitmap;
        public byte[] Bytes;
    }

    public class ClipImage
    {
        public ReactiveProperty<int> X { get; } = new ReactiveProperty<int>(0);
        public ReactiveProperty<int> Y { get; } = new ReactiveProperty<int>(0);
        public ReactiveProperty<int> Height { get; } = new ReactiveProperty<int>(20);
        public ReactiveProperty<int> Width { get; } = new ReactiveProperty<int>(20);
        public BehaviorSubject<ImageData> Image { get; } = new BehaviorSubject<ImageData>(null);
    }

    public class ClipImageConteir : IDisposable
    {
        public ClipImage ClipImage { get; }
        CanvasRenderTarget renderTarget;
        bool needResize;
        public ClipImageConteir(ClipImage clipImage)
        {
            ClipImage = clipImage;
            Observable.CombineLatest(clipImage.Height, clipImage.Width)
                .Subscribe(list =>
                {
                    needResize = true;
                });
        }

        public void Resize(ICanvasResourceCreator resourceCreator, bool force)
        {
            if (needResize || force || renderTarget == null)
            {
                renderTarget?.Dispose();
                renderTarget = new CanvasRenderTarget(resourceCreator, ClipImage.Width.Value < 10 ? 10 : ClipImage.Width.Value, ClipImage.Height.Value < 10 ? 10 : ClipImage.Height.Value, 96);
                needResize = false;
            }
        }

        public void Rendar(CanvasBitmap canvasBitmap)
        {
            if (renderTarget != null)
            {
                using (CanvasDrawingSession ds = renderTarget.CreateDrawingSession())
                {
                    ds.Clear(Colors.Black);
                    ds.DrawImage(canvasBitmap, -ClipImage.X.Value, -ClipImage.Y.Value);
                }
            }
        }

        public void RefreshImage()
        {
            if (renderTarget != null)
            {
                var bytes = renderTarget.GetPixelBytes();
                var bitmap = SoftwareBitmap.CreateCopyFromBuffer(bytes.AsBuffer(), BitmapPixelFormat.Bgra8, (int)renderTarget.Size.Width, (int)renderTarget.Size.Height);
                ClipImage.Image.OnNext(new ImageData { SoftwareBitmap = bitmap, Bytes = bytes });
            }

        }

        public void Start(ICanvasResourceCreator resourceCreator)
        {
            Resize(resourceCreator, true);
        }
        public void Stop()
        {
            renderTarget?.Dispose();
            renderTarget = null;
        }
        public void Dispose()
        {
            renderTarget?.Dispose();
        }
    }

    public sealed partial class MyUserControl1 : UserControl
    {
        public MyUserControl1()
        {
            this.InitializeComponent();
        }
        private SizeInt32 _lastSize;
        private GraphicsCaptureItem _item;
        private Direct3D11CaptureFramePool _framePool;
        private GraphicsCaptureSession _session;

        // Non-API related members.
        private CanvasDevice _canvasDevice;
        private SoftwareBitmap bitmap;
        CanvasRenderTarget renderTarget;
        CanvasBitmap canvasBitmap;
        public BehaviorSubject<ImageData> Image { get; } = new BehaviorSubject<ImageData>(null);
        public Dictionary<ClipImage, ClipImageConteir> ClipImages { get; } = new Dictionary<ClipImage, ClipImageConteir>();
        public void StartCaptureInternal(GraphicsCaptureItem item)
        {

            // Stop the previous capture if we had one.
            StopCapture();
            _item = item;
            _lastSize = _item.Size;
            _canvasDevice = new CanvasDevice();
            _framePool = Direct3D11CaptureFramePool.Create(
               _canvasDevice, // D3D device
               DirectXPixelFormat.B8G8R8A8UIntNormalized, // Pixel format
               2, // Number of frames
               _item.Size); // Size of the buffers
            renderTarget = new CanvasRenderTarget(_canvasDevice, _item.Size.Width, _item.Size.Height, 96);
            foreach (var clip in ClipImages.Values)
            {
                clip.Start(_canvasDevice);
            }
            _framePool.FrameArrived += (s, a) =>
            {
                // The FrameArrived event is raised for every frame on the thread
                // that created the Direct3D11CaptureFramePool. This means we
                // don't have to do a null-check here, as we know we're the only
                // one dequeueing frames in our application.  

                // NOTE: Disposing the frame retires it and returns  
                // the buffer to the pool.

                using (var frame = _framePool.TryGetNextFrame())
                {
                    ProcessFrame(frame);
                }
            };

            _item.Closed += (s, a) =>
            {
                StopCapture();
            };

            _session = _framePool.CreateCaptureSession(_item);
            _session.StartCapture();
        }

        public void StopCapture()
        {
            foreach (var clip in ClipImages.Values)
            {
                clip.Stop();
            }
            renderTarget?.Dispose();
            _session?.Dispose();
            _framePool?.Dispose();
            _item = null;
            _session = null;
            _framePool = null;
            renderTarget = null;
        }

        private void ProcessFrame(Direct3D11CaptureFrame frame)
        {
            // Resize and device-lost leverage the same function on the
            // Direct3D11CaptureFramePool. Refactoring it this way avoids
            // throwing in the catch block below (device creation could always
            // fail) along with ensuring that resize completes successfully and
            // isn’t vulnerable to device-lost.
            bool needsReset = false;
            bool recreateDevice = false;

            if ((frame.ContentSize.Width != _lastSize.Width) ||
                (frame.ContentSize.Height != _lastSize.Height))
            {
                needsReset = true;
                _lastSize = frame.ContentSize;
            }

            try
            {
                canvasBitmap = CanvasBitmap.CreateFromDirect3D11Surface(
   _canvasDevice,
   frame.Surface);
                foreach (var clip in ClipImages.Values)
                {
                    clip.Resize(_canvasDevice, false);
                }

                using (CanvasDrawingSession ds = renderTarget.CreateDrawingSession())
                {
                    ds.Clear(Colors.Black);
                    ds.DrawImage(canvasBitmap);
                    ds.DrawRectangle(100, 200, 5, 6, Colors.Red);
                }
                foreach (var clip in ClipImages.Values)
                {
                    clip.Rendar(canvasBitmap);
                }
            }

            // This is the device-lost convention for Win2D.
            catch (Exception e) when (_canvasDevice.IsDeviceLost(e.HResult))
            {
                // We lost our graphics device. Recreate it and reset
                // our Direct3D11CaptureFramePool.  
                needsReset = true;
                recreateDevice = true;
            }

            if (needsReset)
            {
                ResetFramePool(frame.ContentSize, recreateDevice);
            }
        }

        public void RefreshImage()
        {
            if (renderTarget != null)
            {
                var bytes = renderTarget.GetPixelBytes();
                var bitmap = SoftwareBitmap.CreateCopyFromBuffer(bytes.AsBuffer(), BitmapPixelFormat.Bgra8, (int)renderTarget.Size.Width, (int)renderTarget.Size.Height);
                Image.OnNext(new ImageData { SoftwareBitmap = bitmap, Bytes = bytes });
            }
            foreach (var clip in ClipImages.Values)
            {
                clip.RefreshImage();
            }

        }

        private void ResetFramePool(SizeInt32 size, bool recreateDevice)
        {
            do
            {
                try
                {
                    if (recreateDevice)
                    {
                        _canvasDevice = new CanvasDevice();
                        foreach (var clip in ClipImages.Values)
                        {
                            clip.Resize(_canvasDevice, true);
                        }
                    }

                    _framePool.Recreate(
                        _canvasDevice,
                        DirectXPixelFormat.B8G8R8A8UIntNormalized,
                        2,
                        size);
                    renderTarget.Dispose();
                    renderTarget = new CanvasRenderTarget(_canvasDevice, size.Width, size.Height, 96);
                }
                // This is the device-lost convention for Win2D.
                catch (Exception e) when (_canvasDevice.IsDeviceLost(e.HResult))
                {
                    _canvasDevice = null;
                    recreateDevice = true;
                }
            } while (_canvasDevice == null);
        }



        private void OnRegionsInvalidated(Microsoft.Graphics.Canvas.UI.Xaml.CanvasVirtualControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasRegionsInvalidatedEventArgs args)
        {
            foreach (var region in args.InvalidatedRegions)
            {
                using (var ds = sender.CreateDrawingSession(region))
                {
                    // draw the region            if (canvasBitmap != null)
                    {
                        ds.DrawImage(canvasBitmap);
                    }
                    ds.DrawText("Hello, world!", 0, 0, Colors.Yellow);


                }
            }
        }
    }
}
