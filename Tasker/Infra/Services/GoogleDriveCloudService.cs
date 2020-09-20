﻿using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Takser.Infra.Options;
using Tasker.App.Services;
using Tasker.Infra.Consts;
using GoogleData = Google.Apis.Drive.v3.Data;

namespace Tasker.Infra.Services
{
    public class GoogleDriveCloudService : ICloudService
    {
        private const string FolderMimeType = "application/vnd.google-apps.folder";
        private const string ZipExtension = ".zip";
        private const string TaskerDriveDirectory = AppConsts.AppName;

        private readonly ILogger<GoogleDriveCloudService> mLogger;
        private readonly string mDatabasePath;
        private readonly string mTasksNotesPath;
        private readonly string[] Scopes = { DriveService.Scope.Drive };

        public GoogleDriveCloudService(IOptions<DatabaseConfigurtaion> configuration,
            ILogger<GoogleDriveCloudService> logger)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            mDatabasePath = configuration.Value.DatabaseDirectoryPath;
            mTasksNotesPath = configuration.Value.NotesTasksDirectoryPath;

            mLogger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> Upload()
        {
            string tempArchiveFile = null;

            try
            {
                tempArchiveFile = PackDataIntoZip();

                UserCredential credential = await CreateUserCredential().ConfigureAwait(false);
                DriveService service = GetDriveService(credential);

                string taskerDriveDirectoryId =
                    await GetOrCreateTaskerDriveDirectory(service).ConfigureAwait(false);

                return await UploadToGoogleDrive(service, tempArchiveFile, taskerDriveDirectoryId)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (tempArchiveFile != null)
                    File.Delete(tempArchiveFile);
            }
        }

        private string PackDataIntoZip()
        {
            string outputDirectoryName = DateTime.Now.ToString("M/d/yyyy").Replace('/', '-');
            Directory.CreateDirectory(outputDirectoryName);

            string archiveName = outputDirectoryName + ZipExtension;

            try
            {
                CopyContentIntoDirectory(outputDirectoryName);
                ZipFile.CreateFromDirectory(outputDirectoryName, archiveName);
            }
            finally
            {
                Directory.Delete(outputDirectoryName, recursive: true);
            }

            return archiveName;
        }

        private void CopyContentIntoDirectory(string outputDirectory)
        {
            string sourceDatabaseFile = Path.Combine(mDatabasePath, AppConsts.DatabaseName);
            string destDatabaseFile = Path.Combine(outputDirectory, AppConsts.DatabaseName);
            File.Copy(sourceDatabaseFile, destDatabaseFile);

            string sourceIdDatabaseFile = Path.Combine(mDatabasePath, AppConsts.NextIdHolderName);
            string destIdDatabaseFile = Path.Combine(outputDirectory, AppConsts.NextIdHolderName);
            File.Copy(sourceIdDatabaseFile, destIdDatabaseFile);

            string tasksNotesDirectoryName = Path.GetFileName(mTasksNotesPath);
            string destTaskNotesDirectoryName = Path.Combine(outputDirectory, tasksNotesDirectoryName);
            Directory.CreateDirectory(destTaskNotesDirectoryName);

            foreach (string note in Directory.EnumerateFiles(mTasksNotesPath))
            {
                string destNote = Path.Combine(destTaskNotesDirectoryName, Path.GetFileName(note));
                File.Copy(note, destNote);
            }
        }

        private async Task<UserCredential> CreateUserCredential()
        {
            UserCredential credential;

            await using Stream stream = new FileStream(
                "credentials.json", FileMode.Open, FileAccess.Read, FileShare.None, 4096, useAsync: true);

            // The file token.json stores the user's access and refresh tokens, and is created
            // automatically when the authorization flow completes for the first time.
            const string credPath = "token.json";

            credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.Load(stream).Secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(credPath, true)).Result;

            mLogger.LogDebug($"Credential file saved to: {credPath}");

            return credential;
        }

