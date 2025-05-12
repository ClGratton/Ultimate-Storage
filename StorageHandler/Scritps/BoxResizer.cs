using StorageHandler.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection; // For MethodBase
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace StorageHandler.Scripts {
    public class BoxResizer {
        private readonly StorageLoader _storageLoader;
        private readonly StorageContainer _rootContainer;
        private Border _currentBox;
        private StorageContainer _currentContainer;
        private Rectangle _resizeHandle;
        private Point _startDragPoint;
        private bool _isDragging;
        private int _originalGridWidth;
        private int _originalGridHeight;
        private Dictionary<string, int[]> _originalChildPositions;
        private Dictionary<string, Rectangle> _handleMap;

        private const double HandleVisualSize = 16;
        private const double CanvasScaleFactor = 100;
        private const int MinGridSize = 1;
        private const int MaxGridCoordinate = 10;
        // private const double ResizeFromOppositeEdgeThreshold = 20; // Not directly used in this simplified version of logic capture

        private static readonly JsonSerializerOptions JsonSerializerOptions = new() { WriteIndented = true };

        public BoxResizer(StorageLoader storageLoader, StorageContainer rootContainer) {
            _storageLoader = storageLoader;
            _rootContainer = rootContainer;
            _originalChildPositions = new Dictionary<string, int[]>();
            _handleMap = new Dictionary<string, Rectangle>();
            Debug.WriteLine("BoxResizer: Initialized");
        }

        public void AttachResizeHandle(Border box, StorageContainer container) {
            Debug.WriteLine($"BoxResizer: Attaching resize handle to container '{container.Name}'");

            var handle = new Rectangle {
                Width = HandleVisualSize,
                Height = HandleVisualSize,
                Fill = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255)),
                Stroke = Brushes.Gray,
                StrokeThickness = 1,
                Cursor = Cursors.SizeNWSE,
                Tag = container.Name
            };

            PositionResizeHandle(handle, box);
            Canvas.SetZIndex(handle, 100);

            Debug.WriteLine($"BoxResizer: Handle positioned at ({Canvas.GetLeft(handle)}, {Canvas.GetTop(handle)})");

            if (box.Parent is Canvas canvas) {
                canvas.Children.Add(handle);
                _handleMap[container.Name] = handle;
                Debug.WriteLine("BoxResizer: Handle added to canvas");
            } else {
                Debug.WriteLine("BoxResizer: ERROR - Box parent is not a Canvas");
                return;
            }

            handle.MouseDown += (object s, MouseButtonEventArgs e) => HandleResizeStart(s, e, box, container, handle);
            handle.MouseMove += (object s, MouseEventArgs e) => HandleResizeDrag(s, e, box);
            handle.MouseUp += (object s, MouseButtonEventArgs e) => HandleResizeEnd(s, e);


            Debug.WriteLine($"BoxResizer: All event handlers attached for '{container.Name}'");
        }

        private void PositionResizeHandle(Rectangle handle, FrameworkElement boundingBox) {
            Canvas.SetLeft(handle, Canvas.GetLeft(boundingBox) + boundingBox.Width - HandleVisualSize);
            Canvas.SetTop(handle, Canvas.GetTop(boundingBox) + boundingBox.Height - HandleVisualSize);
        }

        private void UpdateResizeHandlePosition(Rectangle handle, FrameworkElement box) {
            PositionResizeHandle(handle, box);
        }

        private void UpdateSpecificBoxUI(Border box, double canvasLeft, double canvasTop, double canvasWidth, double canvasHeight) {
            Canvas.SetLeft(box, canvasLeft);
            Canvas.SetTop(box, canvasTop);
            box.Width = canvasWidth;
            box.Height = canvasHeight;
        }

        private void HandleResizeStart(object sender, MouseButtonEventArgs e, Border box, StorageContainer container, Rectangle handle) {
            Debug.WriteLine("BoxResizer: Handle MouseDown event triggered");
            _isDragging = true;
            if (box.Parent is not Canvas canvas) return;

            _startDragPoint = e.GetPosition(canvas);
            _currentBox = box;
            _currentContainer = container;
            _originalGridWidth = container.Size[0];
            _originalGridHeight = container.Size[1];
            _resizeHandle = handle;

            _originalChildPositions.Clear();
            foreach (var child in _rootContainer.Children) {
                _originalChildPositions[child.Name] = new[] { child.Position[0], child.Position[1] };
            }

            Debug.WriteLine($"BoxResizer: Starting resize at ({_startDragPoint.X}, {_startDragPoint.Y})");
            Debug.WriteLine($"BoxResizer: Original grid size: {_originalGridWidth}x{_originalGridHeight}");
            e.Handled = true;
            ((UIElement)sender).CaptureMouse();
        }

        private void HandleResizeDrag(object sender, MouseEventArgs e, Border referenceBox) {
            if (!_isDragging || _currentBox == null || _currentContainer == null || _resizeHandle == null) return;
            if (referenceBox.Parent is not Canvas canvas) return;

            Point currentPoint = e.GetPosition(canvas);
            double deltaXCanvas = currentPoint.X - _startDragPoint.X;
            double deltaYCanvas = currentPoint.Y - _startDragPoint.Y;

            Debug.WriteLine($"BoxResizer: MouseMove - Canvas Delta: ({deltaXCanvas}, {deltaYCanvas})");

            // --- MODIFIED Logic for newGridWidth and newGridHeight ---
            // Calculate change in grid units directly using the signed delta
            int gridDeltaX = (int)Math.Round(deltaXCanvas / CanvasScaleFactor);
            int newGridWidth = _originalGridWidth + gridDeltaX;
            newGridWidth = Math.Max(MinGridSize, newGridWidth); // Ensure it doesn't go below minimum

            int gridDeltaY = (int)Math.Round(deltaYCanvas / CanvasScaleFactor);
            int newGridHeight = _originalGridHeight + gridDeltaY;
            newGridHeight = Math.Max(MinGridSize, newGridHeight);
            // --- END MODIFIED Logic ---

            // For a bottom-right handle, the top-left position of the box does NOT change during resize.
            double currentCanvasLeft = Canvas.GetLeft(_currentBox);
            double currentCanvasTop = Canvas.GetTop(_currentBox);

            // The draggedLeft/Up flags are no longer used to shift the box's origin for this handle type.
            // We pass false for them as prospectiveX/Y will be originalX/Y in IsValidResize under this model.
            // Alternatively, IsValidResize can be simplified to not need them if it always uses original X/Y.
            // For now, let's pass false to signify the top-left anchor isn't being programmatically shifted by HandleResizeDrag.
            bool effectivelyDraggedLeftToShiftOrigin = false;
            bool effectivelyDraggedUpToShiftOrigin = false;

            // This check remains, but now applies to fixed top-left.
            // It's unlikely to be hit if currentCanvasLeft/Top are already >=0 unless MinGridSize makes width/height too big from 0,0
            // if (currentCanvasLeft < 0 || currentCanvasTop < 0) // This check might be redundant if top-left is fixed and was valid
            // {
            //    Debug.WriteLine("BoxResizer: Resize would place box out of bounds (negative canvas coordinates for fixed top-left)");
            //    return;
            // }


            // Reset other boxes to original positions before checking if resize is valid
            foreach (var child in _rootContainer.Children) {
                if (child.Name != _currentContainer.Name && _originalChildPositions.ContainsKey(child.Name)) {
                    child.Position[0] = _originalChildPositions[child.Name][0];
                    child.Position[1] = _originalChildPositions[child.Name][1];
                }
            }

            if (IsValidResize(_currentContainer, newGridWidth, newGridHeight,
                              effectivelyDraggedLeftToShiftOrigin, effectivelyDraggedUpToShiftOrigin)) {
                Debug.WriteLine("BoxResizer: Resize is valid - updating UI for current box and handle");
                // Update the visual representation of the box being resized
                // Top-left position remains the same for a bottom-right handle resize.
                _currentBox.Width = newGridWidth * CanvasScaleFactor;
                _currentBox.Height = newGridHeight * CanvasScaleFactor;
                Canvas.SetLeft(_currentBox, currentCanvasLeft); // Explicitly set, though should be unchanged
                Canvas.SetTop(_currentBox, currentCanvasTop);   // Explicitly set, though should be unchanged

                UpdateResizeHandlePosition(_resizeHandle, _currentBox);
            } else {
                Debug.WriteLine("BoxResizer: Resize is invalid - collision or bounds issue detected by IsValidResize");
            }
        }

        private void HandleResizeEnd(object sender, MouseButtonEventArgs e) {
            Debug.WriteLine("BoxResizer: Handle MouseUp event triggered");
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

            Debug.WriteLine($"BoxResizer: Final grid size: {finalGridWidth}x{finalGridHeight}, Position: [{finalGridX}, {finalGridY}]");

            bool hasSizeChanged = finalGridWidth != _originalGridWidth || finalGridHeight != _originalGridHeight;
            bool hasPositionChanged = false;
            if (_originalChildPositions.TryGetValue(_currentContainer.Name, out var originalPos)) {
                hasPositionChanged = finalGridX != originalPos[0] || finalGridY != originalPos[1];
            }

            if (finalGridX < 0 || finalGridY < 0 || finalGridWidth < MinGridSize || finalGridHeight < MinGridSize) {
                Debug.WriteLine($"BoxResizer: Invalid final state detected. Reverting.");
                RevertCurrentBoxToOriginalState();
                _originalChildPositions.Clear();
                return;
            }

            if (hasSizeChanged || hasPositionChanged) {
                try {
                    var containerInRoot = _rootContainer.Children.FirstOrDefault(c => c.Name == _currentContainer.Name);
                    if (containerInRoot != null) {
                        containerInRoot.Size = new int[] { finalGridWidth, finalGridHeight };
                        containerInRoot.Position = new int[] { finalGridX, finalGridY };
                        Debug.WriteLine($"BoxResizer: Updated container '{containerInRoot.Name}' in root: Size [{containerInRoot.Size[0]}, {containerInRoot.Size[1]}], Pos [{containerInRoot.Position[0]}, {containerInRoot.Position[1]}]");
                    } else {
                        Debug.WriteLine($"BoxResizer: WARNING - Couldn't find container {_currentContainer.Name} in root children during save.");
                    }

                    Debug.WriteLine("BoxResizer: Pre-serialize state of root's children:");
                    foreach (var child in _rootContainer.Children) {
                        Debug.WriteLine($"  {child.Name} at [{child.Position[0]}, {child.Position[1]}] size [{child.Size[0]}, {child.Size[1]}]");
                    }

                    var json = JsonSerializer.Serialize(_rootContainer, JsonSerializerOptions);
                    Debug.WriteLine($"BoxResizer: Serialized JSON: {json}");
                    var clonedRootForSave = JsonSerializer.Deserialize<StorageContainer>(json, JsonSerializerOptions);

                    Debug.WriteLine("BoxResizer: Saving changes to temporary file");
                    _storageLoader.SaveTemporary(clonedRootForSave);
                    UpdateAllHandles();
                } catch (Exception ex) {
                    Debug.WriteLine($"BoxResizer: Error during save: {ex.Message}");
                    Debug.WriteLine($"BoxResizer: Stack trace: {ex.StackTrace}");
                    RevertCurrentBoxToOriginalState();
                }
            } else {
                Debug.WriteLine("BoxResizer: No change in size or position - not saving");
            }

            _originalChildPositions.Clear();
            _currentBox = null;
            _currentContainer = null;
            _resizeHandle = null;
        }

        private void RevertCurrentBoxToOriginalState() {
            if (_currentBox == null || _currentContainer == null || _resizeHandle == null ||
                !_originalChildPositions.TryGetValue(_currentContainer.Name, out var originalPos)) {
                Debug.WriteLine("BoxResizer: Could not revert current box, necessary info missing.");
                return;
            }

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

        private bool IsValidResize(StorageContainer activeContainer, int newGridWidth, int newGridHeight,
                                   bool isOriginShiftedLeft, bool isOriginShiftedUp) // Parameters renamed for clarity
        {
            if (!_originalChildPositions.TryGetValue(activeContainer.Name, out var originalPositionArray)) {
                Debug.WriteLine($"BoxResizer: ERROR - Could not find original position for active container {activeContainer.Name}");
                return false;
            }
            int origGridX = originalPositionArray[0];
            int origGridY = originalPositionArray[1];

            // --- MODIFIED: For a standard bottom-right handle, prospective X/Y are the original X/Y ---
            int prospectiveNewGridX = origGridX;
            int prospectiveNewGridY = origGridY;

            // This logic is removed if we assume a fixed top-left anchor for bottom-right handle
            // if (isOriginShiftedLeft && newGridWidth != _originalGridWidth) // _originalGridWidth is for activeContainer
            // {
            //     prospectiveNewGridX = origGridX - (newGridWidth - _originalGridWidth);
            // }
            // if (isOriginShiftedUp && newGridHeight != _originalGridHeight) // _originalGridHeight is for activeContainer
            // {
            //     prospectiveNewGridY = origGridY - (newGridHeight - _originalGridHeight);
            // }
            // --- END MODIFIED ---

            if (!AdjustAndValidateBounds(prospectiveNewGridX, prospectiveNewGridY, newGridWidth, newGridHeight))
                return false;

            bool success = true;

            // _originalGridWidth and _originalGridHeight here refer to the state at MouseDown for the active container
            success = HandleHorizontalContainerAdjustments(activeContainer,
                origGridX, _originalGridWidth,
                prospectiveNewGridX, newGridWidth,
                prospectiveNewGridY, newGridHeight);
            if (!success) {
                RevertNonActiveChildPositions(activeContainer.Name);
                return false;
            }

            success = HandleVerticalContainerAdjustments(activeContainer,
                origGridY, _originalGridHeight,
                prospectiveNewGridY, newGridHeight,
                prospectiveNewGridX, newGridWidth);
            if (!success) {
                RevertNonActiveChildPositions(activeContainer.Name);
                return false;
            }

            var rootVersionOfActiveContainer = _rootContainer.Children.FirstOrDefault(c => c.Name == activeContainer.Name);
            if (rootVersionOfActiveContainer != null) {
                rootVersionOfActiveContainer.Position[0] = prospectiveNewGridX; // Will be origGridX
                rootVersionOfActiveContainer.Position[1] = prospectiveNewGridY; // Will be origGridY
                rootVersionOfActiveContainer.Size[0] = newGridWidth;
                rootVersionOfActiveContainer.Size[1] = newGridHeight;
            }

            UpdateBoxPositions();
            Debug.WriteLine("BoxResizer: IsValidResize successful - all boxes repositioned as needed in data model and UI updated.");
            return true;
        }

        private void RevertNonActiveChildPositions(string activeContainerName) {
            Debug.WriteLine("BoxResizer: Reverting positions of non-active children due to failed adjustment.");
            foreach (var child in _rootContainer.Children) {
                if (child.Name != activeContainerName && _originalChildPositions.TryGetValue(child.Name, out var originalPos)) {
                    child.Position[0] = originalPos[0];
                    child.Position[1] = originalPos[1];
                }
            }
        }

        private bool AdjustAndValidateBounds(int newGridX, int newGridY, int newGridWidth, int newGridHeight) {
            if (newGridX < 0 || newGridY < 0) {
                Debug.WriteLine($"BoxResizer: Resize invalid - would result in negative grid position [{newGridX}, {newGridY}]");
                return false;
            }
            if (newGridX + newGridWidth > MaxGridCoordinate || newGridY + newGridHeight > MaxGridCoordinate) {
                Debug.WriteLine($"BoxResizer: Resize invalid - would go out of grid bounds (max coord: {MaxGridCoordinate})");
                return false;
            }
            if (newGridWidth < MinGridSize || newGridHeight < MinGridSize) {
                Debug.WriteLine($"BoxResizer: Resize invalid - size cannot be less than min [{newGridWidth}, {newGridHeight}]");
                return false;
            }
            return true;
        }

        private bool HandleHorizontalContainerAdjustments(
            StorageContainer activeContainer,
            int mainOriginalGridX, int mainOriginalGridWidth,
            int mainProspectiveNewGridX, int mainNewGridWidth,
            int mainProspectiveNewGridY, int mainNewGridHeight) // <-- Added mainNewGridHeight for Y-span of active
        {
            int widthChange = mainNewGridWidth - mainOriginalGridWidth;
            int activeContainerOldRightEdge = mainOriginalGridX + mainOriginalGridWidth;
            int activeContainerNewRightEdge = mainProspectiveNewGridX + mainNewGridWidth;

            if (widthChange > 0) // Expanding
            {
                int pushStartX = mainOriginalGridX + mainOriginalGridWidth;
                int pushDistance = activeContainerNewRightEdge - pushStartX;

                if (mainProspectiveNewGridX < mainOriginalGridX) // Expanded left, potentially pushing boxes originally to its left
                {
                    // This case needs careful definition of pushStartX and pushDistance
                    // For simplicity, the original logic primarily pushed boxes to the right of the *original* right edge.
                    // If expanding left also needs to push, this section would need more complex logic.
                    // The current code pushes boxes that were to the right of original right edge.
                }


                if (pushDistance > 0) {
                    var boxesToPush = _rootContainer.Children
                        .Where(b => b.Name != activeContainer.Name && b.Depth == activeContainer.Depth)
                        .Where(b => _originalChildPositions[b.Name][0] >= mainOriginalGridX + mainOriginalGridWidth)
                        // Vertical overlap with the active container's NEW prospective Y-span
                        .Where(b => _originalChildPositions[b.Name][1] < mainProspectiveNewGridY + mainNewGridHeight &&
                                    _originalChildPositions[b.Name][1] + b.Size[1] > mainProspectiveNewGridY)
                        .OrderBy(b => _originalChildPositions[b.Name][0])
                        .ToList();

                    Debug.WriteLine($"BoxResizer: Horizontally pushing {boxesToPush.Count} boxes right by {pushDistance}");
                    foreach (var boxToPush in boxesToPush) {
                        int newBoxX = _originalChildPositions[boxToPush.Name][0] + pushDistance;
                        if (newBoxX + boxToPush.Size[0] > MaxGridCoordinate) {
                            Debug.WriteLine($"BoxResizer: Can't push '{boxToPush.Name}' right - would go out of bounds.");
                            return false;
                        }
                        bool wouldOverlap = _rootContainer.Children
                            .Where(other => other.Name != boxToPush.Name && other.Name != activeContainer.Name && other.Depth == activeContainer.Depth)
                            .Any(other => {
                                int otherCurrentX = boxesToPush.Contains(other) ? _originalChildPositions[other.Name][0] + pushDistance : _originalChildPositions[other.Name][0];
                                int otherOriginalY = _originalChildPositions[other.Name][1]; // Use original Y for non-pushed item

                                return newBoxX < otherCurrentX + other.Size[0] &&
                                       newBoxX + boxToPush.Size[0] > otherCurrentX &&
                                       _originalChildPositions[boxToPush.Name][1] < otherOriginalY + other.Size[1] && // boxToPush original Y
                                       _originalChildPositions[boxToPush.Name][1] + boxToPush.Size[1] > otherOriginalY;
                            });
                        if (wouldOverlap) {
                            Debug.WriteLine($"BoxResizer: Can't push '{boxToPush.Name}' right - would overlap another box.");
                            return false;
                        }
                        boxToPush.Position[0] = newBoxX;
                        Debug.WriteLine($"BoxResizer: Pushed '{boxToPush.Name}' right to [{newBoxX}, {boxToPush.Position[1]}]");
                    }
                }
            } else if (widthChange < 0) // Contracting
              {
                var boxesToPull = _rootContainer.Children
                    .Where(b => b.Name != activeContainer.Name && b.Depth == activeContainer.Depth)
                    .Where(b => _originalChildPositions[b.Name][0] >= activeContainerNewRightEdge)
                    .Where(b => _originalChildPositions[b.Name][0] >= mainOriginalGridX + mainOriginalGridWidth)
                    // Vertical overlap with the active container's NEW prospective Y-span
                    .Where(b => _originalChildPositions[b.Name][1] < mainProspectiveNewGridY + mainNewGridHeight &&
                                _originalChildPositions[b.Name][1] + b.Size[1] > mainProspectiveNewGridY)
                    .OrderBy(b => _originalChildPositions[b.Name][0])
                    .ToList();

                if (boxesToPull.Any()) {
                    Debug.WriteLine($"BoxResizer: Horizontally pulling {boxesToPull.Count} boxes left.");
                    var boxesByColumn = boxesToPull
                        .GroupBy(b => _originalChildPositions[b.Name][0])
                        .OrderBy(g => g.Key);

                    int previousColumnNewRightEdge = activeContainerNewRightEdge;
                    foreach (var columnGroup in boxesByColumn) {
                        int targetXForThisColumn = previousColumnNewRightEdge;
                        // Calculate actual shift for this column based on its original position
                        int shiftAmount = columnGroup.Key - targetXForThisColumn;


                        foreach (var boxToPull in columnGroup) {
                            // Ensure not to pull beyond original position or cause negative coords
                            int newPulledX = _originalChildPositions[boxToPull.Name][0] - shiftAmount;
                            if (newPulledX < 0) newPulledX = 0; // Boundary condition

                            boxToPull.Position[0] = newPulledX;
                            Debug.WriteLine($"BoxResizer: Pulled '{boxToPull.Name}' left to [{boxToPull.Position[0]}, {boxToPull.Position[1]}]");
                        }
                        int maxPulledWidthInColumn = columnGroup.Max(b => b.Size[0]);
                        previousColumnNewRightEdge = targetXForThisColumn + maxPulledWidthInColumn;
                    }
                }
            }
            return true;
        }

        private bool HandleVerticalContainerAdjustments(
            StorageContainer activeContainer,
            int mainOriginalGridY, int mainOriginalGridHeight,
            int mainProspectiveNewGridY, int mainNewGridHeight,
            int mainProspectiveNewGridX, int mainNewGridWidth) // <-- Added mainNewGridWidth for X-span of active
        {
            int heightChange = mainNewGridHeight - mainOriginalGridHeight;
            int activeContainerOldBottomEdge = mainOriginalGridY + mainOriginalGridHeight;
            int activeContainerNewBottomEdge = mainProspectiveNewGridY + mainNewGridHeight;

            if (heightChange > 0) // Expanding
            {
                int pushStartY = mainOriginalGridY + mainOriginalGridHeight;
                int pushDistance = activeContainerNewBottomEdge - pushStartY;

                if (mainProspectiveNewGridY < mainOriginalGridY) // Expanded up
                {
                    // Similar to horizontal, this case might need more specific logic if it's to push boxes above.
                }


                if (pushDistance > 0) {
                    var boxesToPush = _rootContainer.Children
                        .Where(b => b.Name != activeContainer.Name && b.Depth == activeContainer.Depth)
                        .Where(b => _originalChildPositions[b.Name][1] >= mainOriginalGridY + mainOriginalGridHeight)
                        // Horizontal overlap with the active container's NEW prospective X-span
                        .Where(b => _originalChildPositions[b.Name][0] < mainProspectiveNewGridX + mainNewGridWidth &&
                                    _originalChildPositions[b.Name][0] + b.Size[0] > mainProspectiveNewGridX)
                        .OrderBy(b => _originalChildPositions[b.Name][1])
                        .ToList();

                    Debug.WriteLine($"BoxResizer: Vertically pushing {boxesToPush.Count} boxes down by {pushDistance}");
                    foreach (var boxToPush in boxesToPush) {
                        int newBoxY = _originalChildPositions[boxToPush.Name][1] + pushDistance;
                        if (newBoxY + boxToPush.Size[1] > MaxGridCoordinate) {
                            Debug.WriteLine($"BoxResizer: Can't push '{boxToPush.Name}' down - would go out of bounds.");
                            return false;
                        }
                        bool wouldOverlap = _rootContainer.Children
                            .Where(other => other.Name != boxToPush.Name && other.Name != activeContainer.Name && other.Depth == activeContainer.Depth)
                            .Any(other => {
                                int otherCurrentY = boxesToPush.Contains(other) ? _originalChildPositions[other.Name][1] + pushDistance : _originalChildPositions[other.Name][1];
                                int otherOriginalX = _originalChildPositions[other.Name][0]; // Use original X for non-pushed item

                                return newBoxY < otherCurrentY + other.Size[1] &&
                                       newBoxY + boxToPush.Size[1] > otherCurrentY &&
                                       _originalChildPositions[boxToPush.Name][0] < otherOriginalX + other.Size[0] && // boxToPush original X
                                       _originalChildPositions[boxToPush.Name][0] + boxToPush.Size[0] > otherOriginalX;
                            });
                        if (wouldOverlap) {
                            Debug.WriteLine($"BoxResizer: Can't push '{boxToPush.Name}' down - would overlap.");
                            return false;
                        }
                        boxToPush.Position[1] = newBoxY;
                        Debug.WriteLine($"BoxResizer: Pushed '{boxToPush.Name}' down to [{boxToPush.Position[0]}, {newBoxY}]");
                    }
                }
            } else if (heightChange < 0) // Contracting
              {
                var boxesToPull = _rootContainer.Children
                    .Where(b => b.Name != activeContainer.Name && b.Depth == activeContainer.Depth)
                    .Where(b => _originalChildPositions[b.Name][1] >= activeContainerNewBottomEdge)
                    .Where(b => _originalChildPositions[b.Name][1] >= mainOriginalGridY + mainOriginalGridHeight)
                     // Horizontal overlap with the active container's NEW prospective X-span
                     .Where(b => _originalChildPositions[b.Name][0] < mainProspectiveNewGridX + mainNewGridWidth &&
                                _originalChildPositions[b.Name][0] + b.Size[0] > mainProspectiveNewGridX)
                    .OrderBy(b => _originalChildPositions[b.Name][1])
                    .ToList();

                if (boxesToPull.Any()) {
                    Debug.WriteLine($"BoxResizer: Vertically pulling {boxesToPull.Count} boxes up.");
                    var boxesByRow = boxesToPull
                        .GroupBy(b => _originalChildPositions[b.Name][1])
                        .OrderBy(g => g.Key);

                    int previousRowNewBottomEdge = activeContainerNewBottomEdge;
                    foreach (var rowGroup in boxesByRow) {
                        int targetYForRow = previousRowNewBottomEdge;
                        int shiftAmount = rowGroup.Key - targetYForRow;

                        foreach (var boxToPull in rowGroup) {
                            int newPulledY = _originalChildPositions[boxToPull.Name][1] - shiftAmount;
                            if (newPulledY < 0) newPulledY = 0; // Boundary condition

                            boxToPull.Position[1] = newPulledY;
                            Debug.WriteLine($"BoxResizer: Pulled '{boxToPull.Name}' up to [{boxToPull.Position[0]}, {boxToPull.Position[1]}]");
                        }
                        int maxPulledHeightInRow = rowGroup.Max(b => b.Size[1]);
                        previousRowNewBottomEdge = targetYForRow + maxPulledHeightInRow;
                    }
                }
            }
            return true;
        }

        private void UpdateBoxPositions() {
            Debug.WriteLine("BoxResizer: Updating all box visuals from _rootContainer data.");
            Canvas canvas = _currentBox?.Parent as Canvas;
            if (canvas == null) {
                if (_rootContainer.Children.Any() && _handleMap.Any()) {
                    var firstHandleVisual = _handleMap.Values.FirstOrDefault();
                    if (firstHandleVisual?.Parent is Canvas handleCanvas) {
                        canvas = handleCanvas;
                    }
                }
            }

            if (canvas == null) {
                Debug.WriteLine($"BoxResizer ({MethodBase.GetCurrentMethod()?.Name}): Relevant canvas could not be determined. Skipping UI update.");
                return;
            }

            var allUiBoxes = canvas.Children.OfType<Border>()
                .Where(b => b.DataContext is StorageContainer)
                .ToList();
            Debug.WriteLine($"BoxResizer: Found {allUiBoxes.Count} UI boxes in canvas to update.");

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
                        Debug.WriteLine($"BoxResizer: Synced and Updated UI for {correspondingRootChild.Name} to Pos:[{correspondingRootChild.Position[0]},{correspondingRootChild.Position[1]}] Size:[{correspondingRootChild.Size[0]},{correspondingRootChild.Size[1]}]");
                    }
                }
            }
        }

        private void UpdateAllHandles() {
            Canvas canvas = _currentBox?.Parent as Canvas;
            if (canvas == null) {
                if (_rootContainer.Children.Any() && _handleMap.Any()) {
                    var firstHandleVisual = _handleMap.Values.FirstOrDefault();
                    if (firstHandleVisual?.Parent is Canvas handleCanvas) {
                        canvas = handleCanvas;
                    }
                }
            }
            if (canvas == null) {
                Debug.WriteLine($"BoxResizer ({MethodBase.GetCurrentMethod()?.Name}): Relevant canvas could not be determined. Skipping UI update for handles.");
                return;
            }

            var allUiBoxes = canvas.Children.OfType<Border>()
                .Where(b => b.DataContext is StorageContainer)
                .ToList();

            foreach (var uiBox in allUiBoxes) {
                if (uiBox.DataContext is StorageContainer containerData) {
                    if (_handleMap.TryGetValue(containerData.Name, out Rectangle handle)) {
                        UpdateResizeHandlePosition(handle, uiBox);
                    }
                }
            }
            Debug.WriteLine("BoxResizer: Updated all handle positions.");
        }

        public void ResizeWithOffset(StorageContainer container, int newGridWidth, int newGridHeight, int offsetGridX, int offsetGridY) {
            var targetContainer = _rootContainer.Children.FirstOrDefault(c => c.Name == container.Name);
            if (targetContainer == null) {
                Debug.WriteLine($"BoxResizer: ResizeWithOffset - Container {container.Name} not found in root.");
                return;
            }

            targetContainer.Position[0] += offsetGridX;
            targetContainer.Position[1] += offsetGridY;
            targetContainer.Size[0] = Math.Max(MinGridSize, newGridWidth);
            targetContainer.Size[1] = Math.Max(MinGridSize, newGridHeight);

            if (targetContainer.Position[0] < 0) targetContainer.Position[0] = 0;
            if (targetContainer.Position[1] < 0) targetContainer.Position[1] = 0;
            if (targetContainer.Position[0] + targetContainer.Size[0] > MaxGridCoordinate)
                targetContainer.Size[0] = MaxGridCoordinate - targetContainer.Position[0];
            if (targetContainer.Position[1] + targetContainer.Size[1] > MaxGridCoordinate)
                targetContainer.Size[1] = MaxGridCoordinate - targetContainer.Position[1];

            UpdateBoxPositions();
            UpdateAllHandles();

            Debug.WriteLine($"BoxResizer: Programmatic resize for {targetContainer.Name}. New Pos:[{targetContainer.Position[0]},{targetContainer.Position[1]}], Size:[{targetContainer.Size[0]},{targetContainer.Size[1]}]");
            _storageLoader.SaveTemporary(_rootContainer);
        }

        public void ClearHandles(Canvas canvas) {
            if (canvas == null) {
                Debug.WriteLine("BoxResizer: ClearHandles called with null canvas.");
                return;
            }
            var handlesToRemove = canvas.Children.OfType<Rectangle>()
                .Where(r => r.Tag != null && !string.IsNullOrEmpty(r.Tag.ToString()) && _handleMap.ContainsKey(r.Tag.ToString()))
                .ToList();

            Debug.WriteLine($"BoxResizer: Clearing {handlesToRemove.Count} resize handles from canvas.");

            foreach (var handle in handlesToRemove) {
                canvas.Children.Remove(handle);
            }
            _handleMap.Clear();
        }
    }
}