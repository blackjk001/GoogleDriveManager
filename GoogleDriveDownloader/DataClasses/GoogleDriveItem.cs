using System;
using System.Reflection;

namespace GoogleDriveDownloader.DataClasses
{ 
    // Вспомогательный класс для хранения информации о файле/папке Google Drive
    public class GoogleDriveItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsFolder { get; set; }

        public long Size { get; set; }

        public string FormattedSize
        {
            get
            {
                if (IsFolder) return ""; 
                return FileSizeFormatter.FormatSize(this.Size);
            }
        }

        
    }
}