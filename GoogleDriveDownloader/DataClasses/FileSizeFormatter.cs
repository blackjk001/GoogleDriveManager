using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoogleDriveDownloader.DataClasses
{
    public static class FileSizeFormatter
    {
        public static string FormatSize(long size)
        {
            if (size <= 0) return "0 B";

            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double dblSByte = size;

            while (dblSByte >= 1024 && i < suffixes.Length - 1)
            {
                dblSByte /= 1024;
                i++;
            }

            if (i == 0 && size == 0) return "";

            return string.Format("{0:0.##} {1}", dblSByte, suffixes[i]);
        }

    }  
}
