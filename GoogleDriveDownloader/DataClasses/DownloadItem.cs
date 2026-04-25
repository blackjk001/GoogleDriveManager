using GoogleDriveDownloader;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using GoogleDriveDownloader.UI;

namespace GoogleDriveDownloader.DataClasses
{
    public class DownloadItem : INotifyPropertyChanged
    {
        private string _fileName;
        private double _progress;
        private string _status;
        private CancellationTokenSource _cts;
        private long _fileSize;
        private string _progressText = "0%";

        public string FileName
        {
            get => _fileName;
            set { _fileName = value; OnPropertyChanged(); }
        }

        public double Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                OnPropertyChanged();
                ProgressText = $"{value:F0}%";
            }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public long FileSize
        {
            get => _fileSize;
            set { _fileSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileSizeText)); }
        }

        public string FileSizeText
        {
            get
            {
                if (_fileSize <= 0) return "...";
                if (_fileSize < 1024) return $"{_fileSize} B";
                if (_fileSize < 1024 * 1024) return $"{_fileSize / 1024.0:F1} KB";
                if (_fileSize < 1024 * 1024 * 1024) return $"{_fileSize / (1024.0 * 1024.0):F1} MB";
                return $"{_fileSize / (1024.0 * 1024.0 * 1024.0):F1} GB";
            }
        }

        public string ProgressText
        {
            get => _progressText;
            set { _progressText = value; OnPropertyChanged(); }
        }

        public ICommand CancelCommand { get; }

        public DownloadItem(CancellationTokenSource cts, long fileSize)
        {
            _cts = cts;
            FileSize = fileSize; // Сохраняем размер файла
            CancelCommand = new RelayCommand(_ => CancelDownload());
        }

        private void CancelDownload()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                Status = "Отмена...";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}