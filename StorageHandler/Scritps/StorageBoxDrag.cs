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
        private readonly Action? _refreshUi;

        // Drag tracking
        private bool _isDragging;
        private Point _dragStartPoint;
        private Border? _currentDragBox;
        private int[]? _originalPosition;
        private readonly Dictionary<string, int[]> _originalChildPositions = new();
        private Border? _currentHandle;

        public const int MaxGridCoordinate = 10;

        private const double DragPixelThreshold = 2.0; // ignore sub-pixel jitters
        private static readonly JsonSerializerOptions JsonOptionsIndented = new() { WriteIndented = true };

        private string GetStr(string key) {
            return Application.Current.TryFindResource(key) as string ?? key;
        }

        public StorageBoxDrag(Canvas storageGrid, StorageLoader storageLoader, StorageContainer rootContainer, BoxResizer boxResizer, Action? refreshUi = null) {
            _storageGrid = storageGrid;
            _storageLoader = storageLoader;
            _rootContainer = rootContainer;
            _boxResizer = boxResizer;
            _refreshUi = refreshUi;

            if (DebugDrag) {
                Debug.WriteLine("StorageBoxDrag: Initialized");
            }
        }

        public void AttachDragHandlers(Border box, StorageContainer container) {
            box.MouseLeftButtonDown += (sender, e) => Box_MouseLeftButtonDown(sender, e, container);
            box.MouseMove += (sender, e) => Box_MouseMove(sender, e);
            box.MouseLeftButtonUp += (sender, e) => Box_MouseLeftButtonUp(sender, e, container);

            if (DebugDrag) {
                Debug.WriteLine($"StorageBoxDrag: Attached drag handlers to {container.Name}");
            }
        }

        private void Box_MouseLeftButtonDown(object sender, MouseButtonEventArgs e, StorageContainer container) {
            if (sender is not Border box || e.ClickCount != 1) {
                return;
            }

            if (DebugDrag) {
                Debug.WriteLine($"StorageBoxDrag: Starting drag for {container.Name}");
            }

            _isDragging = true;
            _dragStartPoint = e.GetPosition(_storageGrid);
            _currentDragBox = box;
            _originalPosition = new[] { container.Position[0], container.Position[1] };

            // Snapshot original positions for collision checks/restores
            _originalChildPositions.Clear();
            foreach (var child in _rootContainer.Children) {
                _originalChildPositions[child.Name] = new[] { child.Position[0], child.Position[1] };
            }

            // Hide resize handle while dragging
            _currentHandle = FindResizeHandle(container.Name);
            if (_currentHandle != null) {
                _currentHandle.Visibility = Visibility.Hidden;
            }

            // Bring dragged box to front and capture mouse
            Canvas.SetZIndex(box, 1000);
            box.CaptureMouse();
            e.Handled = true;
        }

        private void Box_MouseMove(object sender, MouseEventArgs e) {
            if (!_isDragging || _currentDragBox == null) {
                return;
            }

            var currentPosition = e.GetPosition(_storageGrid);
            var deltaX = currentPosition.X - _dragStartPoint.X;
            var deltaY = currentPosition.Y - _dragStartPoint.Y;

            if (Math.Abs(deltaX) < DragPixelThreshold && Math.Abs(deltaY) < DragPixelThreshold) {
                return; // ignore tiny movements
            }

            var newLeft = Math.Max(0, Canvas.GetLeft(_currentDragBox) + deltaX);
            var newTop = Math.Max(0, Canvas.GetTop(_currentDragBox) + deltaY);

            Canvas.SetLeft(_currentDragBox, newLeft);
            Canvas.SetTop(_currentDragBox, newTop);

            _dragStartPoint = currentPosition;
            e.Handled = true;
        }

        private void Box_MouseLeftButtonUp(object sender, MouseButtonEventArgs e, StorageContainer container) {
            if (!_isDragging || _currentDragBox == null) {
                return;
            }

            if (DebugDrag) {
                Debug.WriteLine($"StorageBoxDrag: Ending drag for {container.Name}");
            }

            _isDragging = false;
            _currentDragBox.ReleaseMouseCapture();

            // Snap current pixel to nearest cell before computing grid
            var snappedLeft = Math.Round(Canvas.GetLeft(_currentDragBox) / BoxResizer.CanvasScaleFactor) * BoxResizer.CanvasScaleFactor;
            var snappedTop = Math.Round(Canvas.GetTop(_currentDragBox) / BoxResizer.CanvasScaleFactor) * BoxResizer.CanvasScaleFactor;
            Canvas.SetLeft(_currentDragBox, Math.Max(0, snappedLeft));
            Canvas.SetTop(_currentDragBox, Math.Max(0, snappedTop));

            var newGridX = (int)Math.Round(Canvas.GetLeft(_currentDragBox) / BoxResizer.CanvasScaleFactor);
            var newGridY = (int)Math.Round(Canvas.GetTop(_currentDragBox) / BoxResizer.CanvasScaleFactor);

            // Clamp within bounds considering box size
            newGridX = Math.Max(0, Math.Min(newGridX, MaxGridCoordinate - container.Size[0]));
            newGridY = Math.Max(0, Math.Min(newGridY, MaxGridCoordinate - container.Size[1]));

            var moved = _originalPosition != null && (newGridX != _originalPosition[0] || newGridY != _originalPosition[1]);

            if (moved) {
                if (DebugDrag) {
                    Debug.WriteLine($"StorageBoxDrag: Position changed from [{_originalPosition?[0]},{_originalPosition?[1]}] to [{newGridX},{newGridY}]");
                }

                var targetContainer = FindValidDropTargetByIoU(newGridX, newGridY, container);

                if (targetContainer != null) {
                    if (DebugDrag) {
                        Debug.WriteLine($"StorageBoxDrag: Box dropped on target container {targetContainer.Name}");
                    }

                    if (targetContainer.IsItemContainer) {
                        if (container.IsItemContainer) {
                            if (ConfirmMerge(container, targetContainer)) {
                                MergeItemLists(container, targetContainer);
                            } else {
                                RestoreBoxPosition(container);
                            }
                        } else {
                            MessageBox.Show(GetStr("Str_BoxInItemListError"), GetStr("Str_InvalidOp"), MessageBoxButton.OK, MessageBoxImage.Warning);
                            RestoreBoxPosition(container);
                        }
                    } else {
                        if (ConfirmMakeChildBox(container, targetContainer)) {
                            MakeBoxChildOf(container, targetContainer);
                        } else {
                            RestoreBoxPosition(container);
                        }
                    }
                } else {
                    TryMoveBox(container, newGridX, newGridY);
                }
            } else {
                if (DebugDrag) {
                    Debug.WriteLine("StorageBoxDrag: Position unchanged, snapping to grid");
                }

                if (_originalPosition != null) {
                    Canvas.SetLeft(_currentDragBox, _originalPosition[0] * BoxResizer.CanvasScaleFactor);
                    Canvas.SetTop(_currentDragBox, _originalPosition[1] * BoxResizer.CanvasScaleFactor);
                }
            }

            // Reset z-index and show handle
            Canvas.SetZIndex(_currentDragBox, 0);
            if (_currentHandle != null) {
                _currentHandle.Visibility = Visibility.Visible;
                _currentHandle = null;
            }

            _currentDragBox = null;
            _originalChildPositions.Clear();
            e.Handled = true;
        }

        private void RestoreBoxPosition(StorageContainer container) {
            if (DebugDrag) {
                Debug.WriteLine($"StorageBoxDrag: Restoring box {container.Name} to original position");
            }

            if (!_originalChildPositions.TryGetValue(container.Name, out var origPos)) {
                return;
            }

            var containerInRoot = _rootContainer.Children.FirstOrDefault(c => c.Name == container.Name);
            if (containerInRoot == null) {
                return;
            }

            containerInRoot.Position[0] = origPos[0];
            containerInRoot.Position[1] = origPos[1];

            try {
                _storageLoader.SaveTemporary(_rootContainer);

                if (_currentDragBox != null) {
                    Canvas.SetLeft(_currentDragBox, origPos[0] * BoxResizer.CanvasScaleFactor);
                    Canvas.SetTop(_currentDragBox, origPos[1] * BoxResizer.CanvasScaleFactor);
                    _boxResizer.ResizeWithOffset(container, container.Size[0], container.Size[1], 0, 0);
                }
            } catch (Exception ex) {
                if (DebugDrag) {
                    Debug.WriteLine($"StorageBoxDrag: Error during position restore - {ex.Message}");
                }
            }
        }

        private bool ConfirmMerge(StorageContainer source, StorageContainer target) {
            var result = MessageBox.Show(
                string.Format(GetStr("Str_MergeMsg"), source.Name, target.Name),
                GetStr("Str_MergeTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );
            return result == MessageBoxResult.Yes;
        }

        private void MergeItemLists(StorageContainer source, StorageContainer target) {
            try {
                var sourceItems = _storageLoader.LoadItems(source.Name);
                var targetItems = _storageLoader.LoadItems(target.Name);

                foreach (var sourceItem in sourceItems) {
                    var existingItem = targetItems.FirstOrDefault(t => t.Id == sourceItem.Id);
                    if (existingItem != null) {
                        existingItem.Quantity += sourceItem.Quantity;
                    } else {
                        targetItems.Add(sourceItem);
                    }
                }

                _storageLoader.SaveItems(target.Name, targetItems);
                _storageLoader.DeleteItems(source.Name);

                // Remove source container
                StorageContainer? currentParent;
                var containerToMove = _rootContainer.Children.FirstOrDefault(c => c.Name == source.Name);
                if (containerToMove != null) {
                    _rootContainer.Children.Remove(containerToMove);
                } else {
                    containerToMove = FindContainerByNameDeep(_rootContainer, source.Name, out currentParent);
                    if (currentParent != null && containerToMove != null) {
                        currentParent.Children.Remove(containerToMove);
                    }
                }

                _storageLoader.SaveTemporary(_rootContainer);
                _refreshUi?.Invoke();

            } catch (Exception ex) {
                MessageBox.Show(string.Format(GetStr("Str_MergeError"), ex.Message), GetStr("Str_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                RestoreBoxPosition(source);
            }
        }

        // Valid drop target detection using IoU and visibility constraints.
        private StorageContainer? FindValidDropTargetByIoU(int newX, int newY, StorageContainer dragged) {
            // Determining current visible siblings: find parent of dragged
            var draggedParent = FindParentOf(_rootContainer, dragged.Name);
            var candidates = draggedParent?.Children ?? _rootContainer.Children;

            // Compute dragged rect
            var dx1 = newX;
            var dy1 = newY;
            var dx2 = dx1 + dragged.Size[0];
            var dy2 = dy1 + dragged.Size[1];

            StorageContainer? best = null;
            double bestIou = 0.0;

            foreach (var box in candidates) {
                if (box.Name == dragged.Name) {
                    continue; // cannot drop onto itself
                }
                if (IsDescendantOf(dragged, box)) {
                    continue; // cannot drop onto a descendant to avoid cycles
                }

                // Only consider targets at same visible depth level or a direct parent candidate
                // Allow dropping into larger boxes even if their depth differs, but prefer same-level big boxes.
                // Practical rule: accept when target is larger and fully contains most of the dragged area.
                var ix1 = Math.Max(dx1, box.Position[0]);
                var iy1 = Math.Max(dy1, box.Position[1]);
                var ix2 = Math.Min(dx2, box.Position[0] + box.Size[0]);
                var iy2 = Math.Min(dy2, box.Position[1] + box.Size[1]);

                var iw = Math.Max(0, ix2 - ix1);
                var ih = Math.Max(0, iy2 - iy1);
                if (iw == 0 || ih == 0) {
                    continue;
                }

                var inter = iw * ih;
                var areaDragged = dragged.Size[0] * dragged.Size[1];
                var areaBox = box.Size[0] * box.Size[1];
                var union = areaDragged + areaBox - inter;
                var iou = union > 0 ? inter / (double)union : 0.0;

                // Thresholds:
                // - Require at least 0.5 IoU with the larger-area target, or
                // - Require at least 0.65 overlap of dragged over the target area when target is larger.
                var overlapDragged = inter / (double)areaDragged;
                var overlapTarget = inter / (double)areaBox;

                var acceptable =
                    iou >= 0.5 ||
                    (areaBox >= areaDragged && overlapDragged >= 0.65 && overlapTarget >= 0.25);

                if (!acceptable) {
                    continue;
                }

                // Prefer the candidate with the highest IoU
                if (iou > bestIou) {
                    bestIou = iou;
                    best = box;
                }
            }

            return best;
        }

        private StorageContainer? FindParentOf(StorageContainer root, string name) {
            foreach (var child in root.Children) {
                if (child.Name == name) {
                    return root;
                }
                var deep = FindParentOf(child, name);
                if (deep != null) {
                    return deep;
                }
            }
            return null;
        }

        private bool ConfirmMakeChildBox(StorageContainer boxToMove, StorageContainer targetParent) {
            if (IsDescendantOf(targetParent, boxToMove)) {
                MessageBox.Show(
                    string.Format(GetStr("Str_CircularRefError"), targetParent.Name, boxToMove.Name),
                    GetStr("Str_InvalidOp"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return false;
            }

            var directChildCount = boxToMove.Children.Count;
            var totalDescendants = CountTotalDescendants(boxToMove);

            var message = string.Format(GetStr("Str_MakeChildBoxMsg"), boxToMove.Name, targetParent.Name);
            if (directChildCount > 0) {
                message += string.Format(GetStr("Str_MakeChildBoxMsgDetails"), directChildCount, totalDescendants - directChildCount);
            }

            var result = MessageBox.Show(message, GetStr("Str_MakeChildBoxTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        private bool IsDescendantOf(StorageContainer potentialDescendant, StorageContainer potentialAncestor) {
            if (potentialDescendant.Name == potentialAncestor.Name) {
                return true;
            }
            foreach (var child in potentialDescendant.Children) {
                if (IsDescendantOf(child, potentialAncestor)) {
                    return true;
                }
            }
            return false;
        }

        private int CountTotalDescendants(StorageContainer container) {
            var count = container.Children.Count;
            foreach (var child in container.Children) {
                count += CountTotalDescendants(child);
            }
            return count;
        }

        private void MakeBoxChildOf(StorageContainer boxToMove, StorageContainer newParent) {
            if (DebugDrag) {
                Debug.WriteLine($"StorageBoxDrag: Making {boxToMove.Name} a child of {newParent.Name}");
            }

            StorageContainer? currentParent;
            var containerToMove = _rootContainer.Children.FirstOrDefault(c => c.Name == boxToMove.Name);
            if (containerToMove != null) {
                _rootContainer.Children.Remove(containerToMove);
                if (DebugDrag) {
                    Debug.WriteLine($"StorageBoxDrag: Removed {boxToMove.Name} from root container");
                }
                currentParent = null;
            } else {
                containerToMove = FindContainerByNameDeep(_rootContainer, boxToMove.Name, out currentParent);
                if (currentParent != null && containerToMove != null) {
                    currentParent.Children.Remove(containerToMove);
                    if (DebugDrag) {
                        Debug.WriteLine($"StorageBoxDrag: Removed {boxToMove.Name} from parent {currentParent.Name}");
                    }
                }
            }

            var targetParentContainer = FindContainerByNameDeep(_rootContainer, newParent.Name, out _);
            if (containerToMove == null || targetParentContainer == null) {
                if (DebugDrag) {
                    Debug.WriteLine("StorageBoxDrag: ERROR - Failed to find containers for move operation");
                }
                return;
            }

            // Reset position relative to new parent and adjust depths
            containerToMove.Position[0] = 0;
            containerToMove.Position[1] = 0;

            var depthAdjustment = targetParentContainer.Depth + 1 - containerToMove.Depth;
            AdjustDepthRecursively(containerToMove, depthAdjustment);

            targetParentContainer.Children.Add(containerToMove);

            try {
                var clonedRootForSave = JsonSerializer.Deserialize<StorageContainer>(JsonSerializer.Serialize(_rootContainer, JsonOptionsIndented), JsonOptionsIndented);
                if (clonedRootForSave != null) {
                    _storageLoader.SaveTemporary(clonedRootForSave);
                }

                MessageBox.Show(
                    string.Format(GetStr("Str_BoxChildSuccess"), boxToMove.Name, newParent.Name),
                    GetStr("Str_BoxStructureUpdated"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                _refreshUi?.Invoke();
            } catch (Exception ex) {
                if (DebugDrag) {
                    Debug.WriteLine($"StorageBoxDrag: Error saving changes - {ex.Message}");
                }

                // Revert changes on error
                targetParentContainer.Children.Remove(containerToMove);
                AdjustDepthRecursively(containerToMove, -depthAdjustment);

                if (currentParent != null) {
                    currentParent.Children.Add(containerToMove);
                } else {
                    _rootContainer!.Children.Add(containerToMove!);
                }
                _refreshUi?.Invoke();
            }

            // Reset visual position in current view; UI updates on navigation elsewhere
            if (_currentDragBox != null && _originalPosition != null) {
                Canvas.SetLeft(_currentDragBox, _originalPosition[0] * BoxResizer.CanvasScaleFactor);
                Canvas.SetTop(_currentDragBox, _originalPosition[1] * BoxResizer.CanvasScaleFactor);
            }
        }

        private void AdjustDepthRecursively(StorageContainer container, int depthAdjustment) {
            container.Depth += depthAdjustment;
            if (DebugDrag) {
                Debug.WriteLine($"StorageBoxDrag: Adjusted depth for {container.Name} to {container.Depth}");
            }
            foreach (var child in container.Children) {
                AdjustDepthRecursively(child, depthAdjustment);
            }
        }

        private StorageContainer? FindContainerByNameDeep(StorageContainer root, string name, out StorageContainer? parent) {
            parent = null;

            if (root.Name == name) {
                return root;
            }

            foreach (var child in root.Children) {
                if (child.Name == name) {
                    parent = root;
                    return child;
                }

                var result = FindContainerByNameDeep(child, name, out var foundParent);
                if (result != null) {
                    parent = foundParent ?? child;
                    return result;
                }
            }

            return null;
        }

        private Border? FindResizeHandle(string containerName) {
            return _storageGrid.Children.OfType<Border>()
                .FirstOrDefault(b => b.Tag as string == containerName);
        }

        private void TryMoveBox(StorageContainer container, int newGridX, int newGridY) {
            var originalX = container.Position[0];
            var originalY = container.Position[1];

            var containerInRoot = _rootContainer.Children.FirstOrDefault(c => c.Name == container.Name);
            if (containerInRoot == null) {
                return;
            }

            containerInRoot.Position[0] = newGridX;
            containerInRoot.Position[1] = newGridY;

            var hasCollision = CheckForCollisionsInParentScope(container);
            if (!hasCollision) {
                if (DebugDrag) {
                    Debug.WriteLine("StorageBoxDrag: New position valid, saving changes");
                }

                try {
                    var clonedRootForSave = JsonSerializer.Deserialize<StorageContainer>(JsonSerializer.Serialize(_rootContainer, JsonOptionsIndented), JsonOptionsIndented);
                    if (clonedRootForSave != null) {
                        _storageLoader.SaveTemporary(clonedRootForSave);
                    }

                    Canvas.SetLeft(_currentDragBox, newGridX * BoxResizer.CanvasScaleFactor);
                    Canvas.SetTop(_currentDragBox, newGridY * BoxResizer.CanvasScaleFactor);

                    _boxResizer.ResizeWithOffset(container, container.Size[0], container.Size[1], 0, 0);
                    return;
                } catch (Exception ex) {
                    if (DebugDrag) {
                        Debug.WriteLine($"StorageBoxDrag: Error saving - {ex.Message}");
                    }
                }
            } else {
                if (DebugDrag) {
                    Debug.WriteLine("StorageBoxDrag: Collision detected, reverting position");
                }
            }

            // Revert model and UI
            containerInRoot.Position[0] = originalX;
            containerInRoot.Position[1] = originalY;
            Canvas.SetLeft(_currentDragBox, originalX * BoxResizer.CanvasScaleFactor);
            Canvas.SetTop(_currentDragBox, originalY * BoxResizer.CanvasScaleFactor);
        }

        // Limit collision checks to the current parent’s children to avoid false positives across levels.
        private bool CheckForCollisionsInParentScope(StorageContainer moved) {
            var parent = FindParentOf(_rootContainer, moved.Name);
            var siblings = parent?.Children ?? _rootContainer.Children;

            foreach (var a in siblings) {
                foreach (var b in siblings) {
                    if (ReferenceEquals(a, b)) continue;
                    if (a.Depth != b.Depth) continue;
                    if (IsOverlapping(a, b)) {
                        if (DebugDrag) {
                            Debug.WriteLine($"CheckForCollisions: Collision detected between {a.Name} and {b.Name}");
                        }
                        return true;
                    }
                }

                // Check recursively inside each container to keep invariant
                if (CheckForCollisionsInContainer(a)) {
                    return true;
                }
            }

            return false;
        }

        private bool CheckForCollisions() {
            // Kept for backward compatibility; default to parent-scope first.
            foreach (var box1 in _rootContainer.Children) {
                foreach (var box2 in _rootContainer.Children) {
                    if (ReferenceEquals(box1, box2)) {
                        continue;
                    }

                    if (box1.Depth != box2.Depth) {
                        continue;
                    }

                    if (IsOverlapping(box1, box2)) {
                        if (DebugDrag) {
                            Debug.WriteLine($"CheckForCollisions: Collision detected between {box1.Name} and {box2.Name}");
                        }
                        return true;
                    }
                }

                if (CheckForCollisionsInContainer(box1)) {
                    return true;
                }
            }

            return false;
        }

        private bool CheckForCollisionsInContainer(StorageContainer container) {
            var children = container.Children;

            for (int i = 0; i < children.Count; i++) {
                for (int j = i + 1; j < children.Count; j++) {
                    var child1 = children[i];
                    var child2 = children[j];

                    if (child1.Depth != child2.Depth) {
                        continue;
                    }

                    if (IsOverlapping(child1, child2)) {
                        if (DebugDrag) {
                            Debug.WriteLine($"CheckForCollisions: Collision detected between {child1.Name} and {child2.Name} in container {container.Name}");
                        }
                        return true;
                    }
                }

                if (CheckForCollisionsInContainer(children[i])) {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOverlapping(StorageContainer a, StorageContainer b) {
            return a.Position[0] < b.Position[0] + b.Size[0] &&
                   a.Position[0] + a.Size[0] > b.Position[0] &&
                   a.Position[1] < b.Position[1] + b.Size[1] &&
                   a.Position[1] + a.Size[1] > b.Position[1];
        }
    }
}