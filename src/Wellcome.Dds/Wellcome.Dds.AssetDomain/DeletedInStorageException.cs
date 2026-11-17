using System;

namespace Wellcome.Dds.AssetDomain
{
    /// <summary>
    /// The item's latest version in the storage service has no files: it was deleted there, for example by a
    /// Goobi deletion bag ("{date}_{b number}_deletion"), so the DDS has nothing to build.
    /// Earlier versions are still in storage, and anything the DDS published from them is still served.
    /// </summary>
    public class DeletedInStorageException : InvalidOperationException
    {
        public string Identifier { get; }
        public string? Version { get; }
        public string? BaggingDate { get; }
        public string? SenderDescription { get; }

        public DeletedInStorageException(string identifier, string? version, string? baggingDate,
            string? senderDescription)
            : base($"{identifier} was deleted in the storage service: its latest version ({version}, bagged " +
                   $"{baggingDate}, \"{senderDescription}\") has no files, so there is nothing to build.")
        {
            Identifier = identifier;
            Version = version;
            BaggingDate = baggingDate;
            SenderDescription = senderDescription;
        }

        /// <summary>
        /// The DeletedInStorageException in this exception or its inner exceptions, if there is one
        /// </summary>
        public static DeletedInStorageException? Find(Exception? exception)
        {
            while (exception != null)
            {
                if (exception is DeletedInStorageException deleted)
                {
                    return deleted;
                }
                exception = exception.InnerException;
            }
            return null;
        }
    }
}
