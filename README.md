# Health Checks .NET

Some health checks in an ASP.NET Web API and an Azure Function. Containerised for easy deployment.

## Getting started

Deploy container to a Linux Azure App Service:

```bash
az webapp config container set -n myWebApp -g myResourceGroup -i daniellarsennz/healthchecksaspnet
```

Deploy container to a Linux Azure Function App:

```bash
az functionapp config container set -n myFunctionApp -g myResourceGroup -i daniellarsennz/healthchecksazurefunctions 
```

## Health Checks ASP.NET

The Health Checks ASP.NET container provides three endpoints:


## Configuration

Health Checks .NET will report detailed failure status reports to Application Insights. The Instrumentation Key must be configured:

    "APPINSIGHTS_INSTRUMENTATIONKEY": "(Azure Application Insights Instrumentation Key)"

The following Health Checks can be configured via App Settings or Environment Variables:

### Redis cache

    "REDIS_CONNECTION_STRING": "(Stack Overflow style Redis connection string)"

### Azure Service Bus

    "AZURE_SERVICE_BUS_CONNECTION_STRING": "(Azure Service Bus connection string)",
    "AZURE_SERVICE_BUS_QUEUE_NAME": "(Name of a Queue to test. This queue must exist.)"

### Azure Cosmos DB

> **Note**: Both settings must be set for the Health Check to be included in results.

    "AZURE_COSMOSDB_CONNECTION_STRING": "(Azure Cosmos DB Connection String)",
    "AZURE_COSMOSDB_DATABASE_NAME": "(Name of a Database to test. This Database must exist.)"

### Azure Key Vault

    "AZURE_KEYVAULT_URI": "(Azure Key Vault URI)"

### Azure Blob Storage

> **Note**: Both settings must be set for the Health Check to be included in results.

    "AZURE_STORAGE_CONNECTION_STRING": "(Azure Storage connection string)",
    "AZURE_STORAGE_CONTAINER_NAME": "(Name of a blob container to test. This container must exist.)"

### SQL Server

    "SQL_SERVER_CONNECTION_STRING": "(SQL Server or Azure SQL connection string)",

### HTTPS endpoints

Any HTTPS endpoint can be checked. The health check result will be Healthy if the response has a non-error status code. Multiple endpoints can be checked in individual health checks by separating URLs with a semi-colon (;). For example:

    "HTTPS_ENDPOINT_URLS": "https://www.google.com;https://localhost"
