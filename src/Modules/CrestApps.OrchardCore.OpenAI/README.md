## OpenAI Chat Feature

The **OpenAI AI Chat** feature enhances the **AI Services** functionality by integrating OpenAI's models. It provides a suite of services to interact with these models, enabling advanced AI capabilities.

### Configuration

To configure the OpenAI connection, add the following settings to the `appsettings.json` file:

```json
{
  "OrchardCore": {
    "CrestApps_AI": {
      "Providers": {
        "OpenAI": {
          "DefaultConnectionName": "openai-cloud",
          "DefaultDeploymentName": "gpt-4o-mini",
          "Connections": {
            "openai-cloud": {
              "ApiKey": "<!-- Your API Key Goes here -->",
              "DefaultDeploymentName": "gpt-4o-mini"
            }
          }
        }
      }
    }
  }
}
```

---

## OpenAI-Compatible Chat Feature  

The **OpenAI-Compatible Chat** feature enhances the **OpenAI Chat** functionality by enabling integration with any AI model provider that adheres to OpenAI’s standards.  

### Enabling and Configuring a Connection  

1. **Access the Settings**  
   - Navigate to **"Artificial Intelligence"** in the admin menu.  
   - Click on **"OpenAI Connections"** to configure a new connection.  

2. **Adding a New Connection**  
   - Click **"Add Connection"** and provide the required details.  
   - For example, to connect to **Google Gemini**:  
     - Visit [Google AI Studio](https://aistudio.google.com) and generate an **API Key**.  
     - In the **Endpoint** field, enter:  
       ```
       https://generativelanguage.googleapis.com/v1beta/openai/
       ```  
     - In the **Deployment/Model** field, specify the model name, such as **gemini-2.0-flash**.  

### Creating AI Profiles  

Once the connection is set up, you can create **AI profiles** that will interact with the configured model.  

### Using AI Deployments  

If the **AI Deployments** feature is enabled, you can create multiple deployments under the same connection. This allows different AI profiles to utilize different models while sharing the same connection.  
