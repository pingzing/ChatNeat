# ChatNeat

[![Build Status](https://dev.azure.com/mcalistern/ChatNeat/_apis/build/status/ChatNeat%20Backend?branchName=master)](https://dev.azure.com/mcalistern/ChatNeat/_build/latest?definitionId=18&branchName=master)

A small PoC/example chat service based on Azure SignalR, powered by Azure Functions and Azure Table Storage. It has no authentication, and should not be used by anyone.

# Requirements

## Running ChatNeat.API locally

ChatNeat.API is an Azure Functions project, and can be run locally with any of the following:

- Visual Studio 2019 with the Azure Functions workload installed
- Visutal Studio Code with the [Azure Functions extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-azurefunctions)
- The [Azure Functions CLI tools](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local#v2) and your favorite text editor.

The following variables must be defined in `/ChatNeat.API/local.settings.json`:

```json
  "Values": {
    "AzureWebJobsStorage": "an Azure WebJobs connection string",
    "AzureSignalRConnectionString": "an Azure SignalR Service connection string",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet",
    "CHATNEAT_TABLE_STORAGE_CONNECTION_STRING": "an Azure Table Storage connection string OR ",
  }
```

The `CHATNEAT_TABLE_STORAGE_CONNECTION_STRING` variable can theoretically also point to an Azure Storage emulator, but that hasn't been tested. The emulator has some known differences in behavior compared to a real Storage account, so all testing was done against a real Storage account.

Once set up, run the `ChatNeat.API` project via VS2019, VS Code, or `func start --build`.

## Deploying ChatNeat

Every commit to the master branch is automatically built by Azure DevOps, and deployed to Azure. See [azure-pipelines-backend.yml](https://github.com/pingzing/ChatNeat/blob/v1/azure-pipelines-backend.yml) for details on the build process.

For details about the Release process, see the [Azure DevOps Releases page](https://dev.azure.com/mcalistern/ChatNeat/_release?_a=releases&view=mine&definitionId=1).

The connection string environment variables are set in Azure on the Functions app directly.

## Testing ChatNeat

The API is deployed to https://chatneat.azurewebsites.net. Documentation for the API is available via a [Swagger page](https://chatneat.azurewebsites.net/api/swagger/ui).

In addition, two mobile clients are available to facilitate more natural testing. There is a Windows 10 UWP client, and an Android client, both available on [the Releases page](https://github.com/pingzing/ChatNeat/releases/tag/v1), along with installation instructions.

## Table Design

Azure Table Storage is NoSQL based, and each table has what is effectively a composite key made up of two columns: a `ParitionKey` and a `RowKey`. With that in mind, ChatNeat has three kinds of tables:

- A single `AllGroups` table that tracks every currently active `Group`.
- An arbitrary number of `Group` tables. Each table's name is that `Group`'s unique ID. Each `Group` tracks three things:
  - Its own name and creation time.
  - All current `User`s.
  - All `Message`s.
- An arbitrary number of `User` tables. Each table's name is that `User`'s unique ID. Each `User` tracks only one thing:
  - All the groups it currently belongs to.
