using System.Collections.Generic;
using System.IO;
using GoogleDriveDownloader.DataClasses;

namespace GoogleDriveDownloader.Services
{
    public class LocalFileService
    {
        // Получает список дисков
        public List<WindowsItem> GetDrives()
        {
            var items = new List<WindowsItem>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    items.Add(new WindowsItem
                    {
                        Name = drive.Name,
                        FullPath = drive.Name,
                        IsFolder = true
                    });
                }
            }
            return items;
        }

        // Получает содержимое папки (файлы и подпапки), скрывая системные
        public List<WindowsItem> GetDirectoryContents(string path)
        {
            var items = new List<WindowsItem>();
            var dirInfo = new DirectoryInfo(path);

            // Папки
            foreach (var dir in dirInfo.GetDirectories())
            {
                bool isHidden = dir.Attributes.HasFlag(FileAttributes.Hidden);
                bool isSystem = dir.Attributes.HasFlag(FileAttributes.System);

                if (!isHidden && !isSystem)
                {
                    items.Add(new WindowsItem
                    {
                        Name = dir.Name,
                        FullPath = dir.FullName,
                        IsFolder = true
                        
                    });
                }
            }

            //Файлы
            foreach (var file in dirInfo.GetFiles())
            {
                bool isHidden = file.Attributes.HasFlag(FileAttributes.Hidden);
                bool isSystem = file.Attributes.HasFlag(FileAttributes.System);

                if (!isHidden && !isSystem)
                {
                    items.Add(new WindowsItem
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsFolder = false,
                        Size = file.Length
                    });
                }
            }

            return items;
        }

        // Удаляет файл или папку
        public void DeleteItem(WindowsItem item)
        {
            if (item.IsFolder)
                Directory.Delete(item.FullPath, true);
            else
                File.Delete(item.FullPath);
        }

        // Переименовывает файл или папку
        public void RenameItem(WindowsItem item, string newName)
        {
            string dir = Path.GetDirectoryName(item.FullPath);
            string newPath = Path.Combine(dir, newName);

            if (item.IsFolder)
                Directory.Move(item.FullPath, newPath);
            else
                File.Move(item.FullPath, newPath);
        }
    }
}
