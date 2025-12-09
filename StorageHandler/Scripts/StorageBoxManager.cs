using StorageHandler.Models;
using StorageHandler.Config;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;

namespace StorageHandler.Scripts {
    public class StorageBoxManager {
        private readonly Canvas _storageGrid;
        private BoxResizer? _boxResizer;
        private StorageLoader? _storageLoader;
        private StorageContainer? _rootContainer;
        private StorageBoxDrag? _boxDrag;
        public event MouseButtonEventHandler? Box_MouseDoubleClick;
        
        private class RenameContext {
            public StorageContainer? Container;
            public TextBlock? NameText;
            public TextBox? NameEditor;
        }
        private RenameContext? _currentRenameContext;

        private string GetStr(string key) {
            return StorageHandler.Properties.Resources.ResourceManager.GetString(key) ?? key;
        }

        public StorageBoxManager(Canvas storageGrid) {
            _storageGrid = storageGrid;
            _storageGrid.PreviewMouseLeftButtonDown += OnPreviewMouseDown;
        }

        public void UpdateLoader(StorageLoader loader) {
            _storageLoader = loader;
            // If we have a root container, re-initialize resizer
            if (_rootContainer != null) {
                InitializeResizer(loader, _rootContainer);
            }
        }

        public void InitializeResizer(StorageLoader storageLoader, StorageContainer rootContainer) {
            _storageLoader = storageLoader;
            _rootContainer = rootContainer;
            _boxResizer = new BoxResizer(storageLoader, rootContainer, ReloadAllBoxes);
            _boxDrag = new StorageBoxDrag(_storageGrid, storageLoader, rootContainer, _boxResizer, ReloadAllBoxes);
        }

        public void AddStorageBox(StorageContainer container) {
            var outerBorder = new Border {
                Width = container.Size[0] * AppConfig.CanvasScaleFactor,
                Height = container.Size[1] * AppConfig.CanvasScaleFactor,
                BorderBrush = Brushes.Black,
                BorderThickness = new System.Windows.Thickness(AppConfig.BoxBorderThickness),
                CornerRadius = new System.Windows.CornerRadius(AppConfig.BoxCornerRadius),
                DataContext = container
            };

            var box = new Border {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(container.Color)),
                CornerRadius = new System.Windows.CornerRadius(AppConfig.BoxCornerRadius - 1),
                Opacity = container.IsItemContainer ? 0.9 : 1.0
            };

            // Visual distinction for item lists: inner white border
            if (container.IsItemContainer) {
                box.BorderBrush = new SolidColorBrush(Colors.White);
                box.BorderThickness = new System.Windows.Thickness(5.0);
            }

            outerBorder.Child = box;
            var finalBox = outerBorder;

