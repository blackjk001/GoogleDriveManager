using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoogleDriveDownloader.DataClasses
{
    public class WindowsItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
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
