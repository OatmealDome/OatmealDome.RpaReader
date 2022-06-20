# OatmealDome.RpaReader

This library allows you to access individual files within a Ren'Py archive file. Versions 2 and 3 of the format are supported.

## Usage

```csharp
using Stream archiveStream = File.OpenRead(path);
RenPyArchive archive = new RenPyArchive(archiveStream);

// Fetch a file
using Stream fileStream = archive.GetFile("file.rpy");

// Get all files in the archive
foreach (string path in archive)
{
    // Do something
}
```

# Credits

Thanks to Kasadee's [rpaextract](https://github.com/Kaskadee/rpaextract) and [Shizmob's rpatool](https://github.com/Shizmob/rpatool) for details about the Ren'Py archive format.
