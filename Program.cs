using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.Textract;
using Amazon.Textract.Model;

namespace armanproject
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Configure your AWS credentials and region
            var accessKeyId = "AKIATRMF5LV4HOEP45HR";
            var secretAccessKey = "PjMUHt2sHUmfqd05v/ezbtbp6bbkwD4wTo0D9yum";
            var region = RegionEndpoint.APSoutheast1; // Update with your region

            // Initialize Amazon Textract client
            using var textractClient = new AmazonTextractClient(accessKeyId, secretAccessKey, region);

            // Specify the S3 bucket name and the document key
            var bucketName = "armansample";
            var documentKey = "example8.pdf";

            var jobId = await StartDocumentTextDetectionAsync(textractClient, bucketName, documentKey);

            // Wait for the job to complete
            Console.WriteLine("Waiting for job to complete...");
            var jobStatus = await WaitForJobCompletionAsync(textractClient, jobId);

            if (jobStatus == "SUCCEEDED")
            {
                // Get the results of the text detection job
                var extractedContent = await GetExtractedContentAsync(textractClient, jobId);

                // Serialize the extracted content to JSON
                var json = JsonSerializer.Serialize(extractedContent);
                Console.WriteLine(json);
            }
            else
            {
                Console.WriteLine($"Job failed with status: {jobStatus}");
            }
        }

        static async Task<string> StartDocumentTextDetectionAsync(AmazonTextractClient textractClient, string bucketName, string documentKey)
        {
            var request = new StartDocumentTextDetectionRequest
            {
                DocumentLocation = new DocumentLocation
                {
                    S3Object = new S3Object
                    {
                        Bucket = bucketName,
                        Name = documentKey
                    }
                }
            };

            var response = await textractClient.StartDocumentTextDetectionAsync(request);
            return response.JobId;
        }

        static async Task<string> WaitForJobCompletionAsync(AmazonTextractClient textractClient, string jobId)
        {
            string jobStatus = null;

            while (true)
            {
                var response = await textractClient.GetDocumentTextDetectionAsync(new GetDocumentTextDetectionRequest
                {
                    JobId = jobId
                });

                jobStatus = response.JobStatus;

                if (jobStatus == "SUCCEEDED" || jobStatus == "FAILED" || jobStatus == "PARTIAL_SUCCESS")
                {
                    break;
                }

                // Wait before polling again
                await Task.Delay(5000); // Wait for 5 seconds before polling again
            }

            return jobStatus;
        }

        static async Task<ExtractedContent> GetExtractedContentAsync(AmazonTextractClient textractClient, string jobId)
        {
            var response = await textractClient.GetDocumentTextDetectionAsync(new GetDocumentTextDetectionRequest
            {
                JobId = jobId
            });

            // Process the blocks and separate them into text, tables, and forms
            var textBlocks = new List<string>();
            var tableBlocks = new List<string>();
            var formBlocks = new List<string>();

            foreach (var item in response.Blocks)
            {
                switch (item.BlockType)
                {
                    case "LINE":
                        textBlocks.Add(item.Text);
                        break;
                    case "TABLE":
                        tableBlocks.Add(item.Text);
                        break;
                    case "KEY_VALUE_SET":
                        formBlocks.Add(item.Text);
                        break;
                }
            }

            return new ExtractedContent
            {
                Text = textBlocks,
                Tables = tableBlocks,
                Forms = formBlocks
            };
        }
    }

    class ExtractedContent
    {
        public List<string> Text { get; set; }
        public List<string> Tables { get; set; }
        public List<string> Forms { get; set; }
    }
}
