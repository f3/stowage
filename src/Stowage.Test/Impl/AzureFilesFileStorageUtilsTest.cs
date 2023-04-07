using System.Collections.Generic;
using System.Threading.Tasks;
using Stowage.Impl.Microsoft;
using Xunit;

namespace Stowage.Test.Impl;

[Trait("Category", "Integration")]
public class AzureFilesFileStorageUtilsTest
{
   [Theory]
   [InlineData(null, false)] // srsly?
   [InlineData("", false)] // no good
   [InlineData("     ", false)] // nuh-uh
   [InlineData("lorem-ipsum-dolor-sit", true)]
   [InlineData("-lorem-ipsum-dolor-sit", false)] // starts with a dash, no good
   [InlineData("lorem-ipsum-dolor-sit-", false)] // ends with a dash, nope
   [InlineData("lorem--ipsum-dolor-sit-", false)] // double-dashes; c'mon, really?
   [InlineData("lo", false)] // length >= 0 but less than 3; sorry
   [InlineData("123", true)] // minimum 3 alphanumerics (good)
   [InlineData("123456789012345678901234567890123456789012345678901234567890123", true)] // length = 63 (good)
   [InlineData("1234567890123456789012345678901234567890123456789012345678901234", false)] // length = 64 (insane)
   public Task IsValidShareName_validates_share_names(string shareName, bool isExpectedToBeValid)
   {
      AzureFilesFileStorage testStorage = default;

      Assert.Equal(isExpectedToBeValid, testStorage.IsValidShareName(shareName));

      return Task.CompletedTask;
   }

   [Theory]
   [MemberData(nameof(GetFileSystemResourceNames))]
   public Task IsValidDirectoryOrFileName_validates_file_or_directory_names(string fileOrDirectoryName, bool isExpectedToBeValid, bool isFolder = false)
   {
      AzureFilesFileStorage testStorage = default;
      var ioPath = new IOPath(fileOrDirectoryName);

      Assert.Equal(isExpectedToBeValid, testStorage.HasValidDirectoryOrFileName(ioPath));
      Assert.Equal(isFolder, ioPath.IsFolder);

      return Task.CompletedTask;
   }

   public static IEnumerable<object[]> GetFileSystemResourceNames()
   {
      yield return new object[] { null, true, true }; // IOPath is ok with this (ends up being "root")
      yield return new object[] { "", true, true }; // same ⬆
      yield return new object[] { "foo.txt", true };
      yield return new object[] { "foo/bar/baz.txt", true };
      yield return new object[] { new string('x', 255), true };
      yield return new object[] { new string('x', 256), false }; // too big
      yield return new object[] { "f", true };
      yield return new object[] { "3", true };
      yield return new object[] { "LPT", true }; // valid
      yield return new object[] { "LPT0", true }; // LPT0 is valid, but
      yield return new object[] { "LPT1", false }; // LPT1 through LPT9 are not
      yield return new object[] { "LPT2", false }; //   |
      yield return new object[] { "LPT3", false }; //   |
      yield return new object[] { "LPT4", false }; //  \/
      yield return new object[] { "LPT5", false };
      yield return new object[] { "LPT6", false };
      yield return new object[] { "LPT7", false };
      yield return new object[] { "LPT8", false };
      yield return new object[] { "LPT9", false };
      yield return new object[] { "LPT10", true }; // ...but LPT10 is ok
      yield return new object[] { "COM", true };  // valid
      yield return new object[] { "COM0", true };  // COM0 is valid, but
      yield return new object[] { "COM1", false }; // COM1 through COM9 are not
      yield return new object[] { "COM2", false }; //   |
      yield return new object[] { "COM3", false }; //   |
      yield return new object[] { "COM4", false }; //  \/
      yield return new object[] { "COM5", false };
      yield return new object[] { "COM6", false };
      yield return new object[] { "COM7", false };
      yield return new object[] { "COM8", false };
      yield return new object[] { "COM9", false };
      yield return new object[] { "COM10", true }; // ...but COM10 is fine
      yield return new object[] { "COMB", true };
      yield return new object[] { "PRN", false };
      yield return new object[] { "PRNT", true };
      yield return new object[] { "AUX", false };
      yield return new object[] { "FAUX", true };
      yield return new object[] { "CON", false };
      yield return new object[] { "CONE", true };
      yield return new object[] { "CLOCK$", false };
      yield return new object[] { "CLOCK", true };
      yield return new object[] { ".", false };
      yield return new object[] { "..", false };
      yield return new object[] { "file.", false };
      yield return new object[] { "file..", false };
      yield return new object[] { "file...", false };
      yield return new object[] { "folder/", true, true };
      yield return new object[] { "folder//", true, true }; // IOPath will strip off the extra /'s
      yield return new object[] { "C:folder/", false, true };
      yield return new object[] { "folder\\/", false, true };
      yield return new object[] { "file-or-folder\\", false };
      yield return new object[] { "f\\\\ile", false };
      yield return new object[] { "<-file", false };
      yield return new object[] { "file->", false };
      yield return new object[] { "fi*le", false };
      yield return new object[] { "fi?le", false };
      yield return new object[] { "\"file\"", false };
      yield return new object[] { "fil\0e", false }; // Control characters and surrogates (invalid)
      yield return new object[] { "fi\ale", false }; //    |
      yield return new object[] { "f\bile", false }; //    |
      yield return new object[] { "fil\fe", false }; //   \/
      yield return new object[] { "fil\ne", false };
      yield return new object[] { "fil\re", false };
      yield return new object[] { "fil\ve", false };
      yield return new object[] { "fil\u000ee", false };
      yield return new object[] { "fil\u0015e", false };
      yield return new object[] { "Xสีน้ำเงิน", false };
   }
}

