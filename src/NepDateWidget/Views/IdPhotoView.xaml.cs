using ImageMagick;
using NepDateWidget.ViewModels;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace NepDateWidget.Views;

public partial class IdPhotoView : UserControl
{
    // ── State ────────────────────────────────────────────────────────────────

    private IdPhotoViewModel? _vm;

    // Rendered image bounds within CropCanvas coordinate space
    private double _imgOffsetX;
    private double _imgOffsetY;
    private double _imgRenderedW;
    private double _imgRenderedH;

    // Current crop frame in CropCanvas coordinates
    private double _cropX;
    private double _cropY;
    private double _cropW;
    private double _cropH;

    // Drag state
    private bool _isDragging;
    private Point _dragStart;
    private double _cropXAtDragStart;
    private double _cropYAtDragStart;

    // Normalized crop values — local copy so we can restore display coords after container resize
    // without touching the VM (which holds the same values for the export path).
    private double _normX;
    private double _normY;
    private double _normW;
    private double _normH;

    // True once ResetCropFrame() has completed at least once for the current photo+preset.
    // When true, SizeChanged restores display coords from normalized values instead of resetting.
    private bool _cropInitialized;

    // Browse click guard (same pattern as ResizeView)
    private bool _clickStartedHere;

    // ── Construction ─────────────────────────────────────────────────────────

