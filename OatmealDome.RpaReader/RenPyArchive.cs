using System.Collections;
using System.Text;
using OatmealDome.BinaryData;
using Razorvine.Pickle;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;

namespace OatmealDome.RpaReader;

public sealed class RenPyArchive : IDisposable, IEnumerable<string>
{
    private const string VersionTwoMagic = "RPA-2.0";
    private const string VersionThreeMagic = "RPA-3.0";

    class ArchiveIndex
    {
        public long Offset;
        public int Length;
        
        // TODO: find an archive that uses this and implement
        // public byte[] Prefix;
    }

    private Stream _stream;
    private BinaryDataReader _reader;

    private int _version;
    private Dictionary<string, ArchiveIndex> _indices;
    
    public RenPyArchive(Stream stream)
    {
        _stream = stream;
        _indices = new Dictionary<string, ArchiveIndex>();
        
        Read();
    }

    public RenPyArchive(byte[] rawData) : this(new MemoryStream(rawData))
    {
        //
    }

    private void Read()
    {
        _reader = new BinaryDataReader(_stream, true);

        _reader.Seek(0, SeekOrigin.Begin);
        
        string magic = _reader.ReadString(7, Encoding.ASCII);
        switch (magic)
        {
            case VersionTwoMagic:
                _version = 2;
                break;
            case VersionThreeMagic:
                _version = 3;
                break;
            default:
                throw new RpaReaderException($"Unsupported archive format {magic}");
        }

        _reader.Seek(1); // skip whitespace
        
        // Read the rest of the header string
        int headerStringLen = 0;
        using (_reader.TemporarySeek())
        {
            while (_reader.ReadByte() != '\n')
            {
                headerStringLen++;
            }
        }

        string headerString = _reader.ReadString(headerStringLen, Encoding.ASCII);
        string[] splitHeaderString = headerString.Split(' ');
        
        // We don't read every file into memory right now due to the fact that these archive files will often
        // contain the *entire game content* (all CGs, BGs, audio, scripts, etc), making them ginormous.
        // Instead, we keep the Stream open and save the offsets + lengths of each file so we can seek to the
        // file contents at a later point and read it into a separate MemoryStream.
        
        long indicesOffset = Convert.ToInt64(splitHeaderString[0], 16);
        ReadIndices(indicesOffset);
        
        // Version 3 RPA archives have obfuscated indices, for whatever reason.
        if (_version == 3)
        {
            int obfuscationKey = Convert.ToInt32(splitHeaderString[1], 16);
            
            foreach (ArchiveIndex index in _indices.Values)
            {
                index.Offset ^= obfuscationKey;
                index.Length ^= obfuscationKey;
            }
        }
    }

    private void ReadIndices(long offset)
    {
        _reader.Seek(offset, SeekOrigin.Begin);
        
        // TODO: Is this correct? Will there never be any data after the compressed indices?
        byte[] compressedData =_reader.ReadBytes((int)(_reader.Length - _reader.Position));

        using MemoryStream compressedStream = new MemoryStream(compressedData);
        
        using ZlibStream zlibStream = new ZlibStream(compressedStream, CompressionMode.Decompress);
        using MemoryStream decompressedStream = new MemoryStream();
        
        zlibStream.CopyTo(decompressedStream);
        decompressedStream.Seek(0, SeekOrigin.Begin);

        Unpickler unpickler = new Unpickler();
        Hashtable? unpickledHashtable = unpickler.load(decompressedStream) as Hashtable;
        
        // Lots of checks starting from here, just in case something changes...

        if (unpickledHashtable == null)
        {
            throw new RpaReaderException("Unpickled object is not a Hashtable");
        }

        foreach (string key in unpickledHashtable.Keys)
        {
            ArrayList? innerList = unpickledHashtable[key] as ArrayList;

            if (innerList == null)
            {
                throw new RpaReaderException("Expected ArrayList, got something else instead");
            }

            if (innerList.Count != 1)
            {
                throw new RpaReaderException("ArrayList has more than one item");
            }

            object[]? indexArray = innerList[0] as object[];

            if (indexArray == null)
            {
                throw new RpaReaderException("ArrayList item is not an object[]");
            }

            if (indexArray.Length != 3)
            {
                throw new RpaReaderException("Index array length is not 3");
            }

            string? arrString = indexArray[2] as string;

            if (arrString != "")
            {
                throw new RpaReaderException(
                    "Expected empty string for second item in index array, got something else instead");
            }
            
            _indices.Add(key, new ArchiveIndex()
            {
                Offset = (long)indexArray[0],
                Length = (int)indexArray[1]
            });
        }
    }

    public void Dispose()
    {
        _reader.Dispose();
        _stream.Dispose();
    }

    public Stream GetFile(string path)
    {
        if (!_indices.TryGetValue(path, out ArchiveIndex index))
        {
            throw new FileNotFoundException($"File '{path}' does not exist in this archive");
        }

        _reader.Seek(index.Offset, SeekOrigin.Begin);
        byte[] fileContents = _reader.ReadBytes(index.Length);

        return new MemoryStream(fileContents);
    }

    public IEnumerator<string> GetEnumerator()
    {
        return _indices.Keys.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
