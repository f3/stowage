using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Stowage.Impl.Microsoft;

/// <summary>
/// Represents a Stowage storage provider for an Azure Files file share
/// </summary>
class AzureFilesFileStorage : PolyfilledHttpFileStorage
{
   // SAS auth examples:
   // https://learn.microsoft.com/en-us/rest/api/storageservices/service-sas-examples?source=recommendations#file-examples
   // Shared key auth info:
   // https://learn.microsoft.com/en-us/rest/api/storageservices/authorize-with-shared-key#specifying-the-authorization-header

   private readonly string _shareName;

   /// <summary>
   /// Initializes a new instance of the <see cref="AzureFilesFileStorage"/> class
   /// with the given Azure Storage storage account name, Azure Files file share name, and
   /// <see cref="DelegatingHandler"/> for authorization. 
   /// </summary>
   /// <param name="accountName">The Azure Storage storage account name.</param>
   /// <param name="shareName">The name of the file share.</param>
   /// <param name="authHandler">The HTTP handler used for authentication/authorization.</param>
   /// <exception cref="ArgumentException">Thrown if <paramref name="accountName"/> is null or empty.</exception>
   public AzureFilesFileStorage(string accountName, string shareName, DelegatingHandler authHandler)
      : this(new Uri($"https://{accountName}.file.core.windows.net"), shareName, authHandler)
   {
      if(string.IsNullOrWhiteSpace(accountName))
         throw new ArgumentException($"{nameof(accountName)} cannot be empty", nameof(accountName));
   }

   /// <summary>
   /// Initializes a new instance of the <see cref="AzureFilesFileStorage"/> class
   /// with the given Azure Storage endpoint <see cref="Uri"/>, Azure Files file share
   /// name, and <see cref="DelegatingHandler"/> for authorization. 
   /// </summary>
   /// <param name="endpoint">The <see cref="Uri"/> of the Azure Files storage account.</param>
   /// <param name="shareName">The name of the file share.</param>
   /// <param name="authHandler">The HTTP handler used for authentication/authorization.</param>
   /// <exception cref="ArgumentException">Thrown if the <paramref name="shareName"/> is null or empty.</exception>
   /// <exception cref="ArgumentNullException">Thrown if the <paramref name="authHandler"/> is null.</exception>
   public AzureFilesFileStorage(Uri endpoint, string shareName, DelegatingHandler authHandler)
      : base(endpoint, authHandler)
   {
      if(!this.IsValidShareName(shareName))
         throw new ArgumentException($"{nameof(shareName)} is invalid", nameof(shareName));
      if(authHandler is null)
         throw new ArgumentNullException(nameof(authHandler));

      _shareName = shareName;
   }

   public override Task<bool> Exists(IOPath path, CancellationToken cancellationToken = default) => base.Exists(path, cancellationToken);

   public override Task<IReadOnlyCollection<IOEntry>> Ls(IOPath path = null, bool recurse = false, CancellationToken cancellationToken = default)
   {
      // https://myaccount.file.core.windows.net/myshare/mydirectorypath?restype=directory&comp=list
      if(path != null && !path.IsFolder)
         throw new ArgumentException($"{nameof(path)} needs to be a folder", nameof(path));

      if(path != null && !this.HasValidDirectoryOrFileName(path))
         throw new ArgumentException($"'{path}' is not a valid Azure Files directory name", nameof(path));

      // https://myaccount.file.core.windows.net/myshare/mydirectorypath?restype=directory&comp=list
      return ListAsync(path, recurse, cancellationToken);
   }

   public override async Task<Stream> OpenRead(IOPath path, CancellationToken cancellationToken = default)
   {
      if(path is null)
         throw new ArgumentNullException(nameof(path));

      // call https://docs.microsoft.com/en-us/rest/api/storageservices/get-blob
      using var request = new HttpRequestMessage(HttpMethod.Get, $"{GetShareName(path)}/{GetPathInShare(path)}");
      using HttpResponseMessage response = await SendAsync(request, cancellationToken);

      if(response.StatusCode == HttpStatusCode.NotFound)
         return null;

      response.EnsureSuccessStatusCode();

      return await response.Content.ReadAsStreamAsync();
   }

