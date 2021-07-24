using System;

namespace AzureStorageProvider.Exceptions
{
    public class ExistingBlobException : Exception
    {
        public ExistingBlobException(string message = null, Exception innerException = null) : base(message, innerException)
        {
        }
    }
}
