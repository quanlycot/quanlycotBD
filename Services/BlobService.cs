using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace QuanLyCotWeb.Services
{
    public class BlobService
    {
        private readonly string _connectionString;
        private readonly string _containerName;

        public BlobService(IConfiguration configuration)
        {
            // Khớp với biến môi trường đã khai báo trên Render
            _connectionString = configuration["AzureBlob:ConnectionString"];
            _containerName = configuration["AzureBlob:ContainerName"];
        }

        public async Task<string> UploadAsync(Stream fileStream, string fileName)
        {
            var blobClient = new BlobContainerClient(_connectionString, _containerName);
            await blobClient.CreateIfNotExistsAsync();

            // Đặt quyền công khai để ảnh có thể truy cập từ link
            await blobClient.SetAccessPolicyAsync(Azure.Storage.Blobs.Models.PublicAccessType.Blob);

            var blob = blobClient.GetBlobClient(fileName);
            await blob.UploadAsync(fileStream, overwrite: true);

            return GenerateSasUrl(blob);
        }

        public async Task DeleteAsync(string fileName)
        {
            var blobClient = new BlobContainerClient(_connectionString, _containerName);
            var blob = blobClient.GetBlobClient(fileName);
            await blob.DeleteIfExistsAsync();
        }

        private string GenerateSasUrl(BlobClient blob)
        {
            if (!blob.CanGenerateSasUri)
                return blob.Uri.ToString();

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _containerName,
                BlobName = blob.Name,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(6)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasUri = blob.GenerateSasUri(sasBuilder);
            return sasUri.ToString();
        }
    }
}
