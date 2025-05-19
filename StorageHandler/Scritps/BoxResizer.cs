using StorageHandler.Models;
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
        private static readonly bool DebugInit = false;
        private static readonly bool DebugHandles = true;
        private static readonly bool DebugResize = true;
        private static readonly bool DebugResizeVerbose = false;
        private static readonly bool DebugPositioning = true;
        private static readonly bool DebugEvents = false;
        private static readonly bool DebugErrors = false;
        private static readonly bool DebugErrors1 = true;
        private static readonly bool DebugSerialization = false;

        private readonly Dictionary<string, string> _lastDebugBlocks = new();

        private void DebugWriteBlockOnChange(string blockKey, string output) {
            if (!_lastDebugBlocks.TryGetValue(blockKey, out var last) || last != output) {
                Debug.WriteLine(output);
                _lastDebugBlocks[blockKey] = output;
            }
        }

        private readonly StorageLoader _storageLoader;
        private readonly StorageContainer _rootContainer;
        private Border _currentBox;
        private StorageContainer _currentContainer;
        private Border _resizeHandle;
        private Point _startDragPoint;
        private bool _isDragging;
        private int _originalGridWidth;
        private int _originalGridHeight;
        private Dictionary<string, int[]> _originalChildPositions = new();
        private Dictionary<string, Border> _handleMap = new();

        private const double HandleVisualSize = 16;
        public const double CanvasScaleFactor = 100;
        private const int MinGridSize = 1;
        private const int MaxGridCoordinate = 10;

        private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true };

        public BoxResizer(StorageLoader storageLoader, StorageContainer rootContainer) {
            _storageLoader = storageLoader;
            _rootContainer = rootContainer;
            if (DebugInit) Debug.WriteLine("BoxResizer: Initialized");
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
                if (DebugErrors) Debug.WriteLine("BoxResizer: ERROR - Box parent is not a Canvas");
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
            if (DebugEvents) Debug.WriteLine("BoxResizer: Handle MouseDown event triggered");
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

            int finalGridWidth = (int)Math.Round(_currentBox.Width / CanvasScaleFactor);
            int finalGridHeight = (int)Math.Round(_currentBox.Height / CanvasScaleFactor);
            int finalGridX = (int)Math.Round(Canvas.GetLeft(_currentBox) / CanvasScaleFactor);
            int finalGridY = (int)Math.Round(Canvas.GetTop(_currentBox) / CanvasScaleFactor);

            bool hasSizeChanged = finalGridWidth != _originalGridWidth || finalGridHeight != _originalGridHeight;
            bool hasPositionChanged = _originalChildPositions.TryGetValue(_currentContainer.Name, out var originalPos) &&
                                      (finalGridX != originalPos[0] || finalGridY != originalPos[1]);

            if (finalGridX < 0 || finalGridY < 0 || finalGridWidth < MinGridSize || finalGridHeight < MinGridSize) {
                RevertCurrentBoxToOriginalState();
                _originalChildPositions.Clear();
                return;
            }

            if (hasSizeChanged || hasPositionChanged) {
                try {
                    var containerInRoot = _rootContainer.Children.FirstOrDefault(c => c.Name == _currentContainer.Name);
                    if (containerInRoot != null) {
                        containerInRoot.Size = new[] { finalGridWidth, finalGridHeight };
                        containerInRoot.Position = new[] { finalGridX, finalGridY };
                    }

                    var clonedRootForSave = JsonSerializer.Deserialize<StorageContainer>(
                        JsonSerializer.Serialize(_rootContainer, JsonSerializerOptions), JsonSerializerOptions);

                    _storageLoader.SaveTemporary(clonedRootForSave);
                    UpdateAllHandles();
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

            UpdateSpecificBoxUI(_currentBox,
                originalPos[0] * CanvasScaleFactor,
                originalPos[1] * CanvasScaleFactor,
                _originalGridWidth * CanvasScaleFactor,
                _originalGridHeight * CanvasScaleFactor);
            UpdateResizeHandlePosition(_resizeHandle, _currentBox);
            UpdateAllHandles();
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
            foreach (var box1 in _rootContainer.Children)
                foreach (var box2 in _rootContainer.Children.Where(b => b.Name != box1.Name))
                    if (box1.Position[0] < box2.Position[0] + box2.Size[0] &&
                        box1.Position[0] + box1.Size[0] > box2.Position[0] &&
                        box1.Position[1] < box2.Position[1] + box2.Size[1] &&
                        box1.Position[1] + box1.Size[1] > box2.Position[1]) {
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
                newGridX + newGridWidth > MaxGridCoordinate ||
                newGridY + newGridHeight > MaxGridCoordinate ||
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
            int activeContainerOldRightEdge = mainOriginalGridX + mainOriginalGridWidth;
            int activeContainerNewRightEdge = mainProspectiveNewGridX + mainNewGridWidth;

            // Only select boxes that are to the right of the active container's old right edge
            // and have vertical overlap with the active container's new height
            var allBoxesToRight = _rootContainer.Children
                .Where(b => b.Name != activeContainer.Name && b.Depth == activeContainer.Depth)
                .Where(b => _originalChildPositions[b.Name][0] >= activeContainerOldRightEdge)
                .Where(b => _originalChildPositions[b.Name][1] < mainProspectiveNewGridY + mainNewGridHeight &&
                            _originalChildPositions[b.Name][1] + b.Size[1] > mainProspectiveNewGridY)
                .OrderBy(b => _originalChildPositions[b.Name][0])
                .ToList();

            if (!allBoxesToRight.Any()) return true;

            if (widthChange > 0) {
                int pushDistance = activeContainerNewRightEdge - activeContainerOldRightEdge;

                // Modified logic: Only push boxes that would actually collide with the resized box
                var boxesToPush = allBoxesToRight
                    .Where(b => _originalChildPositions[b.Name][0] < activeContainerNewRightEdge)
                    .ToList();

                if (boxesToPush.Any()) {
                    // Check if pushing these boxes would exceed grid limits
                    foreach (var boxToPush in boxesToPush) {
                        int newBoxX = _originalChildPositions[boxToPush.Name][0] + pushDistance;
                        if (newBoxX + boxToPush.Size[0] > MaxGridCoordinate) return false;
                    }

                    // Push only the boxes that would collide
                    foreach (var boxToPush in boxesToPush)
                        boxToPush.Position[0] = _originalChildPositions[boxToPush.Name][0] + pushDistance;

                    if (debugSb != null)
                        debugSb.AppendLine($"Pushed {boxesToPush.Count} boxes to right by {pushDistance} units");
                } else if (debugSb != null) {
                    debugSb.AppendLine("No boxes needed to be pushed (enough space between boxes)");
                }
            } else if (widthChange < 0) {
                // For each box to the right, move it left as far as possible without overlapping
                foreach (var box in allBoxesToRight.OrderBy(b => _originalChildPositions[b.Name][0])) {
                    int minX = activeContainerNewRightEdge;
                    // Find the rightmost edge of any box to the left of this one (including the resized box)
                    foreach (var other in _rootContainer.Children.Where(b => b != box && b.Depth == activeContainer.Depth)) {
                        int otherRight = other.Position[0] + other.Size[0];
                        if (otherRight <= _originalChildPositions[box.Name][0] && // only consider boxes to the left
                            other.Position[1] < box.Position[1] + box.Size[1] &&
                            other.Position[1] + other.Size[1] > box.Position[1]) // vertical overlap
                        {
                            minX = Math.Max(minX, otherRight);
                        }
                    }
                    box.Position[0] = minX;
                }
            }
            return true;
        }


        private bool HandleVerticalContainerAdjustments(
            StorageContainer activeContainer,
            int mainOriginalGridY, int mainOriginalGridHeight,
            int mainProspectiveNewGridY, int mainNewGridHeight,
            int mainProspectiveNewGridX, int mainNewGridWidth) {
            int heightChange = mainNewGridHeight - mainOriginalGridHeight;
            int activeContainerOldBottomEdge = mainOriginalGridY + mainOriginalGridHeight;
            int activeContainerNewBottomEdge = mainProspectiveNewGridY + mainNewGridHeight;

            if (heightChange > 0) {
                int pushDistance = activeContainerNewBottomEdge - activeContainerOldBottomEdge;
                var boxesToPush = _rootContainer.Children
                    .Where(b => b.Name != activeContainer.Name && b.Depth == activeContainer.Depth)
                    .Where(b => _originalChildPositions[b.Name][1] >= activeContainerOldBottomEdge)
                    .Where(b => _originalChildPositions[b.Name][0] < mainProspectiveNewGridX + mainNewGridWidth &&
                                _originalChildPositions[b.Name][0] + b.Size[0] > mainProspectiveNewGridX)
                    .OrderBy(b => _originalChildPositions[b.Name][1])
                    .ToList();

                foreach (var boxToPush in boxesToPush) {
                    int newBoxY = _originalChildPositions[boxToPush.Name][1] + pushDistance;
                    if (newBoxY + boxToPush.Size[1] > MaxGridCoordinate) return false;
                    bool wouldOverlap = _rootContainer.Children
                        .Where(other => other.Name != boxToPush.Name && other.Name != activeContainer.Name && other.Depth == activeContainer.Depth)
                        .Any(other => {
                            int otherCurrentY = boxesToPush.Contains(other) ? _originalChildPositions[other.Name][1] + pushDistance : _originalChildPositions[other.Name][1];
                            int otherOriginalX = _originalChildPositions[other.Name][0];
                            return newBoxY < otherCurrentY + other.Size[1] &&
                                   newBoxY + boxToPush.Size[1] > otherCurrentY &&
                                   _originalChildPositions[boxToPush.Name][0] < otherOriginalX + other.Size[0] &&
                                   _originalChildPositions[boxToPush.Name][0] + boxToPush.Size[0] > otherOriginalX;
                        });
                    if (wouldOverlap) return false;
                    boxToPush.Position[1] = newBoxY;
                }
            } else if (heightChange < 0) {
                var boxesToPull = _rootContainer.Children
                    .Where(b => b.Name != activeContainer.Name && b.Depth == activeContainer.Depth)
                    .Where(b => _originalChildPositions[b.Name][1] >= activeContainerNewBottomEdge)
                    .Where(b => _originalChildPositions[b.Name][1] >= mainOriginalGridY + mainOriginalGridHeight)
                    .Where(b => _originalChildPositions[b.Name][0] < mainProspectiveNewGridX + mainNewGridWidth &&
                                _originalChildPositions[b.Name][0] + b.Size[0] > mainProspectiveNewGridX)
                    .OrderBy(b => _originalChildPositions[b.Name][1])
                    .ToList();

                if (boxesToPull.Any()) {
                    var boxesByRow = boxesToPull
                        .GroupBy(b => _originalChildPositions[b.Name][1])
                        .OrderBy(g => g.Key);

                    int previousRowNewBottomEdge = activeContainerNewBottomEdge;
                    foreach (var rowGroup in boxesByRow) {
                        int targetYForRow = previousRowNewBottomEdge;
                        int shiftAmount = rowGroup.Key - targetYForRow;
                        foreach (var boxToPull in rowGroup) {
                            int newPulledY = _originalChildPositions[boxToPull.Name][1] - shiftAmount;
                            if (newPulledY < 0) newPulledY = 0;
                            boxToPull.Position[1] = newPulledY;
                        }
                        int maxPulledHeightInRow = rowGroup.Max(b => b.Size[1]);
                        previousRowNewBottomEdge = targetYForRow + maxPulledHeightInRow;
                    }
                }
            }
            return true;
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
                    _handleMap.TryGetValue(containerData.Name, out Border handle)) {
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

        private Canvas GetRelevantCanvas() {
            if (_currentBox?.Parent is Canvas canvas) return canvas;
            if (_rootContainer.Children.Any() && _handleMap.Any()) {
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
            targetContainer.Size[0] = Math.Max(MinGridSize, Math.Min(newGridWidth, MaxGridCoordinate - targetContainer.Position[0]));
            targetContainer.Size[1] = Math.Max(MinGridSize, Math.Min(newGridHeight, MaxGridCoordinate - targetContainer.Position[1]));

            UpdateBoxPositions();
            UpdateAllHandles();
            _storageLoader.SaveTemporary(_rootContainer);
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
