﻿using Azure.Storage.Blobs;
using DokkanDaily.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace DokkanDaily.Services.Interfaces
{
    public interface IAzureBlobService
    {
        Task<List<BlobClient>> GetFilesForTag(string tag, string bucket = null);

        Task<string> UploadToAzureAsync(string fileName, string contentType, IBrowserFile browserFile, Challenge challengeModel, string bucket = null, string userAgent = null, string discordUsername = null);

        Task<int> GetFileCountForTag(string tagName, string bucket = null);

        Task PruneContainers(int daysToKeep);

        string GetBlobSASTOkenByFile(string fileName);

        string GetBucketNameForDate(string formattedDateTag);

    }
}
