using System.IO;
using csDBPF;

namespace SC4ModdingSuite.Models;

/// <summary>
/// Opens DBPF files for reading. Use this instead of <c>DbpfFileReader.Open(path)</c>.
/// <para>
/// csDBPF's <c>DBPFFile(string)</c> does <c>new FileStream(path, FileMode.Open)</c>, which
/// asks for read AND write access. That fails - and the app showed the file as an empty
/// tab - whenever the file has the read-only attribute, or another program (Reader,
/// PIM-X, SC4 itself...) has it open allowing others to read only. Those programs can
/// still read it, which made it look like a path-length problem to testers.
/// </para>
/// <para>
/// Here the file is opened read-only and shared with everyone (including programs that
/// write or delete it). csDBPF reads every entry into memory and closes the stream before
/// returning, so nothing stays locked; saving goes through <see cref="DbpfWriter"/>, which
/// opens the file itself.
/// </para>
/// </summary>
public static class DbpfFileReader
{
    public static DBPFFile Open(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        try
        {
            return new DBPFFile(stream);
        }
        finally
        {
            stream.Dispose(); // csDBPF already closes it; harmless, and covers a parse exception
        }
    }
}
