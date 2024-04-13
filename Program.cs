using Azure;
using Azure.AI.OpenAI.Assistants;

// Azure OpenAI service
var client = new AssistantsClient(new Uri("https://your-azure-openai-service.openai.azure.com/"), 
new AzureKeyCredential("azure-openai-api-key"));

// OpenAI API
// var client = new AssistantsClient("standard OpenAI key");

if (args.Length < 1) {
    Console.WriteLine("Provide a file path as the first argument or drag and drop onto the binary");
    return;
}

// For Azure OpenAI service the model name is the "deployment" name
var assistantCreationOptions = new AssistantCreationOptions("gpt-4")
{
    Name = "File question answerer",
    Instructions = "Answer questions from the user about the provided file. " +
    "For PDF files, immediately use PyPDF2 to extract the text contents and answer quesions based on that.",
    Tools = { new CodeInterpreterToolDefinition() },
};


var fileUploadResponse = await client.UploadFileAsync(args[0], OpenAIFilePurpose.Assistants);
assistantCreationOptions.FileIds.Add(fileUploadResponse.Value.Id);
Console.WriteLine($"Uploaded file {fileUploadResponse.Value.Filename}");

assistantCreationOptions.Instructions += $" The file with id {fileUploadResponse.Value.Id} " +
$"has a original filename of {Path.GetFileName(args[0])} and is " +
$" a {Path.GetExtension(args[0]).Replace(".",string.Empty)} file.";

var assistant = await client.CreateAssistantAsync(assistantCreationOptions);
var thread = await client.CreateThreadAsync();

Console.WriteLine("Ask a question about the file (empty response to quit):");
var question = Console.ReadLine();

while (!string.IsNullOrWhiteSpace(question))
{
    string? lastMessageId = null;

    await client.CreateMessageAsync(thread.Value.Id, MessageRole.User, question);
    var run = await client.CreateRunAsync(thread.Value.Id, new CreateRunOptions(assistant.Value.Id));
    Response<ThreadRun> runResponse;

    do {
        await Task.Delay(TimeSpan.FromMilliseconds(1000));
        runResponse = await client.GetRunAsync(thread.Value.Id, run.Value.Id);
        Console.Write(".");
    } while (runResponse.Value.Status == RunStatus.Queued
            || runResponse.Value.Status == RunStatus.InProgress);
    
    Console.WriteLine(string.Empty);

    var messageResponse = await client.GetMessagesAsync(thread.Value.Id, order: ListSortOrder.Ascending, after: lastMessageId);

    foreach(var message in messageResponse.Value.Data)
    {
        lastMessageId = message.Id;
        foreach(var content in message.ContentItems)
        {
            if (content is MessageTextContent textContent)
            {
                if (textContent.Text != question)
                {
                    Console.WriteLine(textContent.Text);
                }
                
            }
        }
    }

    Console.WriteLine("Your response: (leave empty to quit)");
    question = Console.ReadLine();

}

// clean up the file and assistant
Console.WriteLine("Cleaning up and exiting...");
await client.DeleteFileAsync(fileUploadResponse.Value.Id);
await client.DeleteThreadAsync(thread.Value.Id);
await client.DeleteAssistantAsync(assistant.Value.Id);
