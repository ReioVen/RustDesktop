using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using RustDesktop.Core.Models;
using TeamMember = RustDesktop.Core.Models.TeamMember;

namespace RustDesktop.App.Controls;

public partial class ZoomableMapControl : UserControl
{
    private double _zoomLevel = 1.0;
    private const double ZoomFactor = 1.2;
    private const double MinZoom = 0.5;
    private const double MaxZoom = 5.0;
    private int _mapWidth = 4096;
    private int _mapHeight = 4096;
    private int _worldSize = 0;
    private ScaleTransform? _imageScaleTransform;
    private ScaleTransform? _overlayScaleTransform;
    private TranslateTransform? _imageTranslateTransform;
    private TranslateTransform? _overlayTranslateTransform;
    private bool _isDragging = false;
    private Point _lastMousePosition;

    public static readonly DependencyProperty ImageDataProperty =
        DependencyProperty.Register(
            nameof(ImageData),
            typeof(byte[]),
            typeof(ZoomableMapControl),
            new PropertyMetadata(null, OnImageDataChanged));

    public static readonly DependencyProperty VendingMachinesProperty =
        DependencyProperty.Register(
            nameof(VendingMachines),
            typeof(ObservableCollection<VendingMachine>),
            typeof(ZoomableMapControl),
            new PropertyMetadata(null, OnVendingMachinesChanged));

    public static readonly DependencyProperty TeamMembersProperty =
        DependencyProperty.Register(
            nameof(TeamMembers),
            typeof(List<TeamMember>),
            typeof(ZoomableMapControl),
            new PropertyMetadata(null, OnTeamMembersChanged));

    public static readonly DependencyProperty WorldSizeProperty =
        DependencyProperty.Register(
            nameof(WorldSize),
            typeof(int),
            typeof(ZoomableMapControl),
            new PropertyMetadata(0, OnWorldSizeChanged));

    public byte[]? ImageData
    {
        get => (byte[]?)GetValue(ImageDataProperty);
        set => SetValue(ImageDataProperty, value);
    }

    public ObservableCollection<VendingMachine>? VendingMachines
    {
        get => (ObservableCollection<VendingMachine>?)GetValue(VendingMachinesProperty);
        set => SetValue(VendingMachinesProperty, value);
    }

    public List<TeamMember>? TeamMembers
    {
        get => (List<TeamMember>?)GetValue(TeamMembersProperty);
        set => SetValue(TeamMembersProperty, value);
    }

    public int WorldSize
    {
        get => (int)GetValue(WorldSizeProperty);
        set => SetValue(WorldSizeProperty, value);
    }

    public ZoomableMapControl()
    {
        InitializeComponent();
        Loaded += ZoomableMapControl_Loaded;
        MapContainer.SizeChanged += MapContainer_SizeChanged;
    }

    private void ZoomableMapControl_Loaded(object sender, RoutedEventArgs e)
    {
        // Get the transforms from XAML
        _imageScaleTransform = ImageScaleTransform;
        _overlayScaleTransform = OverlayScaleTransform;
        _imageTranslateTransform = ImageTranslateTransform;
        _overlayTranslateTransform = OverlayTranslateTransform;
        
        // Update overlay when image size changes (after Uniform stretch is applied)
        MapImage.SizeChanged += MapImage_SizeChanged;
    }

