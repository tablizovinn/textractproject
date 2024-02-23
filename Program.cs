using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
                var json = JsonSerializer.Serialize(extractedContent, new JsonSerializerOptions { WriteIndented = true });
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

        static async Task<MainDocument> GetExtractedContentAsync(AmazonTextractClient textractClient, string jobId)
        {
            var response = await textractClient.GetDocumentTextDetectionAsync(new GetDocumentTextDetectionRequest
            {
                JobId = jobId
            });

            // Initialize lists to store extracted content
            var textBlocks = new List<ContentBlock>();
            var tableBlocks = new List<Table>();
            var formBlocks = new List<Form>();

            // Iterate through each block in the response
            foreach (var block in response.Blocks)
            {
                switch (block.BlockType)
                {
                    case "LINE":
                        // Extract text
                        textBlocks.Add(new ContentBlock { Confidence = block.Confidence, Text = block.Text });
                        break;
                    case "TABLE":
                        // Extract tables
                        var table = ExtractTableFromBlock(response, block);
                        if (table != null)
                        {
                            tableBlocks.Add(table);
                        }
                        break;
                    case "KEY_VALUE_SET":
                        // Extract forms (key-value pairs)
                        var form = ExtractFormFromBlock(response, block);
                        if (form != null)
                        {
                            formBlocks.Add(form);
                        }
                        break;
                }
            }

            return new MainDocument
            {
                Text = textBlocks,
                Tables = tableBlocks,
                Forms = formBlocks
            };
        }

        static Table ExtractTableFromBlock(GetDocumentTextDetectionResponse response, Block tableBlock)
        {
            var table = new Table();
            foreach (var relationship in tableBlock.Relationships)
            {
                if (relationship.Type == "CHILD")
                {
                    foreach (var cellId in relationship.Ids)
                    {
                        var cellBlock = response.Blocks.FirstOrDefault(b => b.Id == cellId);
                        if (cellBlock != null && cellBlock.BlockType == "CELL")
                        {
                            table.AddRow(new ContentBlock { Confidence = cellBlock.Confidence, Text = cellBlock.Text });
                        }
                    }
                }
            }
            return table;
        }

        static Form ExtractFormFromBlock(GetDocumentTextDetectionResponse response, Block formBlock)
        {
            var form = new Form();
            var fields = new Dictionary<string, object>();
            foreach (var relationship in formBlock.Relationships)
            {
                if (relationship.Type == "CHILD")
                {
                    foreach (var childId in relationship.Ids)
                    {
                        var childBlock = response.Blocks.FirstOrDefault(b => b.Id == childId);
                        if (childBlock != null && childBlock.BlockType == "KEY")
                        {
                            // Find the value blocks related to this key
                            var valueIds = relationship.Ids.Where(id => id != childId && response.Blocks.Any(b => b.Id == id && b.BlockType == "VALUE"));
                            foreach (var valueId in valueIds)
                            {
                                var valueBlock = response.Blocks.FirstOrDefault(b => b.Id == valueId);
                                if (valueBlock != null)
                                {
                                    fields.Add(childBlock.Text, valueBlock.Text);
                                }
                            }
                        }
                    }
                }
            }
            form.Fields = fields;
            return form;
        }
    }

    class MainDocument
    {
        public List<ContentBlock> Text { get; set; }
        public List<Table> Tables { get; set; }
        public List<Form> Forms { get; set; }
    }

    class ContentBlock
    {
        public float Confidence { get; set; }
        public string Text { get; set; }
    }

    class Table
    {
        public List<ContentBlock> Rows { get; } = new List<ContentBlock>();

        public void AddRow(ContentBlock rowBlock)
        {
            Rows.Add(rowBlock);
        }
    }

    class Form
    {
        public Dictionary<string, object> Fields { get; set; }
    }
}
