## Ollama AI Chat Feature  

The **Ollama AI Chat** allows developers to interact with any Ollama model seamlessly. You can explore all supported models at [Ollama Search](https://ollama.com/search).  

### Running Ollama Locally  

To run an Ollama model locally, you'll need a tool to manage Docker containers. **Docker Desktop** is one of the easiest ways to get started, but you may use other tools such as **Podman** or **Docker Engine on WSL 2**. Visit the official [documentation](https://docs.docker.com/desktop/setup/install/windows-install/) for instructions on how to install Docker Desktop.

Next, do the following steps in the project:  

1. Set `CrestApp.Aspire.HostApp` as your startup project.  
2. Run the project to start the Aspire host, which sets up the necessary environment to connect to any Ollama model locally.  

By default, the project uses the `deepseek-v2:16b` model (8.9GB). Ensure your system has enough storage space before running it. The model will be downloaded automatically on the first run. You can monitor the download and service statuses from the **Resources** tab in the Aspire dashboard.  

### Configuration

To configure the Ollama connection, add the following settings to the `appsettings.json` file:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "Ollama": {
          "DefaultConnectionName": "Default",
          "DefaultDeploymentName": "deepseek-v2:16b",
          "Connections": {
            "Default": {
              "Endpoint": "<!-- Ollama host address -->",
              "DefaultDeploymentName": "deepseek-v2:16b"
            }
          }
        }
      }
    }
  }
}
```

### Aspire

If you are running this project using Aspire, Ollama will be automatically configured for you without needing to manually set the `appsettings.json` file.
