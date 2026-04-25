using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Download;
using GoogleDriveDownloader.DataClasses; // Подключаем модели

namespace GoogleDriveDownloader.Services
{
    public class DownloadService
    {
        public ObservableCollection<DownloadItem> ActiveDownloads { get; } = new ObservableCollection<DownloadItem>();

        // Запускает скачивание файла
        public async Task StartDownloadAsync(GoogleAccount account, GoogleDriveItem file, string targetFolder)
        {
            var cts = new CancellationTokenSource();
            DownloadItem item = null;
            string savePath = Path.Combine(targetFolder, file.Name);

            try
            {
                // Получаем размер файла
                var metaReq = account.Service.Files.Get(file.Id);
                metaReq.Fields = "size";
                var meta = await metaReq.ExecuteAsync(cts.Token);
                long fileSize = meta.Size.GetValueOrDefault(0);

                // Создаем элемент загрузки
                item = new DownloadItem(cts, fileSize)
                {
                    FileName = file.Name,
                    Status = "Ожидание..."
                };

                // Добавляем в список 
                System.Windows.Application.Current.Dispatcher.Invoke(() => ActiveDownloads.Add(item));

                // Скачиваем
                var request = account.Service.Files.Get(file.Id);
                using (var stream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
                {
                    item.Status = "Скачивание...";

                    request.MediaDownloader.ProgressChanged += (progress) =>
                    {
                        if (progress.Status == DownloadStatus.Downloading && fileSize > 0)
                        {
                            item.Progress = (double)progress.BytesDownloaded * 100 / fileSize;
                        }
                        else if (progress.Status == DownloadStatus.Failed)
                        {
                            item.Status = "Ошибка";
                        }
                    };

                    await request.DownloadAsync(stream, cts.Token);
                }

                item.Status = "Завершено";
                item.Progress = 100;

            }
            catch (OperationCanceledException)
            {
                if (item != null) item.Status = "Отменено";
                try { if (File.Exists(savePath)) File.Delete(savePath); } catch { }
            }
            catch (Exception ex)
            {
                if (item != null) item.Status = $"Ошибка: {ex.Message}";
            }
            finally
            {
                // Удаляем из списка через 3 секунды
                if (item != null)
                {
                    await Task.Delay(3000);
                    System.Windows.Application.Current.Dispatcher.Invoke(() => ActiveDownloads.Remove(item));
                }
            }
        }
    }
}