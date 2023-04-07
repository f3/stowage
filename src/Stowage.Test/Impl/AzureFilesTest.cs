using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Config.Net;
using Xunit;

namespace Stowage.Test.Impl;

[Trait("Category", "Integration")]
public class AzureFilesTest
{
   private readonly IFileStorage _storage;

   public AzureFilesTest()
   {
      ITestSettings settings = new ConfigurationBuilder<ITestSettings>()
         .UseIniFile("c:\\tmp\\integration-tests.ini")
         .Build();

      _storage = Files.Of.AzureFilesStorage(settings.AzureStorageAccount, settings.AzureStorageKey, settings.AzureFilesShareName);
   }

   [Fact]
   public async Task ListRootAndImmediateChildrenAsync()
   {
      IReadOnlyCollection<IOEntry> root = await _storage.Ls();

      Assert.NotNull(root);
      foreach(IOEntry folder in root.Where(r => r.Path.IsFolder))
      {
         IReadOnlyCollection<IOEntry> folderContents = await _storage.Ls(folder.Path);
         Assert.NotNull(folderContents);
      }
   }
}
