using Azure.AI.OpenAI;
using OpenAI.Assistants;

AzureOpenAIClient azureClient = new(
            new Uri("https://your-deployment-url.openai.azure.com/"),
            new Azure.AzureKeyCredential("your azure open ai api key"));

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
var client = azureClient.GetAssistantClient();
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

var filename = args[0];

// For Azure OpenAI service the model name is the "deployment" name
var assistantCreationOptions = new AssistantCreationOptions
{
    Name = "File question answerer",
    Instructions = "Answer questions from the user about the provided file.",
    Tools = { ToolDefinition.CreateFileSearch() },
};

var assistant = await client.CreateAssistantAsync("gpt4-assistant2", assistantCreationOptions);

var fileUploadResponse = await azureClient.GetFileClient().UploadFileAsync(File.Open(filename, FileMode.Open), 
System.IO.Path.GetFileName(filename), OpenAI.Files.FileUploadPurpose.Assistants);
Console.WriteLine($"Uploaded file {fileUploadResponse.Value.Filename}");

Console.WriteLine("Ask a question about the file (empty response to quit):");
var question = Console.ReadLine();

var thread = await client.CreateThreadAsync();


while (!string.IsNullOrWhiteSpace(question))
{
    var messageCreationOptions = new MessageCreationOptions();
    messageCreationOptions.Attachments.Add(new MessageCreationAttachment(fileUploadResponse.Value.Id, new List<ToolDefinition>() {ToolDefinition.CreateFileSearch()}));

    await client.CreateMessageAsync(thread, new List<MessageContent>() { MessageContent.FromText(question)}, messageCreationOptions);

    await foreach (StreamingUpdate streamingUpdate
            in client.CreateRunStreamingAsync(thread, assistant, new RunCreationOptions()))
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
await azureClient.GetFileClient().DeleteFileAsync(fileUploadResponse.Value.Id);
await client.DeleteThreadAsync(thread.Value.Id);
await client.DeleteAssistantAsync(assistant.Value.Id);
