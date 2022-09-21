namespace HealthChecksCommon
{
    public static class Constants
    {
        public const string RedisConnectionString = "REDIS_CONNECTION_STRING";
        
        public const string AzureServiceBusFQNamespace = "AZURE_SERVICE_BUS_FQ_NAMESPACE";
        public const string AzureServiceBusConnectionString = "AZURE_SERVICE_BUS_CONNECTION_STRING";
        public const string AzureServiceBusQueueName = "AZURE_SERVICE_BUS_QUEUE_NAME";
        
        public const string AzureCosmosDbEndpointUri = "AZURE_COSMOSDB_ENDPOINT_URI";
        public const string AzureCosmosDbDatabaseName = "AZURE_COSMOSDB_DATABASE_NAME";
        
        public const string AzureKeyVaultUri = "AZURE_KEYVAULT_URI";
        
        public const string AzureStorageBlobEndpointUri = "AZURE_STORAGE_BLOB_ENDPOINT_URI";
        public const string AzureStorageBlobConnectionString = "AZURE_STORAGE_BLOB_CONNECTION_STRING";
        public const string AzureStorageContainerName = "AZURE_STORAGE_CONTAINER_NAME";
        
        public const string SqlServerConnectionString = "SQL_SERVER_CONNECTION_STRING";

        public const string HttpsEndpointUrls = "HTTPS_ENDPOINT_URLS";
        public const string EchoAllowedHosts = "ECHO_ALLOWED_HOSTS";

        public const int DefaultTimeoutSeconds = 60;
        public const int DefaultTimeoutMilliseconds = DefaultTimeoutSeconds * 1000;
    }
}