   public override Task<Stream> OpenWrite(IOPath path, CancellationToken cancellationToken = default)
   {
      throw new NotImplementedException();
   }

   public override Task<string> ReadText(IOPath path, Encoding encoding = null, CancellationToken cancellationToken = default)
   {
      return base.ReadText(path, encoding, cancellationToken);
   }

   public override Task Ren(IOPath oldPath, IOPath newPath, CancellationToken cancellationToken = default)
   {
      return base.Ren(oldPath, newPath, cancellationToken);
   }

   public override Task Rm(IOPath path, bool recurse = false, CancellationToken cancellationToken = default)
   {
      throw new NotImplementedException();
   }

   public override Task WriteText(IOPath path, string contents, Encoding encoding = null, CancellationToken cancellationToken = default)
   {
      return base.WriteText(path, contents, encoding, cancellationToken);
   }

   private async Task<IReadOnlyCollection<IOEntry>> ListAsync(string path, bool recurse, CancellationToken cancellationToken = default)
   {
      var result = new List<IOEntry>();

      string relativePath = GetPathInShare(path);

      string rawXml = await ListAsync(GetShareName(path), relativePath, delimiter: recurse ? null : "/", cancellationToken: cancellationToken);

      var x = XElement.Parse(rawXml);
      XElement entries = x.Element("Entries");
      if(entries != null)
      {
         result.AddRange(ConvertBatch(entries));
      }

      return result;
   }

   private async Task<string> ListAsync(string shareName, string relativePath = null, string delimiter = null, string include = "timestamps%82attributes", CancellationToken cancellationToken = default)
   {
      string url = $"{shareName}";

      if(!string.IsNullOrWhiteSpace(relativePath) && relativePath != IOPath.PathSeparatorString)
         url += $"/{relativePath}";

      url += "?restype=directory&comp=list";

      using var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
      requestMessage.Headers.Add("x-ms-file-extended-info", "true");

      HttpResponseMessage response = await SendAsync(requestMessage, cancellationToken);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadAsStringAsync();
   }

   private string GetShareName(string path) =>
      _shareName ?? path.Split(IOPath.PathSeparatorChar, 2)[0];

   private string GetPathInShare(string path) =>
      IOPath.Normalize(_shareName == null ? IOPath.RemoveRoot(path) : path, true);

   private static IEnumerable<IOEntry> ConvertBatch(XElement entries)
   {
      foreach(XElement prefix in entries.Elements("Prefix"))
      {
         string name = prefix.Value;
         yield return new IOEntry(name + IOPath.PathSeparatorString);
      }

      foreach(XElement entry in entries.Elements().Where(e => new[] { "Directory", "File" }.Contains(e.Name.LocalName)))
      {
         string name = $"{entry.Element("Name").Value}{(entry.Name.LocalName == "Directory" ? IOPath.PathSeparatorString : "")}";
         var file = new IOEntry(name);

         foreach(XElement xp in entry.Element("Properties").Elements())
         {
            string pname = xp.Name.ToString();
            string pvalue = xp.Value;

            if(!string.IsNullOrEmpty(pvalue))
            {
               if(pname == "Last-Modified")
               {
                  file.LastModificationTime = DateTimeOffset.Parse(pvalue);
               }
               else if(pname == "Content-Length")
               {
                  file.Size = long.Parse(pvalue);
               }
               else if(pname == "Content-MD5")
               {
                  file.MD5 = pvalue;
               }
               else if(pname == "Etag")
               {
                  file.Tag = pvalue;
               }
               else
               {
                  file.Properties[pname] = pvalue;
               }
            }
         }

         yield return file;
      }
   }
}
