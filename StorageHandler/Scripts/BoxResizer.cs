using StorageHandler.Models;
using StorageHandler.Config;
using StorageHandler.Config.Constants;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StorageHandler.Scripts {
    public class BoxResizer {
        private static readonly bool DebugHandles = true;
        private static readonly bool DebugResize = true;

        private readonly Dictionary<string, string> _lastDebugBlocks = new();

        private void DebugWriteBlockOnChange(string blockKey, string output) {
            if (!_lastDebugBlocks.TryGetValue(blockKey, out var last) || last != output) {
                Debug.WriteLine(output);
                _lastDebugBlocks[blockKey] = output;
            }
        }

        private readonly StorageLoader _storageLoader;
        private readonly StorageContainer _rootContainer;
        private Border? _currentBox;
        private StorageContainer? _currentContainer;
        private Border? _resizeHandle;
        private Point _startDragPoint;
        private bool _isDragging;
        private int _originalGridWidth;
        private int _originalGridHeight;
        private Dictionary<string, int[]> _originalChildPositions = new();
        private Dictionary<string, Border> _handleMap = new();

        private const double HandleVisualSize = AppConfig.HandleVisualSize;
        public const double CanvasScaleFactor = AppConfig.CanvasScaleFactor;
        private const int MinGridSize = AppConfig.MinGridSize;
        private const int MaxGridWidth = AppConfig.MaxGridWidth;
        private const int MaxGridHeight = AppConfig.MaxGridHeight;

        private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true };

        private readonly Action? _refreshUi;

        public BoxResizer(StorageLoader storageLoader, StorageContainer rootContainer, Action? refreshUi = null) {
            _storageLoader = storageLoader;
            _rootContainer = rootContainer;
            _refreshUi = refreshUi;
        }

        public void AttachResizeHandle(Border box, StorageContainer container) {
            if (DebugHandles) Debug.WriteLine($"BoxResizer: Attaching resize handle to container '{container.Name}'");

            // Derive handle color from box background
            var handleColor = box.Background is SolidColorBrush boxBrush
                ? Color.FromArgb(192, (byte)(boxBrush.Color.R * 0.85), (byte)(boxBrush.Color.G * 0.85), (byte)(boxBrush.Color.B * 0.85))
                : Color.FromArgb(128, 255, 255, 255);

            var handle = new Border {
                Width = HandleVisualSize,
                Height = HandleVisualSize,
                Background = new SolidColorBrush(handleColor),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(0, 0, box.CornerRadius.BottomRight, 0),
                Cursor = Cursors.SizeNWSE,
                Tag = container.Name
            };

            PositionResizeHandle(handle, box);
            Canvas.SetZIndex(handle, 100);

            if (box.Parent is not Canvas canvas) {
                return;
            }

            canvas.Children.Add(handle);
            _handleMap[container.Name] = handle;

            handle.MouseDown += (s, e) => HandleResizeStart(s, e, box, container, handle);
            handle.MouseMove += (s, e) => HandleResizeDrag(s, e, box);
            handle.MouseUp += (s, e) => HandleResizeEnd(s, e);

            if (DebugHandles) Debug.WriteLine($"BoxResizer: All event handlers attached for '{container.Name}'");
        }

        private void PositionResizeHandle(FrameworkElement handle, FrameworkElement boundingBox) {
            Canvas.SetLeft(handle, Canvas.GetLeft(boundingBox) + boundingBox.Width - handle.Width);
            Canvas.SetTop(handle, Canvas.GetTop(boundingBox) + boundingBox.Height - handle.Height);
        }

        private void UpdateResizeHandlePosition(FrameworkElement handle, FrameworkElement box)
            => PositionResizeHandle(handle, box);

        private void UpdateSpecificBoxUI(Border box, double canvasLeft, double canvasTop, double canvasWidth, double canvasHeight) {
            Canvas.SetLeft(box, canvasLeft);
            Canvas.SetTop(box, canvasTop);
            box.Width = canvasWidth;
            box.Height = canvasHeight;
        }

        private void HandleResizeStart(object sender, MouseButtonEventArgs e, Border box, StorageContainer container, Border handle) {
            _isDragging = true;
            if (box.Parent is not Canvas canvas) return;

            _startDragPoint = e.GetPosition(canvas);
            _currentBox = box;
            _currentContainer = container;
            _originalGridWidth = container.Size[0];
            _originalGridHeight = container.Size[1];
            _resizeHandle = handle;

            _originalChildPositions.Clear();
            foreach (var child in _rootContainer.Children)
                _originalChildPositions[child.Name] = new[] { child.Position[0], child.Position[1] };

            e.Handled = true;
            ((UIElement)sender).CaptureMouse();
        }

        private void HandleResizeDrag(object sender, MouseEventArgs e, Border referenceBox) {
            if (!_isDragging || _currentBox == null || _currentContainer == null || _resizeHandle == null) return;
            if (referenceBox.Parent is not Canvas canvas) return;

            var currentPoint = e.GetPosition(canvas);
            int newGridWidth = Math.Max(MinGridSize, _originalGridWidth + (int)Math.Round((currentPoint.X - _startDragPoint.X) / CanvasScaleFactor));
            int newGridHeight = Math.Max(MinGridSize, _originalGridHeight + (int)Math.Round((currentPoint.Y - _startDragPoint.Y) / CanvasScaleFactor));

            // Hide all other handles during drag
            foreach (var (containerName, handle) in _handleMap)
                if (handle != _resizeHandle) handle.Visibility = Visibility.Hidden;

            // Restore all other children to original positions
            foreach (var child in _rootContainer.Children)
                if (child.Name != _currentContainer.Name && _originalChildPositions.TryGetValue(child.Name, out var pos)) {
                    child.Position[0] = pos[0];
                    child.Position[1] = pos[1];
                }

            var sb = DebugHandles || DebugResize ? new StringBuilder() : null;

            if (IsValidResize(_currentContainer, newGridWidth, newGridHeight, false, false, sb)) {
                _currentContainer.Size[0] = newGridWidth;
                _currentContainer.Size[1] = newGridHeight;
                UpdateBoxPositions();
                UpdateAllHandles();
            }

            if (sb != null) DebugWriteBlockOnChange("ResizeDragBlock", sb.ToString());
        }

        private void HandleResizeEnd(object sender, MouseButtonEventArgs e) {
            if (!_isDragging || _currentBox == null || _currentContainer == null) {
                if (_isDragging && sender is UIElement element) element.ReleaseMouseCapture();
                _isDragging = false;
                return;
            }

            _isDragging = false;
            if (sender is UIElement uiElement) uiElement.ReleaseMouseCapture();

            // Trust the current state that was validated during drag - don't recalculate
            // The preview already shows the correct positions, just save them
            bool hasSizeChanged = _currentContainer.Size[0] != _originalGridWidth || _currentContainer.Size[1] != _originalGridHeight;
            bool hasPositionChanged = _originalChildPositions.TryGetValue(_currentContainer.Name, out var originalPos) &&
                                      (_currentContainer.Position[0] != originalPos[0] || _currentContainer.Position[1] != originalPos[1]);

            if (hasSizeChanged || hasPositionChanged) {
                try {
                    if (_rootContainer != null) {
                        // The container state is already correct from the last successful drag validation
                        // Just save it
                        var clonedRootForSave = JsonSerializer.Deserialize<StorageContainer>(
                            JsonSerializer.Serialize(_rootContainer, JsonSerializerOptions), JsonSerializerOptions);

                        if (clonedRootForSave != null) {
                            _storageLoader.SaveTemporary(clonedRootForSave);
                        }
                        _refreshUi?.Invoke();
                    }
                } catch {
                    RevertCurrentBoxToOriginalState();
                }
            }

            // Restore all handles
            foreach (var handle in _handleMap.Values)
                handle.Visibility = Visibility.Visible;

            _originalChildPositions.Clear();
            _currentBox = null;
            _currentContainer = null;
            _resizeHandle = null;
        }

        private void RevertCurrentBoxToOriginalState() {
            if (_currentBox == null || _currentContainer == null || _resizeHandle == null ||
                !_originalChildPositions.TryGetValue(_currentContainer.Name, out var originalPos))
                return;

            _currentContainer.Position[0] = originalPos[0];
            _currentContainer.Position[1] = originalPos[1];
            _currentContainer.Size[0] = _originalGridWidth;
            _currentContainer.Size[1] = _originalGridHeight;

            _refreshUi?.Invoke();
        }

        /// <summary>
        /// Validates and applies a resize operation, including collision and bounds checks.
        /// </summary>
        private bool IsValidResize(StorageContainer activeContainer, int newGridWidth, int newGridHeight,
            bool isOriginShiftedLeft, bool isOriginShiftedUp, StringBuilder? debugSb = null) {
            if (!_originalChildPositions.TryGetValue(activeContainer.Name, out var originalPositionArray))
                return false;

            int origGridX = originalPositionArray[0], origGridY = originalPositionArray[1];
            int prospectiveNewGridX = origGridX, prospectiveNewGridY = origGridY;

            if (!AdjustAndValidateBounds(prospectiveNewGridX, prospectiveNewGridY, newGridWidth, newGridHeight))
                return false;

            var originalPositionsSnapshot = _rootContainer.Children.ToDictionary(
                child => child.Name, child => new[] { child.Position[0], child.Position[1] });

            if (!HandleHorizontalContainerAdjustments(activeContainer, origGridX, _originalGridWidth, prospectiveNewGridX, newGridWidth, prospectiveNewGridY, newGridHeight, debugSb) ||
                !HandleVerticalContainerAdjustments(activeContainer, origGridY, _originalGridHeight, prospectiveNewGridY, newGridHeight, prospectiveNewGridX, newGridWidth)) {
                RevertNonActiveChildPositions(activeContainer.Name);
                return false;
            }

            var rootVersionOfActiveContainer = _rootContainer.Children.FirstOrDefault(c => c.Name == activeContainer.Name);
            if (rootVersionOfActiveContainer != null) {
                rootVersionOfActiveContainer.Position[0] = prospectiveNewGridX;
                rootVersionOfActiveContainer.Position[1] = prospectiveNewGridY;
                rootVersionOfActiveContainer.Size[0] = newGridWidth;
                rootVersionOfActiveContainer.Size[1] = newGridHeight;
            }

            // Check for overlaps
            bool overlapDetected = false;
            foreach (var box1 in _rootContainer.Children) {
                foreach (var box2 in _rootContainer.Children.Where(b => b.Name != box1.Name)) {
                    if (box1.Position[0] < box2.Position[0] + box2.Size[0] &&
                        box1.Position[0] + box1.Size[0] > box2.Position[0] &&
                        box1.Position[1] < box2.Position[1] + box2.Size[1] &&
                        box1.Position[1] + box1.Size[1] > box2.Position[1]) {
                        
                        overlapDetected = true;
                        if (debugSb != null) debugSb.AppendLine($"Overlap detected between {box1.Name} and {box2.Name}");
                        break;
                    }
                }
                if (overlapDetected) break;
            }

            if (overlapDetected) {
                // Restore original positions
                foreach (var child in _rootContainer.Children)
                    if (originalPositionsSnapshot.TryGetValue(child.Name, out var origPos)) {
                        child.Position[0] = origPos[0];
                        child.Position[1] = origPos[1];
                    }
                if (rootVersionOfActiveContainer != null) {
                    rootVersionOfActiveContainer.Size[0] = _originalGridWidth;
                    rootVersionOfActiveContainer.Size[1] = _originalGridHeight;
                }
                return false;
            }

            UpdateBoxPositions();
            return true;
        }

        private void RevertNonActiveChildPositions(string activeContainerName) {
            foreach (var child in _rootContainer.Children)
                if (child.Name != activeContainerName && _originalChildPositions.TryGetValue(child.Name, out var originalPos)) {
                    child.Position[0] = originalPos[0];
                    child.Position[1] = originalPos[1];
                }
        }

        private bool AdjustAndValidateBounds(int newGridX, int newGridY, int newGridWidth, int newGridHeight) {
            if (newGridX < 0 || newGridY < 0 ||
                newGridX + newGridWidth > MaxGridWidth ||
                newGridY + newGridHeight > MaxGridHeight ||
                newGridWidth < MinGridSize || newGridHeight < MinGridSize)
                return false;
            return true;
        }

        // Horizontal and vertical adjustment logic is non-trivial and left as-is for clarity.
        private bool HandleHorizontalContainerAdjustments(
    StorageContainer activeContainer,
    int mainOriginalGridX, int mainOriginalGridWidth,
    int mainProspectiveNewGridX, int mainNewGridWidth,
    int mainProspectiveNewGridY, int mainNewGridHeight,
    StringBuilder? debugSb = null) {
            int widthChange = mainNewGridWidth - mainOriginalGridWidth;
            int activeContainerNewRightEdge = mainProspectiveNewGridX + mainNewGridWidth;

            if (widthChange > 0) {
                // Select boxes that overlap with the NEW bounds of the active container
                // We only care about boxes to the right (since we resize from bottom-right)
                // So we look for boxes whose original Left is < New Right Edge
                // AND whose original Left is >= Original Left (to avoid pushing boxes to the left of us)
                var boxesToPush = _rootContainer.Children
                    .Where(b => b.Name != activeContainer.Name && b.Depth == activeContainer.Depth)
                    .Where(b => _originalChildPositions[b.Name][0] < activeContainerNewRightEdge &&
                                _originalChildPositions[b.Name][0] >= mainOriginalGridX) // Ensure we don't grab boxes to our left
                    .Where(b => b.Position[1] < mainProspectiveNewGridY + mainNewGridHeight &&
                                b.Position[1] + b.Size[1] > mainProspectiveNewGridY)
                    .OrderBy(b => _originalChildPositions[b.Name][0])
                    .ToList();

                // Also include boxes that are to the right of the new edge but might be pushed by the chain
                // So we actually need a broader search for the chain reaction.
                // Let's grab EVERYTHING to the right of the original active container.
                var allBoxesToRight = _rootContainer.Children
                    .Where(b => b.Name != activeContainer.Name && b.Depth == activeContainer.Depth)
                    .Where(b => _originalChildPositions[b.Name][0] >= mainOriginalGridX + mainOriginalGridWidth) // Strictly to the right of original
                    .Where(b => b.Position[1] < mainProspectiveNewGridY + mainNewGridHeight &&
                                b.Position[1] + b.Size[1] > mainProspectiveNewGridY)
                    .OrderBy(b => _originalChildPositions[b.Name][0])
                    .ToList();
                
                // Merge the lists (boxes we directly overlap + boxes that might be pushed)
                var chainList = boxesToPush.Union(allBoxesToRight).OrderBy(b => _originalChildPositions[b.Name][0]).ToList();

                if (!chainList.Any()) return true;

                foreach (var box in chainList) {
                    int requiredX = -1;

                    // 1. Check if pushed by active container
                    // If this box was originally overlapping or touching the new bounds, it must be at least at NewRightEdge
                    if (_originalChildPositions[box.Name][0] < activeContainerNewRightEdge) {
                        requiredX = activeContainerNewRightEdge;
                    }

                    // 2. Check if pushed by other boxes in the chain
                    foreach (var leftBox in chainList.Where(b => b != box)) {
                        // If leftBox is to the left (or at same X but processed earlier)
                        // AND overlaps vertically
                        if (_originalChildPositions[leftBox.Name][0] <= _originalChildPositions[box.Name][0] &&
                            DoBoxesOverlapVertically(leftBox, box)) {
                            
                            int leftBoxNewRight = leftBox.Position[0] + leftBox.Size[0];
                            if (leftBoxNewRight > requiredX) {
                                requiredX = leftBoxNewRight;
                            }
                        }
                    }

                    // Apply push if needed
                    if (requiredX != -1 && box.Position[0] < requiredX) {
                        box.Position[0] = requiredX;
                    }

                    if (box.Position[0] + box.Size[0] > MaxGridWidth) return false;
                }
            } else if (widthChange < 0) {
                // Shrink logic remains similar but simpler
                var allBoxesToRight = _rootContainer.Children
                    .Where(b => b.Name != activeContainer.Name && b.Depth == activeContainer.Depth)
                    .Where(b => _originalChildPositions[b.Name][0] >= mainOriginalGridX + mainOriginalGridWidth)
                    .Where(b => b.Position[1] < mainProspectiveNewGridY + mainNewGridHeight &&
                                b.Position[1] + b.Size[1] > mainProspectiveNewGridY)
                    .OrderBy(b => _originalChildPositions[b.Name][0])
                    .ToList();

                foreach (var box in allBoxesToRight) {
                    int minX = activeContainerNewRightEdge;
                    foreach (var other in _rootContainer.Children.Where(b => b != box && b.Depth == activeContainer.Depth)) {
                        // Only consider boxes to the left of 'box'
                        if (other.Position[0] + other.Size[0] <= _originalChildPositions[box.Name][0] &&
                            other.Position[1] < box.Position[1] + box.Size[1] &&
                            other.Position[1] + other.Size[1] > box.Position[1]) 
                        {
                            minX = Math.Max(minX, other.Position[0] + other.Size[0]);
                        }
                    }
                    box.Position[0] = minX;
                }
            }
            return true;
        }

        private bool DoBoxesOverlapVertically(StorageContainer box1, StorageContainer box2) {
            // Use current positions to account for any previous adjustments
            int y1 = box1.Position[1];
            int h1 = box1.Size[1];
            int y2 = box2.Position[1];
            int h2 = box2.Size[1];

            return y1 < y2 + h2 && y1 + h1 > y2;
        }

        private bool HandleVerticalContainerAdjustments(
            StorageContainer activeContainer,
            int mainOriginalGridY, int mainOriginalGridHeight,
            int mainProspectiveNewGridY, int mainNewGridHeight,
            int mainProspectiveNewGridX, int mainNewGridWidth) {
            int heightChange = mainNewGridHeight - mainOriginalGridHeight;
            int activeContainerNewBottomEdge = mainProspectiveNewGridY + mainNewGridHeight;

            if (heightChange > 0) {
                // Select boxes that overlap with the NEW bounds of the active container
                // We only care about boxes below (since we resize from bottom-right)
                var boxesToPush = _rootContainer.Children
                    .Where(b => b.Name != activeContainer.Name && b.Depth == activeContainer.Depth)
                    .Where(b => _originalChildPositions[b.Name][1] < activeContainerNewBottomEdge &&
                                _originalChildPositions[b.Name][1] >= mainOriginalGridY) // Ensure we don't grab boxes above us
                    .Where(b => b.Position[0] < mainProspectiveNewGridX + mainNewGridWidth &&
                                b.Position[0] + b.Size[0] > mainProspectiveNewGridX)
                    .OrderBy(b => _originalChildPositions[b.Name][1])
                    .ToList();

                // Also include boxes that are below the new edge but might be pushed by the chain
                var allBoxesBelow = _rootContainer.Children
                    .Where(b => b.Name != activeContainer.Name && b.Depth == activeContainer.Depth)
                    .Where(b => _originalChildPositions[b.Name][1] >= mainOriginalGridY + mainOriginalGridHeight) // Strictly below original
                    .Where(b => b.Position[0] < mainProspectiveNewGridX + mainNewGridWidth &&
                                b.Position[0] + b.Size[0] > mainProspectiveNewGridX)
                    .OrderBy(b => _originalChildPositions[b.Name][1])
                    .ToList();

                // Merge lists
                var chainList = boxesToPush.Union(allBoxesBelow).OrderBy(b => _originalChildPositions[b.Name][1]).ToList();

                if (!chainList.Any()) return true;

                foreach (var box in chainList) {
                    int requiredY = -1;

                    // 1. Check if pushed by active container
                    if (_originalChildPositions[box.Name][1] < activeContainerNewBottomEdge) {
                        requiredY = activeContainerNewBottomEdge;
                    }

                    // 2. Check if pushed by other boxes in the chain
                    foreach (var topBox in chainList.Where(b => b != box)) {
                        // If topBox is above (or at same Y but processed earlier)
                        // AND overlaps horizontally
                        if (_originalChildPositions[topBox.Name][1] <= _originalChildPositions[box.Name][1] &&
                            DoBoxesOverlapHorizontally(topBox, box)) {

                            int topBoxNewBottom = topBox.Position[1] + topBox.Size[1];
                            if (topBoxNewBottom > requiredY) {
                                requiredY = topBoxNewBottom;
                            }
                        }
                    }

                    // Apply push if needed
                    if (requiredY != -1 && box.Position[1] < requiredY) {
                        box.Position[1] = requiredY;
                    }

                    if (box.Position[1] + box.Size[1] > MaxGridHeight) return false;
                }
            } else if (heightChange < 0) {
                var allBoxesBelow = _rootContainer.Children
                    .Where(b => b.Name != activeContainer.Name && b.Depth == activeContainer.Depth)
                    .Where(b => _originalChildPositions[b.Name][1] >= mainOriginalGridY + mainOriginalGridHeight)
                    .Where(b => b.Position[0] < mainProspectiveNewGridX + mainNewGridWidth &&
                                b.Position[0] + b.Size[0] > mainProspectiveNewGridX)
                    .OrderBy(b => _originalChildPositions[b.Name][1])
                    .ToList();

                foreach (var box in allBoxesBelow) {
                    int minY = activeContainerNewBottomEdge;
                    foreach (var other in _rootContainer.Children.Where(b => b != box && b.Depth == activeContainer.Depth)) {
                        // Only consider boxes above 'box'
                        if (other.Position[1] + other.Size[1] <= _originalChildPositions[box.Name][1] &&
                            other.Position[0] < box.Position[0] + box.Size[0] &&
                            other.Position[0] + other.Size[0] > box.Position[0]) 
                        {
                            minY = Math.Max(minY, other.Position[1] + other.Size[1]);
                        }
                    }
                    box.Position[1] = minY;
                }
            }
            return true;
        }

        private bool DoBoxesOverlapHorizontally(StorageContainer box1, StorageContainer box2) {
            // Use current positions to account for any previous adjustments
            int x1 = box1.Position[0];
            int w1 = box1.Size[0];
            int x2 = box2.Position[0];
            int w2 = box2.Size[0];

            return x1 < x2 + w2 && x1 + w1 > x2;
        }

        private void UpdateBoxPositions() {
            var canvas = GetRelevantCanvas();
            if (canvas == null) return;

            var allUiBoxes = canvas.Children.OfType<Border>()
                .Where(b => b.DataContext is StorageContainer)
                .ToList();

            foreach (var uiBox in allUiBoxes) {
                if (uiBox.DataContext is StorageContainer uiContainerData) {
                    var correspondingRootChild = _rootContainer.Children.FirstOrDefault(c => c.Name == uiContainerData.Name);
                    if (correspondingRootChild != null) {
                        uiContainerData.Position[0] = correspondingRootChild.Position[0];
                        uiContainerData.Position[1] = correspondingRootChild.Position[1];
                        uiContainerData.Size[0] = correspondingRootChild.Size[0];
                        uiContainerData.Size[1] = correspondingRootChild.Size[1];

                        UpdateSpecificBoxUI(uiBox,
                            correspondingRootChild.Position[0] * CanvasScaleFactor,
                            correspondingRootChild.Position[1] * CanvasScaleFactor,
                            correspondingRootChild.Size[0] * CanvasScaleFactor,
                            correspondingRootChild.Size[1] * CanvasScaleFactor);
                    }
                }
            }
        }

        private void UpdateAllHandles() {
            var canvas = GetRelevantCanvas();
            if (canvas == null) return;

            var allUiBoxes = canvas.Children.OfType<Border>()
                .Where(b => b.DataContext is StorageContainer)
                .ToList();

            foreach (var uiBox in allUiBoxes) {
                if (uiBox.DataContext is StorageContainer containerData &&
                    _handleMap.TryGetValue(containerData.Name, out var handle)) {
                    if (handle == null) continue;
                    UpdateResizeHandlePosition(handle, uiBox);

                    if (uiBox.Background is SolidColorBrush boxBrush) {
                        var handleColor = Color.FromArgb(
                            192,
                            (byte)(boxBrush.Color.R * 0.85),
                            (byte)(boxBrush.Color.G * 0.85),
                            (byte)(boxBrush.Color.B * 0.85));
                        handle.Background = new SolidColorBrush(handleColor);
                    }
                    handle.CornerRadius = new CornerRadius(0, 0, uiBox.CornerRadius.BottomRight, 0);
                }
            }
        }

        private Canvas? GetRelevantCanvas() {
            if (_currentBox?.Parent is Canvas canvas) return canvas;
            if (_rootContainer != null && _rootContainer.Children.Any() && _handleMap.Any()) {
                var firstHandleVisual = _handleMap.Values.FirstOrDefault();
                if (firstHandleVisual?.Parent is Canvas handleCanvas) return handleCanvas;
            }
            return null;
        }

        public void ResizeWithOffset(StorageContainer container, int newGridWidth, int newGridHeight, int offsetGridX, int offsetGridY) {
            var targetContainer = _rootContainer.Children.FirstOrDefault(c => c.Name == container.Name);
            if (targetContainer == null) return;

            targetContainer.Position[0] = Math.Max(0, targetContainer.Position[0] + offsetGridX);
            targetContainer.Position[1] = Math.Max(0, targetContainer.Position[1] + offsetGridY);
            targetContainer.Size[0] = Math.Max(MinGridSize, Math.Min(newGridWidth, MaxGridWidth - targetContainer.Position[0]));
            targetContainer.Size[1] = Math.Max(MinGridSize, Math.Min(newGridHeight, MaxGridHeight - targetContainer.Position[1]));

            _storageLoader.SaveTemporary(_rootContainer);
            _refreshUi?.Invoke();
        }

        public void ClearHandles(Canvas canvas) {
            if (canvas == null) return;
            var handlesToRemove = canvas.Children.OfType<Border>()
                .Where(r => r.Tag is string tag && _handleMap.ContainsKey(tag))
                .ToList();

            foreach (var handle in handlesToRemove)
                canvas.Children.Remove(handle);

            _handleMap.Clear();
        }
    }
}
