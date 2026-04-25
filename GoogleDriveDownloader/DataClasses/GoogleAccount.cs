using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;

namespace GoogleDriveDownloader.DataClasses
{
    public class GoogleAccount
    {
        public string UserId { get; set; } 
        public string Email { get; set; }  
        public UserCredential Credential { get; set; }
        public DriveService Service { get; set; }

        public override string ToString()
        {
            return Email ?? UserId;
        }
    }
}