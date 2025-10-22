using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;

namespace SistemaComercialPyme.Services
{
    public class S3Service
    {
        private readonly string _bucketName;
        private readonly IAmazonS3 _s3Client;

        public S3Service(IConfiguration configuration)
        {
            // Leer variables de entorno
            _bucketName = Environment.GetEnvironmentVariable("AWS_BUCKET_NAME")
                          ?? throw new InvalidOperationException("AWS_BUCKET_NAME no está configurado.");

            string region = Environment.GetEnvironmentVariable("AWS_REGION")
                            ?? throw new InvalidOperationException("AWS_REGION no está configurado.");

            string accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")
                               ?? throw new InvalidOperationException("AWS_ACCESS_KEY_ID no está configurado.");

            string secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY")
                               ?? throw new InvalidOperationException("AWS_SECRET_ACCESS_KEY no está configurado.");

            // Crear cliente S3
            _s3Client = new AmazonS3Client(accessKey, secretKey, RegionEndpoint.GetBySystemName(region));
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folder)
        {
            var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var key = $"{folder}/{fileName}";

            using var newMemoryStream = new MemoryStream();
            await file.CopyToAsync(newMemoryStream);

            var fileTransferUtility = new TransferUtility(_s3Client);
            await fileTransferUtility.UploadAsync(newMemoryStream, _bucketName, key);

            return $"https://{_bucketName}.s3.amazonaws.com/{key}";
        }
    }
}
