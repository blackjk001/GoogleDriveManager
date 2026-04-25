using GoogleDriveDownloader.DataClasses;
using GoogleDriveDownloader.Services;
using GoogleDriveDownloader.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents; 
using System.Windows.Input;
using System.Windows.Media;     


namespace GoogleDriveDownloader
{
    public partial class MainWindow : Window
    {
        // СЕРВИСЫ
        private readonly GoogleDriveService _googleService = new GoogleDriveService();
        private readonly LocalFileService _localService = new LocalFileService();
        private readonly DownloadService _downloadService = new DownloadService();

        // ДАННЫЕ
        private const string TokenDirectory = "tokens";

        // Список аккаунтов 
        private ObservableCollection<GoogleAccount> _googleAccounts = new ObservableCollection<GoogleAccount>();
        private GoogleAccount _currentGoogleAccount;

        public ObservableCollection<DownloadItem> ActiveDownloads => _downloadService.ActiveDownloads;

        // История навигации Google Drive
        private Stack<Tuple<string, string>> _googleDriveHistory = new Stack<Tuple<string, string>>();

        // ПЕРЕМЕННЫЕ ИНТЕРФЕЙСА
        private Point _dragStartPoint;
        private Point _selectionStartPoint;
        private bool _isDragging = false;
        private bool _isSelecting = false;
        private bool _clickedSelected = false;
        private RubberBandAdorner _rubberBandAdorner;
        private AdornerLayer _adornerLayer;

        // Буфер обмена
        private List<GoogleDriveItem> _driveClipboard = new List<GoogleDriveItem>();
        private bool _isDriveCut = false;
        private bool _isWindowsCut = false; 

        // Переименование
        private object _itemToRename;
        private string _renameSourceList;

        public MainWindow()
        {
            InitializeComponent();

            lstDownloads.ItemsSource = _downloadService.ActiveDownloads;
            _downloadService.ActiveDownloads.CollectionChanged += ActiveDownloads_CollectionChanged;

            LoadWindowsDrives();
            LoadPersistedAccounts();
        }

        // Сортровка
        // Храним направление сортировки для каждого столбца
        private bool _isSortNameAsc = true;
        private bool _isSortSizeAsc = true;

        // Клик по заголовку "Имя"
        private void Header_SortByName_Click(object sender, MouseButtonEventArgs e)
        {   
            var headerText = sender as FrameworkElement;
            var parentGrid = FindVisualParent<Grid>(headerText);
        }

        private void Windows_SortName_Click(object sender, MouseButtonEventArgs e)
        {
            SortListBox(lstWindowsFiles, "Name", ref _isSortNameAsc);
        }
        private void Windows_SortSize_Click(object sender, MouseButtonEventArgs e)
        {
            SortListBox(lstWindowsFiles, "Size", ref _isSortSizeAsc);
        }

        private void Google_SortName_Click(object sender, MouseButtonEventArgs e)
        {
            SortListBox(lstGoogleDriveFiles, "Name", ref _isSortNameAsc);
        }
        private void Google_SortSize_Click(object sender, MouseButtonEventArgs e)
        {
            SortListBox(lstGoogleDriveFiles, "Size", ref _isSortSizeAsc);
        }

        private void SortListBox(ListBox listBox, string propertyName, ref bool isAscending)
        {
            // Получаем данные для сортировки
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(listBox.Items);
            if (view == null) return;

            view.SortDescriptions.Clear();

            // Переключаем направление
            var direction = isAscending ? System.ComponentModel.ListSortDirection.Ascending : System.ComponentModel.ListSortDirection.Descending;
            isAscending = !isAscending; // Инвертируем для следующего клика

            // для папок всегда чтобы они были сверху
            // сначала сортируем по IsFolder (true раньше false) потом по свойству
            view.SortDescriptions.Add(new System.ComponentModel.SortDescription("IsFolder", System.ComponentModel.ListSortDirection.Descending));
            view.SortDescriptions.Add(new System.ComponentModel.SortDescription(propertyName, direction));
        }

        #region =================== WINDOWS ЛОГИКА (Через Service) ===================