            var textColor = AppConfig.GetTextColorForBackground(container.Color);
            var nameText = new TextBlock {
                Text = container.Name,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textColor)),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };

            var nameEditor = new TextBox {
                Text = container.Name,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(AppConfig.BoxTextMargin, 0, AppConfig.BoxTextMargin, 0),
                TextAlignment = TextAlignment.Center,
                Visibility = Visibility.Collapsed
            };

            var grid = new Grid();
            grid.Children.Add(nameText);
            grid.Children.Add(nameEditor);
            box.Child = grid;

            Canvas.SetLeft(finalBox, container.Position[0] * AppConfig.CanvasScaleFactor);
            Canvas.SetTop(finalBox, container.Position[1] * AppConfig.CanvasScaleFactor);

            _boxDrag?.AttachDragHandlers(finalBox, container);

            finalBox.MouseLeftButtonDown += (sender, e) => {
                if (e.ClickCount == 2) {
                    Box_MouseDoubleClick?.Invoke(sender, e);
                }
            };

            nameText.MouseLeftButtonDown += (sender, e) => {
                if (e.ClickCount == 1) {
                    BeginRename(container, nameText, nameEditor);
                    e.Handled = true;
                }
            };

            nameEditor.KeyDown += (sender, e) => {
                if (e.Key == Key.Enter) {
                    CommitRename(container, nameText, nameEditor);
                    e.Handled = true;
                } else if (e.Key == Key.Escape) {
                    CancelRename(nameText, nameEditor, container.Name);
                    e.Handled = true;
                }
            };

            nameEditor.LostFocus += (sender, e) => {
                if (nameEditor.Visibility == Visibility.Visible) {
                    CommitRename(container, nameText, nameEditor);
                }
            };

            _storageGrid.Children.Add(finalBox);

            TryAddResizeHandle(finalBox, container);
        }

        private void BeginRename(StorageContainer container, TextBlock nameText, TextBox nameEditor) {
            nameEditor.Text = nameText.Text;
            nameText.Visibility = Visibility.Collapsed;
            nameEditor.Visibility = Visibility.Visible;
            nameEditor.Focus();
            nameEditor.SelectAll();
            _currentRenameContext = new RenameContext { Container = container, NameText = nameText, NameEditor = nameEditor };
        }

        private void CommitRename(StorageContainer container, TextBlock nameText, TextBox nameEditor) {
            if (_rootContainer == null || _storageLoader == null) return;

            var newName = (nameEditor.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(newName) || newName == container.Name) {
                CancelRename(nameText, nameEditor, container.Name);
                return;
            }

            // Prevent duplicate names across the tree (excluding the current container)
            bool nameExists = RootHasContainerName(_rootContainer, newName, except: container);
            if (nameExists) {
                Debug.WriteLine($"CommitRename: Name '{newName}' already exists. Rename cancelled.");
                CancelRename(nameText, nameEditor, container.Name);
                return;
            }

            var existing = FindContainerByName(_rootContainer, container.Name);
            string oldName = container.Name;
            if (existing != null) {
                existing.Name = newName;
            }
            container.Name = newName;

            // Rename item file if it exists
            _storageLoader.RenameItemFile(oldName, newName);

            nameText.Text = newName;
            nameEditor.Visibility = Visibility.Collapsed;
            nameText.Visibility = Visibility.Visible;
            if (_currentRenameContext?.NameEditor == nameEditor) _currentRenameContext = null;

            try {
                _storageLoader.SaveTemporary(_rootContainer);

                // After save, rebuild the current level view like navigation does
                ReloadAllBoxes();
            } catch {
                // Revert name on failure
                existing = FindContainerByName(_rootContainer, newName);
                if (existing != null) {
                    existing.Name = nameText.Text;
                }
                container.Name = nameText.Text;
                nameEditor.Visibility = Visibility.Collapsed;
                nameText.Visibility = Visibility.Visible;
            }
        }

        private void CancelRename(TextBlock nameText, TextBox nameEditor, string originalName) {
            nameEditor.Text = originalName;
            nameEditor.Visibility = Visibility.Collapsed;
            nameText.Visibility = Visibility.Visible;
            if (_currentRenameContext?.NameEditor == nameEditor) _currentRenameContext = null;
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            if (_currentRenameContext != null && 
                _currentRenameContext.NameEditor != null && 
                _currentRenameContext.NameEditor.Visibility == Visibility.Visible) {
                
                var editor = _currentRenameContext.NameEditor;
                var pos = e.GetPosition(editor);
                bool isInside = pos.X >= 0 && pos.X < editor.ActualWidth && 
                                pos.Y >= 0 && pos.Y < editor.ActualHeight;
                
                if (!isInside && _currentRenameContext.Container != null && _currentRenameContext.NameText != null) {
                    // Clicked outside, force commit directly
                    CommitRename(_currentRenameContext.Container, _currentRenameContext.NameText, editor);
                    // We do NOT clear focus here because CommitRename handles the UI state.
                    // Clearing focus might trigger LostFocus which would try to commit again (harmless but redundant).
                }
            }
        }

        private StorageContainer? FindContainerByName(StorageContainer root, string name) {
            if (root.Name == name) return root;
            foreach (var child in root.Children) {
                var result = FindContainerByName(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private bool RootHasContainerName(StorageContainer root, string name, StorageContainer? except) {
            if (!ReferenceEquals(root, except) && root.Name == name) return true;
            foreach (var child in root.Children) {
                if (RootHasContainerName(child, name, except)) return true;
            }
            return false;
        }

        private void TryAddResizeHandle(Border box, StorageContainer container) {
            _boxResizer?.AttachResizeHandle(box, container);
        }

        // Clear all boxes and handles from the storage grid.
        public void ClearStorageGrid() {
            _storageGrid.Children.Clear();
        }

        // Rebuild view from the current root state, similar to navigating levels.
        public void ReloadAllBoxes() {
            if (_rootContainer == null) return;

            _boxResizer?.ClearHandles(_storageGrid);
            ClearStorageGrid();

            // Recreate boxes for root level children
            foreach (var child in _rootContainer.Children) {
                AddStorageBox(child);
            }
        }

        // Edit box color by name, update UI, and persist change.
        public void EditBoxColor(string containerName, string newColorHex) {
            if (_rootContainer == null) return;

            var container = FindContainerByName(_rootContainer, containerName);
            if (container == null) {
                Debug.WriteLine($"EditBoxColor: Container '{containerName}' not found.");
                return;
            }
            EditBoxColor(container, newColorHex);
        }

        // OVERLOAD: Edit box color by container instance.
        public void EditBoxColor(StorageContainer container, string newColorHex) {
            container.Color = newColorHex;

            var border = _storageGrid.Children
                .OfType<Border>()
                .FirstOrDefault(b => b.DataContext is StorageContainer sc && ReferenceEquals(sc, container));

            if (border != null) {
                try {
                    border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(newColorHex));
                } catch {
                    Debug.WriteLine($"EditBoxColor: Invalid color '{newColorHex}'.");
                }
            }

            try {
                if (_rootContainer != null) {
                    _storageLoader?.SaveTemporary(_rootContainer);
                }
            } catch {
                Debug.WriteLine("EditBoxColor: Failed to save color change.");
            }
        }

        public void ShowEmptyStatePrompt(System.Action onAddBox, System.Action onConvertToList) {
            double size = 100;
            double left = 0; // Grid 0
            double top = 0;  // Grid 0

            var containerBorder = new Border {
                Width = size,
                Height = size,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Tag = "EmptyStatePrompt" // Tag to identify and remove later
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Top Half - Add Box
            var topBorder = new Border {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D3D3D3")), // Light Gray
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = Brushes.Black,
                Cursor = Cursors.Hand
            };
            
            var topStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            topStack.Children.Add(new TextBlock { Text = "+", FontWeight = FontWeights.Bold, FontSize = 20, Margin = new Thickness(0, 0, 5, 0) });
            topStack.Children.Add(new TextBlock { Text = GetStr("Str_Box"), FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            topBorder.Child = topStack;
            
            topBorder.MouseLeftButtonDown += (s, e) => { onAddBox?.Invoke(); e.Handled = true; };

            // Bottom Half - Convert to Item List
            var bottomBorder = new Border {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D3D3D3")), // Light Gray
                CornerRadius = new CornerRadius(0, 0, 10, 10),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };

            var bottomStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            bottomStack.Children.Add(new TextBlock { Text = "+", FontWeight = FontWeights.Bold, FontSize = 20, Margin = new Thickness(0, 0, 5, 0) });
            bottomStack.Children.Add(new TextBlock { Text = GetStr("Str_ItemList"), FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            bottomBorder.Child = bottomStack;

            bottomBorder.MouseLeftButtonDown += (s, e) => { onConvertToList?.Invoke(); e.Handled = true; };

            Grid.SetRow(topBorder, 0);
            Grid.SetRow(bottomBorder, 1);

            grid.Children.Add(topBorder);
            grid.Children.Add(bottomBorder);

            containerBorder.Child = grid;

            Canvas.SetLeft(containerBorder, left);
            Canvas.SetTop(containerBorder, top);

            _storageGrid.Children.Add(containerBorder);
        }

        public void HideEmptyStatePrompt() {
            var prompt = _storageGrid.Children.OfType<Border>().FirstOrDefault(b => b.Tag as string == "EmptyStatePrompt");
            if (prompt != null) {
                _storageGrid.Children.Remove(prompt);
            }
        }
    }
}