        private DriveService GetDriveService(UserCredential credential)
        {
            DriveService driveService = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = AppConsts.AppName,
            });

            mLogger.LogDebug($"Created drive service for {AppConsts.AppName}");

            return driveService;
        }

        private async Task<string> GetOrCreateTaskerDriveDirectory(DriveService service)
        {
            string taskerDirectoryId = await FindFile(service,
                file => file.MimeType == FolderMimeType && file.Name == TaskerDriveDirectory)
                .ConfigureAwait(false);

            if (!string.IsNullOrEmpty(taskerDirectoryId))
            {
                mLogger.LogTrace($"Found drive directory {TaskerDriveDirectory}");
                return taskerDirectoryId;
            }

            return await CreateTaskerDriveDirectory(service).ConfigureAwait(false);
        }

        private async Task<string> CreateTaskerDriveDirectory(DriveService service)
        {
            mLogger.LogInformation($"Drive directory {TaskerDriveDirectory} not found, creating..");

            GoogleData.File directory = new GoogleData.File()
            {
                Name = TaskerDriveDirectory,
                MimeType = FolderMimeType,
            };

            FilesResource.CreateRequest createRequest = service.Files.Create(directory);
            createRequest.Fields = "id";

            GoogleData.File resultDirectory = await createRequest.ExecuteAsync().ConfigureAwait(false);
            mLogger.LogInformation($"Done creating drive directory {TaskerDriveDirectory}");
            return resultDirectory.Id;
        }

        private async Task<bool> UploadToGoogleDrive(DriveService service, string fileToUpload, string parentId)
        {
            await RemoveExistingFileIfExists(service, fileToUpload, parentId).ConfigureAwait(false);

            await using Stream fileToUploadStream = new FileStream(
                fileToUpload, FileMode.Open, FileAccess.Read, FileShare.None, 4096, useAsync: true);

            GoogleData.File fileMetadata = new GoogleData.File()
            {
                Name = fileToUpload,
                Parents = new string[] { parentId }
            };

            FilesResource.CreateMediaUpload createRequest =
                service.Files.Create(fileMetadata, fileToUploadStream, null);
            createRequest.Fields = "id";

            IUploadProgress uploadProgress = await createRequest.UploadAsync().ConfigureAwait(false);

            if (uploadProgress.Status != UploadStatus.Completed)
            {
                mLogger.LogWarning($"Uploaded {fileToUpload} to {TaskerDriveDirectory}. " +
                    $"Bytes: {uploadProgress.BytesSent}. Status: {uploadProgress.Status}");
                return false;
            }

            mLogger.LogDebug($"Uploaded {fileToUpload} to {TaskerDriveDirectory}. " +
                    $"Bytes: {uploadProgress.BytesSent}. Status: {uploadProgress.Status}");

            return true;
        }

        private async Task RemoveExistingFileIfExists(DriveService service, string fileToUpload, string parentId)
        {
            string existingFileId = await FindFile(service,
                file => file.MimeType.Contains(ZipExtension) && file.Name == fileToUpload)
                .ConfigureAwait(false);

            if (string.IsNullOrEmpty(existingFileId))
            {
                mLogger.LogDebug($"File {fileToUpload} not found in {TaskerDriveDirectory} directory");
                return;
            }

            await RemoveExistingFileIfExists(service, existingFileId).ConfigureAwait(false);
        }

        private async Task RemoveExistingFileIfExists(DriveService service, string fileIdToDelete)
        {
            mLogger.LogDebug($"Going to remove file id {fileIdToDelete} from {TaskerDriveDirectory} directory");

            FilesResource.DeleteRequest deleteRequest = service.Files.Delete(fileIdToDelete);
            deleteRequest.Fields = "id";

            string deleteResult = await deleteRequest.ExecuteAsync().ConfigureAwait(false);
            mLogger.LogDebug($"Done removing file id {fileIdToDelete}. Delete result: {deleteResult}");
        }

        private async Task<string> FindFile(DriveService service, Func<GoogleData.File, bool> predicate)
        {
            FilesResource.ListRequest listRequest = service.Files.List();
            GoogleData.FileList fileList = await listRequest.ExecuteAsync().ConfigureAwait(false);
            foreach (GoogleData.File file in fileList.Files)
            {
                if (predicate(file))
                {
                    return file.Id;
                }
            }

            return string.Empty;
        }
    }
}