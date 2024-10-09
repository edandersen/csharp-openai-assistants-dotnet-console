using System.ClientModel;
using Azure.AI.OpenAI;
using OpenAI.Assistants;
using OpenAI.Files;

AzureOpenAIClient azureClient = new(
new Uri("https://your-deployment-url.openai.azure.com/"),
new ApiKeyCredential("your azure open ai api key"));

var client = azureClient.GetAssistantClient();

var filename = args[0];
using var fileStream = File.Open(filename, FileMode.Open);
ClientResult<OpenAIFile> fileUploadResponse =
    await azureClient.GetOpenAIFileClient()
        .UploadFileAsync(fileStream, Path.GetFileName(filename), FileUploadPurpose.Assistants);

VectorStoreCreationHelper vectorStoreCreationHelper = new VectorStoreCreationHelper();
vectorStoreCreationHelper.FileIds.Add(fileUploadResponse.Value.Id);

var assistantCreationOptions = new AssistantCreationOptions
{
    Name = "File question answerer",
    Instructions = "Answer questions from the user about the provided file.",
    Temperature = 0.01f,
    Tools =
            {
                new CodeInterpreterToolDefinition(),
                FileSearchToolDefinition.CreateFileSearch(),
            },
    ToolResources = new()
    {
        FileSearch = new()
        {
            NewVectorStores =
                    {
                        vectorStoreCreationHelper,
                    }
        }
    }

};

// For Azure OpenAI service the model name is the "deployment" name
var assistant = await client.CreateAssistantAsync("gpt4-assistant2", assistantCreationOptions);

Console.WriteLine($"Assistant Created!");
Console.WriteLine($"Uploaded file {fileUploadResponse.Value.Filename}");
Console.WriteLine("Ask a question about the file (empty response to quit):");

var question = Console.ReadLine();

var thread = await client.CreateThreadAsync();

while (!string.IsNullOrWhiteSpace(question))
{
    await client.CreateMessageAsync(thread.Value.Id, MessageRole.User, new List<MessageContent>() { MessageContent.FromText(question) });

    await foreach (StreamingUpdate streamingUpdate
            in client.CreateRunStreamingAsync(thread.Value.Id, assistant.Value.Id, new RunCreationOptions()))
    {
        if (streamingUpdate.UpdateKind == StreamingUpdateReason.RunCreated)
        {
            Console.WriteLine($"--- Run started! ---");
        }

        else if (streamingUpdate is MessageContentUpdate contentUpdate)
        {
            if (contentUpdate?.TextAnnotation?.InputFileId == fileUploadResponse.Value.Id)
            {
                Console.Write(" (From: " + fileUploadResponse.Value.Filename + ")");
            }
            else
            {
                Console.Write(contentUpdate?.Text);
            }
        }
    }

    Console.WriteLine();
    Console.WriteLine("Your response: (leave empty to quit)");
    question = Console.ReadLine();
}

// clean up the file and assistant
Console.WriteLine("Cleaning up and exiting...");
await azureClient.GetOpenAIFileClient().DeleteFileAsync(fileUploadResponse.Value.Id);
await client.DeleteThreadAsync(thread.Value.Id);
await client.DeleteAssistantAsync(assistant.Value.Id);
