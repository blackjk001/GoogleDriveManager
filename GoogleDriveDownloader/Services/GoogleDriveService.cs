using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using GoogleDriveDownloader.DataClasses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace GoogleDriveDownloader.Services
{
    public class GoogleDriveService
    {
        private const string TokenDirectory = "tokens";
        private const string AppName = "DriveManager";
        private const string CredentialsPath = "C:\\Users\\Lenovo\\Desktop\\ИТ\\Курсовая работа\\GoogleDriveDownloader\\GoogleDriveDownloader\\client_secrets.json";


        // Аутентификация пользователя и получение сервиса

        public async Task<GoogleAccount> AuthenticateUserAsync(string userId)
        {
            UserCredential credential;
            string credPath = Path.Combine(TokenDirectory, userId);

            using (var stream =  new FileStream(CredentialsPath, FileMode.Open, FileAccess.Read))
            {
                // GoogleWebAuthorizationBroker готовый класс от Google
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets, // передаем наше приложение
                    new[] { DriveService.Scope.Drive }, // сообщаем какие права нам нужны
                    userId,                             // Уникальный ID пользователя
                    CancellationToken.None,              
                    new FileDataStore(credPath, true)); // куда сохранить токен
            }

            if (credential == null) return null;
            
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = AppName,
            });

            // Получаем email для отображения
            var aboutRequest = service.About.Get();
            aboutRequest.Fields = "user(emailAddress)";
            var about = await aboutRequest.ExecuteAsync();

            return new GoogleAccount
            {
                UserId = userId,
                Email = about.User.EmailAddress,
                Credential = credential,
                Service = service
            };
        }

        // Получение списка файлов в папке

        public async Task<List<GoogleDriveItem>> ListFilesAsync(DriveService service, string folderId)
        {
            var list = new List<GoogleDriveItem>();
            var request = service.Files.List();
            request.Q = $"'{folderId}' in parents and trashed = false";
            request.Fields = "files(id, name, mimeType, size)";
            request.OrderBy = "folder, name";
            request.PageSize = 200;

            var result = await request.ExecuteAsync();
            foreach (var file in result.Files)
            {
                list.Add(new GoogleDriveItem
                {
                    Id = file.Id,
                    Name = file.Name,
                    IsFolder = file.MimeType == "application/vnd.google-apps.folder",
                    Size = file.Size.HasValue ? file.Size.Value : 0
                });
            }
            return list;
        }

        public async Task DeleteFileAsync(DriveService service, string fileId)
        {
            await service.Files.Delete(fileId).ExecuteAsync();
        }

        public async Task RenameFileAsync(DriveService service, string fileId, string newName)
        {
            var fileMetadata = new Google.Apis.Drive.v3.Data.File() { Name = newName };
            var request = service.Files.Update(fileMetadata, fileId);
            await request.ExecuteAsync();
        }

        public async Task CopyFileAsync(DriveService service, string fileId, string newParentId)
        {
            var copyMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Parents = new[] { newParentId }
            };
            var request = service.Files.Copy(copyMetadata, fileId);
            await request.ExecuteAsync();
        }

    }
}