    public IdPhotoView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
        DragEnter += OnDragEnter;
        Drop += OnDrop;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.CropResetRequested -= OnCropResetRequested;
            _vm.PropertyChanged   -= OnVmPropertyChanged;
        }

        _vm = e.NewValue as IdPhotoViewModel;

        if (_vm is not null)
        {
            _vm.CropResetRequested += OnCropResetRequested;
            _vm.PropertyChanged   += OnVmPropertyChanged;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }

        _vm.CropResetRequested -= OnCropResetRequested;
        _vm.PropertyChanged   -= OnVmPropertyChanged;
        _vm = null;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IdPhotoViewModel.CropShape))
        {
            // Refresh overlay immediately when shape toggles — no full reset needed.
            Dispatcher.BeginInvoke(UpdateCropOverlay);
        }
    }

    // ── Drop zone click-to-browse (same guard as ResizeView) ─────────────────

    private void DropZone_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _clickStartedHere = true;
    }

    private void DropZone_Click(object sender, MouseButtonEventArgs e)
    {
        bool wasClick = _clickStartedHere;
        _clickStartedHere = false;

        if (!wasClick)
        {
            return;
        }

        _vm?.BrowseCommand.Execute(null);
    }

    // ── Drag-drop onto the UserControl ───────────────────────────────────────

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files is null || files.Length == 0)
        {
            return;
        }

        _clickStartedHere = false;
        _vm?.SetPhoto(files[0]);
        e.Handled = true;
    }

    // ── Crop reset (triggered by VM when photo or preset changes) ─────────────

    private void OnCropResetRequested()
    {
        _cropInitialized = false;
        Dispatcher.BeginInvoke(() =>
        {
            LoadCropImage();
            ResetCropFrame();
        });
    }

    private void LoadCropImage()
    {
        if (_vm is null || string.IsNullOrEmpty(_vm.PhotoPath))
        {
            CropImage.Source = null;
            return;
        }

        try
        {
            // Load via Magick.NET and call AutoOrient() so EXIF rotation is baked into pixels
            // before display. This ensures the crop overlay coordinates match what ExportSinglePhoto
            // will produce (which also calls AutoOrient). Without this, portrait photos from phones
            // (physically landscape with EXIF rotation) show and crop in the wrong orientation.
            using var mag = new MagickImage(_vm.PhotoPath);
            mag.AutoOrient();
            // Downsample to display size to keep memory usage low
            if (mag.Width > 800 || mag.Height > 800)
            {
                mag.Resize(new MagickGeometry(800, 800));
            }
            using var ms = new MemoryStream();
            mag.Write(ms, MagickFormat.Bmp);
            ms.Seek(0, SeekOrigin.Begin);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = ms;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            CropImage.Source = bmp;
        }
        catch
        {
            CropImage.Source = null;
        }
    }

    // ── Layout change: recompute rendered image bounds and crop frame ─────────

    private void CropContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_cropInitialized)
        {
            RestoreCropFromNorm();
        }
        else
        {
            ComputeRenderedBounds();
            ResetCropFrame();
        }
    }

    private void ComputeRenderedBounds()
    {
        if (CropImage.Source is not BitmapSource src || src.PixelWidth == 0 || src.PixelHeight == 0)
        {
            _imgOffsetX = 0;
            _imgOffsetY = 0;
            _imgRenderedW = CropContainer.ActualWidth;
            _imgRenderedH = CropContainer.ActualHeight;
            return;
        }

        double containerW = CropContainer.ActualWidth;
        double containerH = CropContainer.ActualHeight;

        if (containerW <= 0 || containerH <= 0)
        {
            return;
        }

        double imageAspect = (double)src.PixelWidth / src.PixelHeight;
        double containerAspect = containerW / containerH;

        if (imageAspect > containerAspect)
        {
            // Letterbox vertically
            _imgRenderedW = containerW;
            _imgRenderedH = containerW / imageAspect;
            _imgOffsetX   = 0;
            _imgOffsetY   = (containerH - _imgRenderedH) / 2.0;
        }
        else
        {
            // Letterbox horizontally
            _imgRenderedH = containerH;
            _imgRenderedW = containerH * imageAspect;
            _imgOffsetY   = 0;
            _imgOffsetX   = (containerW - _imgRenderedW) / 2.0;
        }

        // Size the crop canvas to cover the full container
        CropCanvas.Width  = containerW;
        CropCanvas.Height = containerH;
    }

    private void ResetCropFrame()
    {
        if (_vm is null)
        {
            return;
        }

        ComputeRenderedBounds();

        if (_imgRenderedW <= 0 || _imgRenderedH <= 0)
        {
            return;
        }

        double targetW = _vm.TargetWidthMm;
        double targetH = _vm.TargetHeightMm;

        if (targetW <= 0 || targetH <= 0)
        {
            return;
        }

        double aspect = targetW / targetH;

        // Size the crop frame to 82% of the constraining dimension
        double maxFrameW = _imgRenderedW * 0.82;
        double maxFrameH = _imgRenderedH * 0.82;

        if (maxFrameW / maxFrameH > aspect)
        {
            // Height-constrained
            _cropH = maxFrameH;
            _cropW = _cropH * aspect;
        }
        else
        {
            // Width-constrained
            _cropW = maxFrameW;
            _cropH = _cropW / aspect;
        }

        // Center within the rendered image
        _cropX = _imgOffsetX + (_imgRenderedW - _cropW) / 2.0;
        _cropY = _imgOffsetY + (_imgRenderedH - _cropH) / 2.0;

        UpdateCropOverlay();
        PushNormalizedCrop();
        _cropInitialized = true;
    }

    // Recomputes display crop coordinates from the stored normalized values after a container
    // resize. Does not re-center — preserves the user's crop position.
    private void RestoreCropFromNorm()
    {
        ComputeRenderedBounds();

        if (_imgRenderedW <= 0 || _imgRenderedH <= 0 || _normW <= 0 || _normH <= 0)
        {
            ResetCropFrame();
            return;
        }

        _cropW = _normW * _imgRenderedW;
        _cropH = _normH * _imgRenderedH;
        _cropX = _imgOffsetX + _normX * _imgRenderedW;
        _cropY = _imgOffsetY + _normY * _imgRenderedH;

        UpdateCropOverlay();
        // Norm values did not change — no PushNormalizedCrop needed.
    }

    private void UpdateCropOverlay()
    {
        double canvasW = CropCanvas.Width;
        double canvasH = CropCanvas.Height;

        // NaN check required: CropCanvas.Width returns double.NaN when Width has not been
        // explicitly set. double.NaN <= 0 evaluates to false in C#, so the plain <= 0
        // guard would not catch it.
        if (double.IsNaN(canvasW) || canvasW <= 0 || double.IsNaN(canvasH) || canvasH <= 0)
        {
            return;
        }

        bool isCircle = _vm?.CropShape == 1;

        if (isCircle)
        {
            // Hide the four rectangles and the square border.
            OvTop.Visibility    = Visibility.Collapsed;
            OvBottom.Visibility = Visibility.Collapsed;
            OvLeft.Visibility   = Visibility.Collapsed;
            OvRight.Visibility  = Visibility.Collapsed;
            CropFrame.Visibility = Visibility.Collapsed;

            // Build a rect-minus-ellipse (EvenOdd) path for the dark mask.
            var canvasRect  = new RectangleGeometry(new Rect(0, 0, canvasW, canvasH));
            var cropEllipse = new EllipseGeometry(new Rect(_cropX, _cropY, _cropW, _cropH));
            var group = new GeometryGroup { FillRule = System.Windows.Media.FillRule.EvenOdd };
            group.Children.Add(canvasRect);
            group.Children.Add(cropEllipse);
            OvCircle.Data = group;
            OvCircle.Visibility = Visibility.Visible;

            // Position and size the circle frame indicator.
            Canvas.SetLeft(CropCircle, _cropX);
            Canvas.SetTop(CropCircle, _cropY);
            CropCircle.Width  = _cropW;
            CropCircle.Height = _cropH;
            CropCircle.Visibility = Visibility.Visible;

            return;
        }

        // Square mode — restore elements and run the original four-rectangle logic.
        OvTop.Visibility    = Visibility.Visible;
        OvBottom.Visibility = Visibility.Visible;
        OvLeft.Visibility   = Visibility.Visible;
        OvRight.Visibility  = Visibility.Visible;
        CropFrame.Visibility  = Visibility.Visible;
        OvCircle.Visibility   = Visibility.Collapsed;
        CropCircle.Visibility = Visibility.Collapsed;

        // Top strip
        Canvas.SetLeft(OvTop, 0);
        Canvas.SetTop(OvTop, 0);
        OvTop.Width  = canvasW;
        OvTop.Height = Math.Max(0, _cropY);

        // Bottom strip
        double botY = _cropY + _cropH;
        Canvas.SetLeft(OvBottom, 0);
        Canvas.SetTop(OvBottom, botY);
        OvBottom.Width  = canvasW;
        OvBottom.Height = Math.Max(0, canvasH - botY);

        // Left strip (between top and bottom)
        Canvas.SetLeft(OvLeft, 0);
        Canvas.SetTop(OvLeft, _cropY);
        OvLeft.Width  = Math.Max(0, _cropX);
        OvLeft.Height = _cropH;

        // Right strip
        double rightX = _cropX + _cropW;
        Canvas.SetLeft(OvRight, rightX);
        Canvas.SetTop(OvRight, _cropY);
        OvRight.Width  = Math.Max(0, canvasW - rightX);
        OvRight.Height = _cropH;

        // Crop frame border
        Canvas.SetLeft(CropFrame, _cropX);
        Canvas.SetTop(CropFrame, _cropY);
        CropFrame.Width  = _cropW;
        CropFrame.Height = _cropH;
    }

    private void PushNormalizedCrop()
    {
        if (_vm is null || _imgRenderedW <= 0 || _imgRenderedH <= 0)
        {
            return;
        }

        // Normalize crop relative to rendered image bounds
        double normX = (_cropX - _imgOffsetX) / _imgRenderedW;
        double normY = (_cropY - _imgOffsetY) / _imgRenderedH;
        double normW = _cropW / _imgRenderedW;
        double normH = _cropH / _imgRenderedH;

        _normX = normX;
        _normY = normY;
        _normW = normW;
        _normH = normH;
        _vm.SetCropNorm(normX, normY, normW, normH);
    }

    // ── Drag interaction ─────────────────────────────────────────────────────

    private void CropCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(CropCanvas);

        if (IsInsideCropFrame(pos))
        {
            _isDragging = true;
            _dragStart = pos;
            _cropXAtDragStart = _cropX;
            _cropYAtDragStart = _cropY;
            CropCanvas.CaptureMouse();
            e.Handled = true;
        }
    }

    private void CropCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            var pos = e.GetPosition(CropCanvas);
            double dx = pos.X - _dragStart.X;
            double dy = pos.Y - _dragStart.Y;

            double newX = Math.Clamp(_cropXAtDragStart + dx, _imgOffsetX, _imgOffsetX + _imgRenderedW - _cropW);
            double newY = Math.Clamp(_cropYAtDragStart + dy, _imgOffsetY, _imgOffsetY + _imgRenderedH - _cropH);

            _cropX = newX;
            _cropY = newY;

            UpdateCropOverlay();
            PushNormalizedCrop();
            e.Handled = true;
            return;
        }

        var hoverPos = e.GetPosition(CropCanvas);
        CropCanvas.Cursor = IsInsideCropFrame(hoverPos) ? Cursors.SizeAll : Cursors.Arrow;
    }

    private void CropCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            CropCanvas.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void CropCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
        {
            CropCanvas.Cursor = Cursors.Arrow;
        }
    }

    private bool IsInsideCropFrame(Point pos)
    {
        if (_vm?.CropShape == 1)
        {
            // Ellipse hit-test: (dx/rx)² + (dy/ry)² ≤ 1
            double rx = _cropW / 2.0;
            double ry = _cropH / 2.0;
            double dx = (pos.X - (_cropX + rx)) / rx;
            double dy = (pos.Y - (_cropY + ry)) / ry;
            return dx * dx + dy * dy <= 1.0;
        }

        return pos.X >= _cropX && pos.X <= _cropX + _cropW &&
               pos.Y >= _cropY && pos.Y <= _cropY + _cropH;
    }

    // ── Frame resize via scroll wheel ────────────────────────────────────────

    private void CropContainer_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_imgRenderedW <= 0 || _imgRenderedH <= 0 || _cropW <= 0 || _cropH <= 0)
        {
            e.Handled = true;
            return;
        }

        const double step = 1.10;
        double factor = e.Delta > 0 ? step : 1.0 / step;
        double aspect = _cropW / _cropH;

        // Resize the frame while maintaining its aspect ratio.
        double newW = _cropW * factor;
        double newH = newW / aspect;

        // Cap at image bounds.
        if (newW > _imgRenderedW) { newW = _imgRenderedW; newH = newW / aspect; }
        if (newH > _imgRenderedH) { newH = _imgRenderedH; newW = newH * aspect; }

        // Minimum: short side >= 20% of the rendered shorter dimension, at least 20px.
        double minShort = Math.Max(20.0, Math.Min(_imgRenderedW, _imgRenderedH) * 0.20);
        double shortSide = Math.Min(newW, newH);
        if (shortSide < minShort)
        {
            double scale = minShort / shortSide;
            newW *= scale;
            newH *= scale;
        }

        // Keep the frame centered on its current center, clamped to image bounds.
        double cx = _cropX + _cropW / 2.0;
        double cy = _cropY + _cropH / 2.0;

        _cropW = newW;
        _cropH = newH;
        _cropX = Math.Clamp(cx - newW / 2.0, _imgOffsetX, _imgOffsetX + _imgRenderedW - newW);
        _cropY = Math.Clamp(cy - newH / 2.0, _imgOffsetY, _imgOffsetY + _imgRenderedH - newH);

        UpdateCropOverlay();
        PushNormalizedCrop();
        e.Handled = true;
    }
}