        private void LoadWindowsDrives()
        {
            lstWindowsFiles.Items.Clear();
            txtWindowsCurrentPath.Text = "This PC";
            btnWindowsUp.IsEnabled = false;

            // Используем сервис!
            var drives = _localService.GetDrives();
            foreach (var drive in drives) lstWindowsFiles.Items.Add(drive);
        }

        private void LoadWindowsDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    MessageBox.Show("Папка не найдена.");
                    return;
                }

                lstWindowsFiles.Items.Clear();
                txtWindowsCurrentPath.Text = path;
                btnWindowsUp.IsEnabled = true;

                // Используем сервис!
                var items = _localService.GetDirectoryContents(path);
                foreach (var item in items) lstWindowsFiles.Items.Add(item);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка доступа: {ex.Message}");
                LoadWindowsDrives(); // Откат
            }
        }

        private void BtnWindowsUp_Click(object sender, RoutedEventArgs e)
        {
            string currentPath = txtWindowsCurrentPath.Text;
            if (currentPath == "This PC") return;

            DirectoryInfo parentInfo = Directory.GetParent(currentPath);
            if (parentInfo != null) LoadWindowsDirectory(parentInfo.FullName);
            else LoadWindowsDrives();
        }

        #endregion

        #region =================== GOOGLE DRIVE ЛОГИКА (Через Service) ===================

        private async void LoadPersistedAccounts()
        {
            if (!Directory.Exists(TokenDirectory)) Directory.CreateDirectory(TokenDirectory);

            var userDirectories = Directory.GetDirectories(TokenDirectory);
            foreach (var userDir in userDirectories)
            {
                string userId = new DirectoryInfo(userDir).Name;

                // Используем сервис для входа
                var account = await _googleService.AuthenticateUserAsync(userId);

                if (account != null)
                {
                    if (!_googleAccounts.Any(a => a.Email == account.Email))
                    {
                        _googleAccounts.Add(account);
                    }
                    else
                    {
                        // Удаляем дубликат токена
                        try { Directory.Delete(userDir, true); } catch { }
                    }
                }
            }
            // Привязываем UI к коллекции
            lstAccounts.ItemsSource = _googleAccounts;
            if (_googleAccounts.Count > 0) lstAccounts.SelectedIndex = 0;
        }

        private async void BtnAddAccount_Click(object sender, RoutedEventArgs e)
        {
            string userId = "user-" + Guid.NewGuid().ToString("N");

            var newAccount = await _googleService.AuthenticateUserAsync(userId);

            if (newAccount != null)
            {
                if (!_googleAccounts.Any(a => a.Email == newAccount.Email))
                {
                    _googleAccounts.Add(newAccount);
                    lstAccounts.SelectedItem = newAccount;
                }
                else
                {
                    MessageBox.Show("Этот аккаунт уже добавлен.");
                    lstAccounts.SelectedItem = _googleAccounts.First(a => a.Email == newAccount.Email);
                    try { Directory.Delete(Path.Combine(TokenDirectory, userId), true); } catch { }
                }
            }
        }
        private void LstAccounts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstAccounts.SelectedItem is GoogleAccount selectedAccount)
            {
                _currentGoogleAccount = selectedAccount;
                _googleDriveHistory.Clear();
                LoadGoogleDriveFolder("root", "My Drive");
            }
            else
            {
                _currentGoogleAccount = null;
                lstGoogleDriveFiles.Items.Clear();
                txtGoogleDriveCurrentPath.Text = "Нет аккаунтов";
            }
        }
        private void BtnRemoveAccount_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var accountToRemove = button.DataContext as GoogleAccount;

            if (accountToRemove != null)
            {
                var result = MessageBox.Show($"Выйти из {accountToRemove.Email}?\nЛокальные файлы останутся, но токен доступа будет удален.",
                                             "Выход", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        string tokenPath = Path.Combine(TokenDirectory, accountToRemove.UserId);
                        if (Directory.Exists(tokenPath)) Directory.Delete(tokenPath, true);
                    }
                    catch { }

                    _googleAccounts.Remove(accountToRemove);
                    if (_googleAccounts.Count > 0) lstAccounts.SelectedIndex = 0;
                }
            }
            e.Handled = true;
        }
        private async void LoadGoogleDriveFolder(string folderId, string folderName)
        {
            if (_currentGoogleAccount == null) return;

            lstGoogleDriveFiles.Items.Clear();
            txtGoogleDriveCurrentPath.Text = folderName;
            txtGoogleDriveCurrentPath.Tag = folderId;
            btnGoogleDriveUp.IsEnabled = _googleDriveHistory.Count > 0;

            try
            {
                var files = await _googleService.ListFilesAsync(_currentGoogleAccount.Service, folderId);
                foreach (var file in files) lstGoogleDriveFiles.Items.Add(file);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка Google Drive: {ex.Message}");
            }
        }
        private void BtnGoogleDriveUp_Click(object sender, RoutedEventArgs e)
        {
            if (_googleDriveHistory.Count > 0)
            {
                var parent = _googleDriveHistory.Pop();
                LoadGoogleDriveFolder(parent.Item1, parent.Item2);
            }
        }

        #endregion

        #region =================== УНИВЕРСАЛЬНОЕ ВЫДЕЛЕНИЕ (MOUSE LOGIC) ===================

        private static T FindVisualParent<T>(DependencyObject obj) where T : DependencyObject
        {
            while (obj != null)
            {
                if (obj is T) return (T)obj;
                obj = VisualTreeHelper.GetParent(obj);
            }
            return null;
        }

        private void Universal_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            if (e.ClickCount == 2)
            {
                Universal_MouseDoubleClick(sender, e);
                e.Handled = true;
                return;
            }

            var source = e.OriginalSource as DependencyObject;
            var clickedItem = FindVisualParent<ListBoxItem>(source);

            _selectionStartPoint = e.GetPosition(listBox);
            _dragStartPoint = e.GetPosition(null);
            _clickedSelected = false;

            if (clickedItem != null)
            {
                if (clickedItem.IsSelected)
                {
                    _clickedSelected = true;
                    e.Handled = true;
                }
            }
            else
            {
                if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    listBox.SelectedItems.Clear();
                }
            }
        }
        private void Universal_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            if (_isSelecting && _rubberBandAdorner != null)
            {
                _rubberBandAdorner.UpdateSelection(e.GetPosition(listBox));

                // Компактная логика Live-выделения
                Rect rect = _rubberBandAdorner.GetSelectionRect();
                bool isCtrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

                foreach (var item in listBox.Items)
                {
                    if (listBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem container)
                    {
                        var bounds = container.TransformToVisual(listBox)
                                      .TransformBounds(new Rect(0, 0, container.ActualWidth, container.ActualHeight));
                        if (rect.IntersectsWith(bounds)) container.IsSelected = true;
                        else if (!isCtrl) container.IsSelected = false;
                    }
                }

                e.Handled = true;
                return;
            }

            if (e.LeftButton == MouseButtonState.Released)
            {
                _clickedSelected = false;
                return;
            }

            Point currentPos = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (_clickedSelected && listBox.SelectedItems.Count > 0)
                {
                    DragDrop.DoDragDrop(listBox, listBox.SelectedItems.Cast<object>().ToList(), DragDropEffects.Copy);
                    _clickedSelected = false;
                    e.Handled = true;
                    return;
                }

                _isSelecting = true;
                if (!Keyboard.IsKeyDown(Key.LeftCtrl)) listBox.SelectedItems.Clear();

                _adornerLayer = AdornerLayer.GetAdornerLayer(listBox);
                if (_adornerLayer != null)
                {
                    _rubberBandAdorner = new RubberBandAdorner(listBox, _selectionStartPoint);
                    _adornerLayer.Add(_rubberBandAdorner);
                    listBox.CaptureMouse();
                }
                e.Handled = true;
            }
        }
        private void Universal_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            if (_isSelecting && _rubberBandAdorner != null)
            {
                _adornerLayer.Remove(_rubberBandAdorner);
                _rubberBandAdorner = null;
            }

            _isDragging = false;
            _isSelecting = false;
            _clickedSelected = false;
            if (listBox.IsMouseCaptured) listBox.ReleaseMouseCapture();
        }
        private void Universal_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null || listBox.SelectedItem == null) return;

            if (listBox == lstWindowsFiles)
            {
                var item = listBox.SelectedItem as WindowsItem;
                if (item != null && item.IsFolder) LoadWindowsDirectory(item.FullPath);
            }
            else if (listBox == lstGoogleDriveFiles)
            {
                var item = listBox.SelectedItem as GoogleDriveItem;
                if (item != null && item.IsFolder)
                {
                    _googleDriveHistory.Push(new Tuple<string, string>(txtGoogleDriveCurrentPath.Tag.ToString(), txtGoogleDriveCurrentPath.Text));
                    LoadGoogleDriveFolder(item.Id, item.Name);
                }
            }
        }

        #endregion

        #region =================== DRAG-N-DROP & ЗАГРУЗКА (Через Service) ===================

        private void LstWindowsFiles_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(List<object>))) e.Effects = DragDropEffects.Copy;
            else e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void LstWindowsFiles_Drop(object sender, DragEventArgs e)
        {
            // 1. Куда бросили
            string targetPath = txtWindowsCurrentPath.Text;
            var element = e.OriginalSource as FrameworkElement;
            var dropItem = element?.DataContext as WindowsItem;
            if (dropItem != null && dropItem.IsFolder) targetPath = dropItem.FullPath;

            if (targetPath == "This PC")
            {
                MessageBox.Show("Выберите диск или папку для скачивания.");
                return;
            }

            // 2. Что бросили
            var draggedData = e.Data.GetData(typeof(List<object>)) as List<object>;
            if (draggedData == null) return;

            // Выбираем файлы Google Drive
            var filesToDownload = draggedData.OfType<GoogleDriveItem>().Where(i => !i.IsFolder).ToList();

            if (filesToDownload.Count > 0)
            {
                ShowDownloadManager();
                foreach (var file in filesToDownload)
                {
                    StartDownload(file, targetPath);
                }
            }
        }

        private async void StartDownload(GoogleDriveItem file, string targetPath)
        {
            ShowDownloadManager();

            await _downloadService.StartDownloadAsync(_currentGoogleAccount, file, targetPath);

            if (txtWindowsCurrentPath.Text.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
            {
                LoadWindowsDirectory(targetPath);
            }
        }

        private void ShowDownloadManager()
        {
            // Раскрываем список, если был свернут
            lstDownloads.Visibility = Visibility.Visible;
            btnTogglePopup.Content = "▼";

            // Открываем Popup
            if (!DownloadPopup.IsOpen) DownloadPopup.IsOpen = true;
            PositionDownloadPopup();
        }

        #endregion

        #region =================== POPUP LOGIC ===================

        private void PositionDownloadPopup()
        {
            if (DownloadPopup.IsOpen)
            {
                DownloadPopup.HorizontalOffset += 1;
                DownloadPopup.HorizontalOffset -= 1;
            }
        }

        private void Window_LocationChanged(object sender, EventArgs e) => PositionDownloadPopup();
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => PositionDownloadPopup();

        private void BtnTogglePopup_Click(object sender, RoutedEventArgs e)
        {
            if (lstDownloads.Visibility == Visibility.Visible)
            {
                lstDownloads.Visibility = Visibility.Collapsed;
                btnTogglePopup.Content = "▲";
            }
            else
            {
                lstDownloads.Visibility = Visibility.Visible;
                btnTogglePopup.Content = "▼";
                PositionDownloadPopup();
            }
        }

        // Закрывает Popup, когда список пуст.
        private void ActiveDownloads_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (_downloadService.ActiveDownloads.Count == 0 && DownloadPopup.IsOpen)
            {
                DownloadPopup.IsOpen = false;

            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Application.Current.Shutdown();
        }

        #endregion

        #region =================== КОНТЕКСТНОЕ МЕНЮ И ОПЕРАЦИИ (Через Services) ===================

        private ListBox GetActiveListBox(object sender)
        {
            if (sender is MenuItem menuItem)
            {
                var menu = menuItem.Parent as ContextMenu;
                if (menu != null)
                {
                    if (menu.PlacementTarget is ListBox lb) return lb;
                    if (menu.PlacementTarget is ListBoxItem lbi) return FindVisualParent<ListBox>(lbi);
                }
            }
            return null;
        }

        private void Universal_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var menu = this.Resources["FileSystemContextMenu"] as ContextMenu;
            if (menu == null) return; 

            MenuItem itemCopy = null, itemCut = null, itemPaste = null, itemRename = null, itemDelete = null, itemOpen = null;

            foreach (var item in menu.Items)
            {
                if (item is MenuItem mi)
                {
                    if (mi.Name == "miCopy") itemCopy = mi;
                    else if (mi.Name == "miCut") itemCut = mi;
                    else if (mi.Name == "miPaste") itemPaste = mi;
                    else if (mi.Name == "miRename") itemRename = mi;
                    else if (mi.Name == "miDelete") itemDelete = mi;
                    else if (mi.Name == "miOpen") itemOpen = mi;
                }
            }

            int count = listBox.SelectedItems.Count;

            if (itemOpen != null) itemOpen.IsEnabled = count == 1;
            if (itemCopy != null) itemCopy.IsEnabled = count > 0;
            if (itemCut != null) itemCut.IsEnabled = count > 0;
            if (itemDelete != null) itemDelete.IsEnabled = count > 0;
            if (itemRename != null) itemRename.IsEnabled = count == 1;

            if (itemPaste != null)
            {
                bool canPaste = false;
                if (listBox == lstWindowsFiles)
                {
                    canPaste = Clipboard.ContainsFileDropList() || _driveClipboard.Count > 0;
                }
                else
                {
                    canPaste = _driveClipboard.Count > 0;
                }
                itemPaste.IsEnabled = canPaste;
            }
        }
        private void Menu_Open_Click(object sender, RoutedEventArgs e)
        {
            Universal_MouseDoubleClick(GetActiveListBox(sender), null);
        }

        private async void Menu_Delete_Click(object sender, RoutedEventArgs e)
        {
            var listBox = GetActiveListBox(sender);
            if (listBox == null || listBox.SelectedItems.Count == 0) return;

            if (MessageBox.Show("Удалить выбранное?", "Подтверждение", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            if (listBox == lstWindowsFiles)
            {
                foreach (WindowsItem item in listBox.SelectedItems.Cast<WindowsItem>().ToList())
                {
                    try { _localService.DeleteItem(item); }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
                LoadWindowsDirectory(txtWindowsCurrentPath.Text);
            }
            else
            {
                foreach (GoogleDriveItem item in listBox.SelectedItems.Cast<GoogleDriveItem>().ToList())
                {
                    try { await _googleService.DeleteFileAsync(_currentGoogleAccount.Service, item.Id); }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
                LoadGoogleDriveFolder(txtGoogleDriveCurrentPath.Tag.ToString(), txtGoogleDriveCurrentPath.Text);
            }
        }
        private void Menu_Copy_Click(object sender, RoutedEventArgs e) => DoCopyOrCut(sender, false);
        private void Menu_Cut_Click(object sender, RoutedEventArgs e) => DoCopyOrCut(sender, true);

        private void DoCopyOrCut(object sender, bool isCut)
        {
            var listBox = GetActiveListBox(sender);
            if (listBox == null) return;

            if (listBox == lstWindowsFiles)
            {
                var paths = new System.Collections.Specialized.StringCollection();
                foreach (WindowsItem item in listBox.SelectedItems) paths.Add(item.FullPath);
                Clipboard.SetFileDropList(paths);
                _isWindowsCut = isCut;
            }
            else
            {
                _driveClipboard.Clear();
                foreach (GoogleDriveItem item in listBox.SelectedItems) _driveClipboard.Add(item);
                _isDriveCut = isCut;
            }
        }
        private async void Menu_Paste_Click(object sender, RoutedEventArgs e)
        {
            var listBox = GetActiveListBox(sender);
            if (listBox == null) return;

            if (listBox == lstWindowsFiles)
            {
                string targetDir = txtWindowsCurrentPath.Text;
                if (targetDir == "This PC") return;

                // 1. Из Google Drive (Скачивание)
                if (_driveClipboard.Count > 0)
                {
                    ShowDownloadManager();
                    foreach (var item in _driveClipboard) StartDownload(item, targetDir);
                    if (_isDriveCut) _driveClipboard.Clear(); 
                    return;
                }

                // 2. Из Windows (Копирование/Перемещение)
                if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    foreach (string src in files)
                    {
                        string dest = Path.Combine(targetDir, Path.GetFileName(src));
                        try
                        {
                            if (Directory.Exists(src))
                            {
                                if (_isWindowsCut) Directory.Move(src, dest);
                                else MessageBox.Show("Копирование папок пока не реализовано.");
                            }
                            else if (File.Exists(src))
                            {
                                if (_isWindowsCut) File.Move(src, dest);
                                else File.Copy(src, dest, true);
                            }
                        }
                        catch (Exception ex) { MessageBox.Show(ex.Message); }
                    }
                    if (_isWindowsCut) { Clipboard.Clear(); _isWindowsCut = false; }
                    LoadWindowsDirectory(targetDir);
                }
            }
            else // Google Drive
            {
                string targetId = txtGoogleDriveCurrentPath.Tag.ToString();
                if (_driveClipboard.Count > 0)
                {
                    foreach (var item in _driveClipboard)
                    {
                        try
                        {
                            if (_isDriveCut)
                            {
                                // Cut: Copy + Delete 
                                await _googleService.CopyFileAsync(_currentGoogleAccount.Service, item.Id, targetId);
                                await _googleService.DeleteFileAsync(_currentGoogleAccount.Service, item.Id);
                            }
                            else
                            {
                                // Copy
                                await _googleService.CopyFileAsync(_currentGoogleAccount.Service, item.Id, targetId);
                            }
                        }
                        catch (Exception ex) { MessageBox.Show($"Ошибка API: {ex.Message}"); }
                    }
                    LoadGoogleDriveFolder(targetId, txtGoogleDriveCurrentPath.Text);
                }
            }
        }
        private void Menu_Rename_Click(object sender, RoutedEventArgs e)
        {
            var listBox = GetActiveListBox(sender);
            if (listBox == null || listBox.SelectedItems.Count != 1) return;

            if (listBox == lstWindowsFiles)
            {
                _renameSourceList = "Windows";
                _itemToRename = listBox.SelectedItem as WindowsItem;
                txtRenameInput.Text = ((WindowsItem)_itemToRename).Name;
            }
            else
            {
                _renameSourceList = "Google";
                _itemToRename = listBox.SelectedItem as GoogleDriveItem;
                txtRenameInput.Text = ((GoogleDriveItem)_itemToRename).Name;
            }
            RenamePopup.IsOpen = true;
            txtRenameInput.Focus();
            txtRenameInput.SelectAll();
        }
        private async void BtnRenameOk_Click(object sender, RoutedEventArgs e)
        {
            string newName = txtRenameInput.Text.Trim();
            if (string.IsNullOrEmpty(newName)) return;
            RenamePopup.IsOpen = false;

            try
            {
                if (_renameSourceList == "Windows")
                {
                    _localService.RenameItem((WindowsItem)_itemToRename, newName);
                    LoadWindowsDirectory(txtWindowsCurrentPath.Text);
                }
                else
                {
                    var item = (GoogleDriveItem)_itemToRename;
                    await _googleService.RenameFileAsync(_currentGoogleAccount.Service, item.Id, newName);
                    LoadGoogleDriveFolder(txtGoogleDriveCurrentPath.Tag.ToString(), txtGoogleDriveCurrentPath.Text);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
        private void BtnRenameCancel_Click(object sender, RoutedEventArgs e) => RenamePopup.IsOpen = false;

        #endregion
    }
}