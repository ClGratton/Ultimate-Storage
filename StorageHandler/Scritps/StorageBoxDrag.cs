using StorageHandler.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StorageHandler.Scripts {
    public class StorageBoxDrag {
        private static readonly bool DebugDrag = true;

        private readonly Canvas _storageGrid;
        private readonly StorageLoader _storageLoader;
        private readonly StorageContainer _rootContainer;
        private readonly BoxResizer _boxResizer;

        // Fields for tracking drag operations
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private Border _currentDragBox;
        private StorageContainer _currentDragContainer;
        private int[] _originalPosition;
        private Dictionary<string, int[]> _originalChildPositions = new();
        private Border _currentHandle;

        public const int MaxGridCoordinate = 10;
        private const int MinGridSize = 1;

        public StorageBoxDrag(Canvas storageGrid, StorageLoader storageLoader, StorageContainer rootContainer, BoxResizer boxResizer) {
            _storageGrid = storageGrid;
            _storageLoader = storageLoader;
            _rootContainer = rootContainer;
            _boxResizer = boxResizer;

            if (DebugDrag) Debug.WriteLine("StorageBoxDrag: Initialized");
        }

        public void AttachDragHandlers(Border box, StorageContainer container) {
            box.MouseLeftButtonDown += (sender, e) => Box_MouseLeftButtonDown(sender, e, container);
            box.MouseMove += (sender, e) => Box_MouseMove(sender, e, container);
            box.MouseLeftButtonUp += (sender, e) => Box_MouseLeftButtonUp(sender, e, container);

            if (DebugDrag) Debug.WriteLine($"StorageBoxDrag: Attached drag handlers to {container.Name}");
        }

        private void Box_MouseLeftButtonDown(object sender, MouseButtonEventArgs e, StorageContainer container) {
            if (sender is Border box && e.ClickCount == 1) {
                if (DebugDrag) Debug.WriteLine($"StorageBoxDrag: Starting drag for {container.Name}");

                _isDragging = true;
                _dragStartPoint = e.GetPosition(_storageGrid);
                _currentDragBox = box;
                _currentDragContainer = container;
                _originalPosition = new[] { container.Position[0], container.Position[1] };

                // Store original positions of all boxes for collision detection
                _originalChildPositions.Clear();
                foreach (var child in _rootContainer.Children) {
                    _originalChildPositions[child.Name] = new[] { child.Position[0], child.Position[1] };
                }

                // Find and hide the resize handle for this box
                _currentHandle = FindResizeHandle(container.Name);
                if (_currentHandle != null) {
                    _currentHandle.Visibility = Visibility.Hidden;
                }

                // Bring to front during drag
                Canvas.SetZIndex(box, 1000);

                // Capture mouse to receive events outside the element
                box.CaptureMouse();
                e.Handled = true;
            }
        }

        private void Box_MouseMove(object sender, MouseEventArgs e, StorageContainer container) {
            if (!_isDragging || _currentDragBox == null)
                return;

            // Get current mouse position
            Point currentPosition = e.GetPosition(_storageGrid);

            // Calculate the delta from the start position
            double deltaX = currentPosition.X - _dragStartPoint.X;
            double deltaY = currentPosition.Y - _dragStartPoint.Y;

            // Update the position of the box on the canvas (visual feedback)
            double newLeft = Math.Max(0, Canvas.GetLeft(_currentDragBox) + deltaX);
            double newTop = Math.Max(0, Canvas.GetTop(_currentDragBox) + deltaY);

            // Apply the new position
            Canvas.SetLeft(_currentDragBox, newLeft);
            Canvas.SetTop(_currentDragBox, newTop);

            // Update the start point for the next move
            _dragStartPoint = currentPosition;

            e.Handled = true;
        }

        private void Box_MouseLeftButtonUp(object sender, MouseButtonEventArgs e, StorageContainer container) {
            if (!_isDragging || _currentDragBox == null)
                return;

            if (DebugDrag) Debug.WriteLine($"StorageBoxDrag: Ending drag for {container.Name}");

            _isDragging = false;
            _currentDragBox.ReleaseMouseCapture();

            // Calculate the grid position based on the pixel position
            int newGridX = (int)Math.Round(Canvas.GetLeft(_currentDragBox) / BoxResizer.CanvasScaleFactor);
            int newGridY = (int)Math.Round(Canvas.GetTop(_currentDragBox) / BoxResizer.CanvasScaleFactor);

            // Ensure we're not out of bounds
            newGridX = Math.Max(0, Math.Min(newGridX, MaxGridCoordinate - container.Size[0]));
            newGridY = Math.Max(0, Math.Min(newGridY, MaxGridCoordinate - container.Size[1]));

            // Check if the position has changed
            if (newGridX != _originalPosition[0] || newGridY != _originalPosition[1]) {
                if (DebugDrag) Debug.WriteLine($"StorageBoxDrag: Position changed from [{_originalPosition[0]},{_originalPosition[1]}] to [{newGridX},{newGridY}]");

                // Try to move the box to the new position using BoxResizer's logic
                TryMoveBox(container, newGridX, newGridY);
            } else {
                if (DebugDrag) Debug.WriteLine($"StorageBoxDrag: Position unchanged, snapping to grid");

                // Reset position exactly to grid 
                Canvas.SetLeft(_currentDragBox, _originalPosition[0] * BoxResizer.CanvasScaleFactor);
                Canvas.SetTop(_currentDragBox, _originalPosition[1] * BoxResizer.CanvasScaleFactor);
            }

            // Reset z-index
            Canvas.SetZIndex(_currentDragBox, 0);

            // Show the resize handle again
            if (_currentHandle != null) {
                _currentHandle.Visibility = Visibility.Visible;
                _currentHandle = null;
            }

            _currentDragBox = null;
            _currentDragContainer = null;
            _originalChildPositions.Clear();

            e.Handled = true;
        }

        private Border FindResizeHandle(string containerName) {
            // Find the resize handle in the canvas based on the container name stored as a Tag
            return _storageGrid.Children.OfType<Border>()
                .FirstOrDefault(b => b.Tag as string == containerName);
        }

        private void TryMoveBox(StorageContainer container, int newGridX, int newGridY) {
            // Store the original position for reverting if necessary
            int originalX = container.Position[0];
            int originalY = container.Position[1];
            int originalWidth = container.Size[0];
            int originalHeight = container.Size[1];

            // Temporarily update the container position
            var containerInRoot = _rootContainer.Children.FirstOrDefault(c => c.Name == container.Name);
            if (containerInRoot != null) {
                containerInRoot.Position[0] = newGridX;
                containerInRoot.Position[1] = newGridY;

                // Check for collisions
                bool hasCollision = CheckForCollisions();

                if (!hasCollision) {
                    if (DebugDrag) Debug.WriteLine($"StorageBoxDrag: New position valid, saving changes");

                    try {
                        // Clone the root container for saving to avoid reference issues
                        var clonedRootForSave = JsonSerializer.Deserialize<StorageContainer>(
                            JsonSerializer.Serialize(_rootContainer, new JsonSerializerOptions { WriteIndented = true }));

                        _storageLoader.SaveTemporary(clonedRootForSave);

                        // Update the UI
                        Canvas.SetLeft(_currentDragBox, newGridX * BoxResizer.CanvasScaleFactor);
                        Canvas.SetTop(_currentDragBox, newGridY * BoxResizer.CanvasScaleFactor);

                        // Update the resize handles visually by using the public ResizeWithOffset method
                        // This effectively has the same result as calling UpdateAllHandles
                        _boxResizer.ResizeWithOffset(container, container.Size[0], container.Size[1], 0, 0);

                        return;
                    } catch (Exception ex) {
                        if (DebugDrag) Debug.WriteLine($"StorageBoxDrag: Error saving - {ex.Message}");
                    }
                } else {
                    if (DebugDrag) Debug.WriteLine("StorageBoxDrag: Collision detected, reverting position");
                }

                // If we reach here, revert the position
                containerInRoot.Position[0] = originalX;
                containerInRoot.Position[1] = originalY;
            }

            // Reset UI position to original 
            Canvas.SetLeft(_currentDragBox, originalX * BoxResizer.CanvasScaleFactor);
            Canvas.SetTop(_currentDragBox, originalY * BoxResizer.CanvasScaleFactor);
        }

        private bool CheckForCollisions() {
            // Check for overlaps between any two boxes
            foreach (var box1 in _rootContainer.Children) {
                foreach (var box2 in _rootContainer.Children.Where(b => b.Name != box1.Name)) {
                    if (box1.Position[0] < box2.Position[0] + box2.Size[0] &&
                        box1.Position[0] + box1.Size[0] > box2.Position[0] &&
                        box1.Position[1] < box2.Position[1] + box2.Size[1] &&
                        box1.Position[1] + box1.Size[1] > box2.Position[1]) {
                        return true; // Collision detected
                    }
                }
            }
            return false; // No collision
        }
    }
}
