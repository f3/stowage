using System.Linq;
using System.Text.RegularExpressions;

namespace Stowage.Impl.Microsoft;

static class AzureFilesFileStorageUtils
{
   /// <summary>
   /// <para>Returns whether the given <paramref name="shareName"/> is valid according
   /// to the restrictions for an Azure Files share name.</para>
   /// <seealso href="https://learn.microsoft.com/en-us/rest/api/storageservices/naming-and-referencing-shares--directories--files--and-metadata#share-names"/>
   /// </summary>
   public static bool IsValidShareName(this AzureFilesFileStorage _, string shareName)
   {
      /*
      The naming restrictions for shares are as follows:
      - A share name must be a valid DNS name.
      - Share names must start with a letter or number, and can contain only letters, numbers, and the dash (-) character.
      - Every dash (-) character must be immediately preceded and followed by a letter or number; consecutive dashes are not permitted in share names.
      - All letters in a share name must be lowercase.
      - Share names must be from 3 through 63 characters long.

         NB: So apparently you cannot create an Azure Files share with Unicode characters, even
          though they could could be constructed as valid IDN labels via Punycode; for example:
          You cannot create a share like "пророк21.file.core.windows.net" even though it would
          resolve to "xn--21-1lcpaehb.file.core.windows.net"
      */

      // Expression accounts for everything but the consecutive dashes
      const string ValidSegmentExpression = "^[a-z0-9](?:[a-z0-9-]{1,61}[a-z0-9])?$";

      return !string.IsNullOrWhiteSpace(shareName) &&
         Regex.IsMatch(shareName, ValidSegmentExpression, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline) &&
         !shareName.Contains("--");
   }

   /// <summary>
   /// <para>Returns whether the name of the given <paramref name="ioPath"/> follows the
   /// naming rules for an Azure Files directory or file name.</para>
   /// <seealso href="https://learn.microsoft.com/en-us/rest/api/storageservices/naming-and-referencing-shares--directories--files--and-metadata#directory-and-file-names"/>
   /// </summary>
   public static bool HasValidDirectoryOrFileName(this AzureFilesFileStorage _, IOPath ioPath)
   {
      /*
      Azure Files enforces the following naming rules for directory and file names:
      - Directory and file names are case-preserving and case-insensitive.
      - Directory and file component names must be no more than 255 characters in length.
      - Directory names cannot end with the forward slash character (/). If provided, it will be automatically removed.
      - File names must not end with the forward slash character (/).
      - Reserved URL characters must be properly escaped.
      - The following characters are not allowed: " \ / : | < > * ?
      - Illegal URL path characters not allowed. Code points like \uE000, while valid in NTFS filenames, are not valid Unicode
        characters. In addition, some ASCII or Unicode characters, like control characters (0x00 to 0x1F, \u0081, etc.), are
        also not allowed. For rules governing Unicode strings in HTTP/1.1 see RFC 2616, Section 2.2: Basic Rules and RFC 3987.
      - The following file names are not allowed:
        LPT1, LPT2, LPT3, LPT4, LPT5, LPT6, LPT7, LPT8, LPT9, COM1, COM2, COM3, COM4, COM5, COM6, COM7, COM8, COM9, PRN, AUX,
        NUL, CON, CLOCK$, dot character (.), and two dot characters (..).
      - Starting with version 2021-12-02, directory and file names will support U+FFFE and U+FFFF characters through all operations.
        These characters will also be supported through the SMB protocol. List Directory and Files and List Handles operations will
        need special handling for these characters as mentioned in their respective documentation.

      By default, dot (.) characters at the end of directory and file names in request URLs are ignored or left out.
      - For example, if a file named "file1..." is being created, the dots at the end will be ignored, and a file named "file1" will
        be created. The same applies to directories in the path. If a file creation request includes the path "\Dir1\Dir2…\File1"
        then the file will be created at "\Dir1\Dir2\File1".
      - However, starting with version 2022-11-02, the default behavior can be overridden by setting the header x-ms-allow-trailing-dot
        to true in the URL request.
      - For example, if you want to create a file named "file1..." and include the trailing dots, the x-ms-allow-trailing-dot should be
        included in request header and set to be true. The same applies for creating directory names.
      - In case of a file copy request, if you want to include trailing dots in the source file name, the x-ms-source-allow-trailing-dot header
        must be set to true. For more information, check out the available header options for each individual REST API.      
      */
      string resourceName = ioPath?.Name?.Trim();

      if(ioPath == null || string.IsNullOrWhiteSpace(resourceName))
         return false;

      // If we're a folder, trim off the *last* trailing '/' so we can continue
      //  with the validations (an IOPath of "folder/" is ok, but "folder//" is not)
      resourceName = ioPath.IsFolder
         ? (resourceName.EndsWith(IOPath.PathSeparator) ? resourceName[..^1] : resourceName)
         : resourceName;

      if(resourceName.Length > 255 || resourceName.EndsWith('/'))
         return false;

      if(!System.Uri.IsWellFormedUriString(resourceName, System.UriKind.RelativeOrAbsolute))
         return false;

      if(Regex.IsMatch(resourceName, "[\"\\/\\:\\|\\<\\>\\*\\?]"))
         return false;

      if(resourceName.Any(char.IsControl))
         return false;

      const string AzureFilesReservedNamesExpression = "^(LPT[1-9]|COM[1-9]|PRN|AUX|NUL|CON|CLOCK\\$|(?:.*\\.{1,2}))$";

      return !Regex.IsMatch(resourceName, AzureFilesReservedNamesExpression);
   }
}