    private void MapImage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Update overlay transform when image's rendered size changes
        UpdateOverlayTransform();
    }

    private void MapContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Update overlay transform when container size changes
        UpdateOverlayTransform();
    }

    private static void OnImageDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomableMapControl control && e.NewValue is byte[] imageData)
        {
            control.LoadImage(imageData);
        }
    }

    private static void OnVendingMachinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomableMapControl control)
        {
            control.UpdateMarkers();
        }
    }

    private static void OnTeamMembersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomableMapControl control)
        {
            control.UpdateMarkers();
        }
    }

    private static void OnWorldSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomableMapControl control && e.NewValue is int worldSize)
        {
            control._worldSize = worldSize > 0 ? worldSize : 0;
            control.UpdateMarkers();
        }
    }

    private void LoadImage(byte[] imageData)
    {
        try
        {
            if (imageData == null || imageData.Length == 0)
            {
                MapImage.Source = null;
                return;
            }

            using var ms = new MemoryStream(imageData);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = ms;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            MapImage.Source = bitmap;
            _mapWidth = bitmap.PixelWidth;
            _mapHeight = bitmap.PixelHeight;
            
            // Update markers after image loads and layout completes
            // We need to wait for the image to be laid out so we can get its ActualWidth/ActualHeight
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Dispatcher.BeginInvoke(new Action(() => 
                {
                    // Force layout update to get ActualWidth/ActualHeight
                    MapImage.UpdateLayout();
                    UpdateMarkers();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading image: {ex.Message}");
        }
    }

    private void MapImage_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        try
        {
            e.Handled = true;
            if (_imageScaleTransform == null) return;

            var zoomFactor = e.Delta > 0 ? ZoomFactor : 1.0 / ZoomFactor;
            var newZoom = _zoomLevel * zoomFactor;
            newZoom = Math.Max(MinZoom, Math.Min(MaxZoom, newZoom));
            
            _zoomLevel = newZoom;
            
            // Apply zoom using ScaleTransform
            _imageScaleTransform.ScaleX = _zoomLevel;
            _imageScaleTransform.ScaleY = _zoomLevel;
            
            // Update overlay transform to match image transform
            UpdateOverlayTransform();
            
            // Update markers to reflect new zoom level (they need to be resized)
            UpdateMarkers();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in MouseWheel: {ex.Message}");
        }
    }

    private void MapImage_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            if (MapImage.CaptureMouse())
            {
                _isDragging = true;
                _lastMousePosition = e.GetPosition(MapContainer);
                MapImage.Cursor = System.Windows.Input.Cursors.Hand;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in MouseLeftButtonDown: {ex.Message}");
        }
    }

    private void MapImage_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        try
        {
            if (_isDragging && _imageTranslateTransform != null && _overlayTranslateTransform != null)
            {
                var currentPosition = e.GetPosition(MapContainer);
                var deltaX = currentPosition.X - _lastMousePosition.X;
                var deltaY = currentPosition.Y - _lastMousePosition.Y;

                // Update translation
                _imageTranslateTransform.X += deltaX;
                _imageTranslateTransform.Y += deltaY;
                _overlayTranslateTransform.X += deltaX;
                _overlayTranslateTransform.Y += deltaY;

                _lastMousePosition = currentPosition;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in MouseMove: {ex.Message}");
        }
    }

    private void MapImage_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            if (_isDragging)
            {
                _isDragging = false;
                MapImage.ReleaseMouseCapture();
                MapImage.Cursor = System.Windows.Input.Cursors.Arrow;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in MouseLeftButtonUp: {ex.Message}");
        }
    }

    private void UpdateOverlayTransform()
    {
        try
        {
            if (_overlayScaleTransform == null || _overlayTranslateTransform == null) return;
            if (_imageScaleTransform == null || _imageTranslateTransform == null) return;
            if (_mapWidth <= 0 || _mapHeight <= 0) return;

            var containerWidth = MapContainer.ActualWidth;
            var containerHeight = MapContainer.ActualHeight;
            if (containerWidth <= 0 || containerHeight <= 0) return;

            // Get the actual rendered size of the image (after Uniform stretch, before our zoom/pan transforms)
            var imageRenderedWidth = MapImage.ActualWidth;
            var imageRenderedHeight = MapImage.ActualHeight;
            
            if (imageRenderedWidth <= 0 || imageRenderedHeight <= 0) return;

            // Set overlay canvas to match the image's rendered size (after Uniform stretch)
            // This ensures the canvas is the same size as the image before transforms
            MarkersOverlay.Width = imageRenderedWidth;
            MarkersOverlay.Height = imageRenderedHeight;

            // Calculate scale factor from map pixels to rendered image size
            var scaleX = imageRenderedWidth / _mapWidth;
            var scaleY = imageRenderedHeight / _mapHeight;
            var baseScale = Math.Min(scaleX, scaleY); // Uniform uses min

            // Apply the same transforms as the image
            // The image's ScaleTransform applies on top of Uniform stretch
            _overlayScaleTransform.ScaleX = _imageScaleTransform.ScaleX;
            _overlayScaleTransform.ScaleY = _imageScaleTransform.ScaleY;
            _overlayTranslateTransform.X = _imageTranslateTransform.X;
            _overlayTranslateTransform.Y = _imageTranslateTransform.Y;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating overlay transform: {ex.Message}");
        }
    }

    private void UpdateMarkers()
    {
        try
        {
            MarkersOverlay.Children.Clear();
            
            if (MapImage.Source == null || _mapWidth <= 0 || _mapHeight <= 0) return;

            // Update overlay transform first (this sets the canvas size)
            UpdateOverlayTransform();

            // Position markers in raw map coordinates (0 to mapWidth/Height)
            // The transforms will handle all scaling and translation
            if (VendingMachines != null && VendingMachines.Count > 0)
            {
                foreach (var vm in VendingMachines)
                {
                    var marker = CreateVendingMachineMarker(vm);
                    if (marker != null)
                    {
                        MarkersOverlay.Children.Add(marker);
                    }
                }
            }

            // Add teammate markers
            if (TeamMembers != null && TeamMembers.Count > 0)
            {
                foreach (var member in TeamMembers)
                {
                    if (member.X.HasValue && member.Y.HasValue)
                    {
                        var marker = CreateTeammateMarker(member);
                        if (marker != null)
                        {
                            MarkersOverlay.Children.Add(marker);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating markers: {ex.Message}");
        }
    }

    private UIElement? CreateVendingMachineMarker(VendingMachine vm)
    {
        try
        {
            var (mapX, mapY) = WorldToMapCoordinates(vm.X, vm.Y);
            if (mapX < 0 || mapY < 0 || mapX > _mapWidth || mapY > _mapHeight) return null;

            // Convert from map pixel coordinates to overlay canvas coordinates
            // Overlay canvas size matches the image's rendered size (after Uniform stretch)
            var imageRenderedWidth = MapImage.ActualWidth;
            var imageRenderedHeight = MapImage.ActualHeight;
            
            if (imageRenderedWidth <= 0 || imageRenderedHeight <= 0) return null;

            // Scale coordinates from map pixels to rendered image size
            var scaleX = imageRenderedWidth / _mapWidth;
            var scaleY = imageRenderedHeight / _mapHeight;
            var baseScale = Math.Min(scaleX, scaleY); // Uniform uses min
            
            var canvasX = mapX * baseScale;
            var canvasY = mapY * baseScale;
            
            // Scale marker size inversely with zoom level
            // When zoomed in (zoom > 1), markers get smaller
            // When zoomed out (zoom < 1), markers get larger
            // Increased base size to help with positioning accuracy and spacing
            const double baseMarkerSize = 20.0;
            var markerSize = baseMarkerSize / _zoomLevel;

            // Determine which image to use based on shop status
            // Shop is active if it has items in stock (Items are only added if quantity > 0)
            // Check Items.Count > 0 since items are filtered to only include those with quantity > 0
            bool hasItemsInStock = vm.Items != null && vm.Items.Count > 0;
            // Also check IsActive property as a fallback
            bool isActive = hasItemsInStock || vm.IsActive;
            string imageFileName = isActive ? "ActiveShop.png" : "InactiveShop.png";
            
            // Debug logging to help diagnose issues
            System.Diagnostics.Debug.WriteLine($"Shop {vm.Id}: Items.Count={vm.Items?.Count ?? 0}, IsActive={vm.IsActive}, hasItemsInStock={hasItemsInStock}, isActive={isActive}, image={imageFileName}");

            // Try to load the shop image
            try
            {
                var imagePath = FindShopImagePath(imageFileName);
                if (imagePath != null && File.Exists(imagePath))
                {
                    using (var fs = File.OpenRead(imagePath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = fs;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        var image = new Image
                        {
                            Source = bitmap,
                            Width = markerSize,
                            Height = markerSize,
                            Stretch = Stretch.Uniform
                        };

                        Canvas.SetLeft(image, canvasX - markerSize / 2);
                        Canvas.SetTop(image, canvasY - markerSize / 2);

                        return image;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading {imageFileName}: {ex.Message}");
                // Fall through to ellipse fallback
            }

            // Fallback to ellipse if image loading failed
            var strokeThickness = 2.0 / _zoomLevel;
            var ellipse = new Ellipse
            {
                Width = markerSize,
                Height = markerSize,
                Fill = new SolidColorBrush(Colors.Orange),
                Stroke = new SolidColorBrush(Colors.DarkOrange),
                StrokeThickness = strokeThickness
            };

            Canvas.SetLeft(ellipse, canvasX - markerSize / 2);
            Canvas.SetTop(ellipse, canvasY - markerSize / 2);

            return ellipse;
        }
        catch
        {
            return null;
        }
    }

    private UIElement? CreateTeammateMarker(TeamMember member)
    {
        try
        {
            if (!member.X.HasValue || !member.Y.HasValue) return null;
            
            var (mapX, mapY) = WorldToMapCoordinates((float)member.X.Value, (float)member.Y.Value);
            if (mapX < 0 || mapY < 0 || mapX > _mapWidth || mapY > _mapHeight) return null;

            // Convert from map pixel coordinates to overlay canvas coordinates
            var imageRenderedWidth = MapImage.ActualWidth;
            var imageRenderedHeight = MapImage.ActualHeight;
            
            if (imageRenderedWidth <= 0 || imageRenderedHeight <= 0) return null;

            // Scale coordinates from map pixels to rendered image size
            var scaleX = imageRenderedWidth / _mapWidth;
            var scaleY = imageRenderedHeight / _mapHeight;
            var baseScale = Math.Min(scaleX, scaleY); // Uniform uses min
            
            var canvasX = mapX * baseScale;
            var canvasY = mapY * baseScale;

            var color = member.Dead ? Colors.Red : (member.Online ? Colors.Green : Colors.Gray);
            
            // Scale marker size inversely with zoom level
            // When zoomed in (zoom > 1), markers get smaller
            // When zoomed out (zoom < 1), markers get larger
            const double baseMarkerSize = 10.0;
            var markerSize = baseMarkerSize / _zoomLevel;
            var strokeThickness = 1.5 / _zoomLevel;
            
            var ellipse = new Ellipse
            {
                Width = markerSize,
                Height = markerSize,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = strokeThickness
            };

            Canvas.SetLeft(ellipse, canvasX - markerSize / 2);
            Canvas.SetTop(ellipse, canvasY - markerSize / 2);

            return ellipse;
        }
        catch
        {
            return null;
        }
    }

    private string? FindShopImagePath(string imageFileName)
    {
        // Try base directory (works for both development and published builds)
        var basePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PICS", imageFileName);
        if (File.Exists(basePath))
        {
            return basePath;
        }
        
        // Try relative to source directory (for development)
        var sourcePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "src", "PICS", imageFileName);
        var fullSourcePath = System.IO.Path.GetFullPath(sourcePath);
        if (File.Exists(fullSourcePath))
        {
            return fullSourcePath;
        }
        
        // Try absolute path (fallback)
        var absolutePath = System.IO.Path.Combine(@"C:\Programming\RustDesktop\src\PICS", imageFileName);
        if (File.Exists(absolutePath))
        {
            return absolutePath;
        }
        
        return null;
    }

    private (double x, double y) WorldToMapCoordinates(float worldX, float worldY)
    {
        var worldSize = _worldSize > 0 ? _worldSize : 4500;

        // Rust world coordinates are 0..worldSize and map image coordinates are 0..mapWidth/Height.
        var clampedX = Math.Clamp(worldX, 0, worldSize);
        var clampedY = Math.Clamp(worldY, 0, worldSize);

        var normalizedX = clampedX / worldSize;
        var normalizedY = 1.0 - (clampedY / worldSize); // Y axis is inverted on images

        var mapX = normalizedX * _mapWidth;
        var mapY = normalizedY * _mapHeight;

        return (mapX, mapY);
    }
}






