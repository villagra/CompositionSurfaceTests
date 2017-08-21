using Microsoft.Graphics.Canvas.Effects;
using Robmikh.CompositionSurfaceFactory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace CompositionTest.Controls
{
    public sealed partial class CrossFadeImage : UserControl
    {
        Compositor _compositor;
        SurfaceFactory _surfaceFactory;
        CompositionEffectBrush _crossFadeBrush;
        CompositionSurfaceBrush _previousSurfaceBrush;
        ScalarKeyFrameAnimation linearAnimation;
        CompositionScopedBatch _crossFadeBatch;

        private bool _firstImage = true;
        public Uri Source
        {
            set
            {
                if (_firstImage)
                {
                    BackgroundImage.Source = value;
                    _firstImage = false;
                }
                else
                {
                    ChangeImage(value);
                }
            }
        }

        public CrossFadeImage()
        {
            this.InitializeComponent();
            this.Loaded += PageBackground_Loaded;

            BackgroundImage.PlaceholderDelay = TimeSpan.FromMilliseconds(-1);
            BackgroundImage.SharedSurface = true;
        }

        public void MakeDarker()
        {
            backDrop.TintColor = Color.FromArgb(180, 0, 0, 0);
        }
        public void MakeClearer()
        {
            backDrop.TintColor = Color.FromArgb(10, 0, 0, 0);
        }


        private void PageBackground_Loaded(object sender, RoutedEventArgs e)
        {
            _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
            _surfaceFactory = SurfaceFactory.CreateFromCompositor(_compositor);

            // Create a crossfade brush to animate image transitions
            CrossFadeEffect effect = new CrossFadeEffect()
            {
                Name = "CrossFadeEffect",
                Source1 = new CompositionEffectSourceParameter("ImageSource"),
                Source2 = new CompositionEffectSourceParameter("ImageSource2")
            };

            CompositionEffectFactory factory = _compositor.CreateEffectFactory(effect, new[] { "CrossFadeEffect.CrossFade" });
            _crossFadeBrush = factory.CreateBrush();

            _previousSurfaceBrush = _compositor.CreateSurfaceBrush();

            // Create the animations for cross-fading
            linearAnimation = _compositor.CreateScalarKeyFrameAnimation();
            linearAnimation.InsertKeyFrame(0, 0);
            linearAnimation.InsertKeyFrame(1, 1);
            linearAnimation.Duration = TimeSpan.FromMilliseconds(450);
        }

        bool _inprogress = false;
        Uri _queue = null;
        private void ChangeImage(Uri uri)
        {
            if (_inprogress)
            {
                _queue = uri;
                return;
            }

            _inprogress = true;
            _previousSurfaceBrush.Surface = BackgroundImage.SurfaceBrush.Surface;
            _previousSurfaceBrush.CenterPoint = BackgroundImage.SurfaceBrush.CenterPoint;
            _previousSurfaceBrush.Stretch = BackgroundImage.SurfaceBrush.Stretch;

            // Load the new background image
            BackgroundImage.ImageOpened += BackgroundImage_ImageChanged;
            BackgroundImage.Source = uri;
        }

        private void BackgroundImage_ImageChanged(object sender, RoutedEventArgs e)
        {
            if (_crossFadeBatch == null)
            {
                // Create a batch object so we can cleanup when the cross-fade completes.
                _crossFadeBatch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

                // Set the sources
                _crossFadeBrush.SetSourceParameter("ImageSource", BackgroundImage.SurfaceBrush);
                _crossFadeBrush.SetSourceParameter("ImageSource2", _previousSurfaceBrush);
                _crossFadeBrush.StartAnimation("CrossFadeEffect.CrossFade", linearAnimation);

                // Update the image to use the cross fade brush
                BackgroundImage.Brush = _crossFadeBrush;

                _crossFadeBatch.Completed += Batch_CrossFadeCompleted;
                _crossFadeBatch.End();
            }

            // Unhook the handler
            BackgroundImage.ImageOpened -= BackgroundImage_ImageChanged;
        }

        private void Batch_CrossFadeCompleted(object sender, CompositionBatchCompletedEventArgs args)
        {
            BackgroundImage.Brush = BackgroundImage.SurfaceBrush;

            // Dispose the image
            ((CompositionDrawingSurface)_previousSurfaceBrush.Surface)?.Dispose();
            _previousSurfaceBrush.Surface = null;

            // Clear out the batch
            _crossFadeBatch.Dispose();
            _crossFadeBatch = null;

            GC.Collect();
            _inprogress = false;
            if (_queue != null)
            {
                var newUri = _queue;
                _queue = null;
                ChangeImage(newUri);
            }
        }
    }
}